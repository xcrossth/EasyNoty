package app.easynoty.android.capture

import android.app.Notification
import android.os.BaseBundle
import android.os.Build
import android.os.Bundle
import android.os.Parcelable
import android.service.notification.StatusBarNotification
import app.easynoty.android.core.Hashing
import app.easynoty.android.core.CanonicalJson
import org.json.JSONArray
import org.json.JSONObject

data class CapturedSnapshot(
    val rawJson: String,
    val parsedJson: String,
    val groupKey: String?,
    val isGroupSummary: Boolean,
    val isOngoing: Boolean,
    val style: String,
    val sortKey: String?,
)

class SnapshotBuilder(
    private val androidVersion: Int = Build.VERSION.SDK_INT,
) {
    fun build(sbn: StatusBarNotification, appName: String, deviceId: String): CapturedSnapshot {
        val notification = sbn.notification
        val extras = safeBundle(notification.extras)
        val isSummary = notification.flags and Notification.FLAG_GROUP_SUMMARY != 0
        val isOngoing = sbn.isOngoing || notification.flags and Notification.FLAG_ONGOING_EVENT != 0
        val raw = JSONObject()
            .put("capture_schema_version", 1)
            .put("android_version", androidVersion)
            .put("device_id", deviceId)
            .put("package_name", sbn.packageName)
            .put("app_name", appName)
            .put("notification_key", sbn.key)
            .put("notification_id", sbn.id)
            .put("tag", sbn.tag)
            .put("group_key", sbn.groupKey)
            .put("post_time_utc_ms", sbn.postTime)
            .put("when_utc_ms", notification.`when`)
            .put("category", notification.category)
            .put("channel_id", notification.channelId)
            .put("flags", notification.flags)
            .put("is_ongoing", isOngoing)
            .put("is_clearable", sbn.isClearable)
            .put("is_group_summary", isSummary)
            .put("style_template", notification.extras.getString(Notification.EXTRA_TEMPLATE))
            .put("extras_raw", extras)
            .put("actions_summary", actions(notification))

        val title = notification.extras.getCharSequence(Notification.EXTRA_TITLE)?.toString()?.take(16384)
        val body = notification.extras.getCharSequence(Notification.EXTRA_BIG_TEXT)?.toString()
            ?: notification.extras.getCharSequence(Notification.EXTRA_TEXT)?.toString()
        val boundedBody = body?.take(1_048_576)
        val parsed = JSONObject()
            .put("parser_name", "generic_android")
            .put("parser_version", 1)
            .put("title", title ?: JSONObject.NULL)
            .put("body", boundedBody ?: JSONObject.NULL)
            .put("conversation_title", notification.extras.getCharSequence(Notification.EXTRA_CONVERSATION_TITLE)?.toString()?.take(16384) ?: JSONObject.NULL)
            .put("sender_name", inferSender(notification.extras)?.take(16384) ?: JSONObject.NULL)
            .put("preview_text", boundedBody?.take(65536) ?: JSONObject.NULL)
            .put("confidence", if (title != null || body != null) 0.7 else 0.25)
        return CapturedSnapshot(CanonicalJson.string(raw.toString()), CanonicalJson.string(parsed.toString()), sbn.groupKey, isSummary, isOngoing, inferStyle(notification, isOngoing), notification.sortKey)
    }

    fun minimal(sbn: StatusBarNotification, appName: String, deviceId: String, error: Throwable): CapturedSnapshot {
        val raw = JSONObject()
            .put("capture_schema_version", 1)
            .put("device_id", deviceId)
            .put("package_name", sbn.packageName)
            .put("app_name", appName)
            .put("notification_key", sbn.key)
            .put("post_time_utc_ms", sbn.postTime)
            .put("capture_error", error.javaClass.simpleName)
        val parsed = JSONObject().put("parser_name", "minimal").put("parser_version", 1)
            .put("title", JSONObject.NULL).put("body", JSONObject.NULL).put("sender_name", JSONObject.NULL)
            .put("conversation_title", JSONObject.NULL).put("preview_text", JSONObject.NULL).put("confidence", 0)
        return CapturedSnapshot(CanonicalJson.string(raw.toString()), CanonicalJson.string(parsed.toString()), sbn.groupKey, false, sbn.isOngoing, "unknown", sbn.notification.sortKey)
    }

    private fun inferStyle(notification: Notification, ongoing: Boolean): String {
        val template = notification.extras.getString(Notification.EXTRA_TEMPLATE).orEmpty()
        return when {
            template.contains("MessagingStyle") -> "messaging"
            template.contains("BigTextStyle") -> "big_text"
            template.contains("BigPictureStyle") -> "big_picture"
            template.contains("InboxStyle") -> "inbox"
            template.contains("MediaStyle") -> "media"
            notification.extras.containsKey(Notification.EXTRA_PROGRESS_MAX) -> "progress"
            ongoing -> "service"
            template.isEmpty() -> "generic"
            else -> "unknown"
        }
    }

    private fun inferSender(extras: Bundle): String? {
        val messages = extras.getParcelableArray(Notification.EXTRA_MESSAGES)
        val latest = messages?.lastOrNull() as? Bundle ?: return null
        return latest.getCharSequence("sender")?.toString()
            ?: latest.getParcelable<Parcelable>("sender_person")?.toString()
    }

    private fun actions(notification: Notification): JSONArray = JSONArray().also { array ->
        notification.actions?.forEach { action ->
            array.put(JSONObject().put("title", action.title?.toString()).put("has_remote_inputs", !action.remoteInputs.isNullOrEmpty()))
        }
    }

    private fun safeBundle(bundle: Bundle?, depth: Int = 0): JSONObject {
        val result = JSONObject()
        bundle?.keySet()?.forEach { key ->
            result.put(key, safeValue(runCatching { bundle.get(key) }.getOrNull(), depth))
        }
        return result
    }

    private fun safeValue(value: Any?, depth: Int): Any? {
        if (value == null) return JSONObject.NULL
        if (depth >= 4) return JSONObject().put("type", value.javaClass.name).put("summary", value.toString().take(512))
        return when (value) {
            is String, is Number, is Boolean -> value
            is CharSequence -> value.toString()
            is Bundle -> safeBundle(value, depth + 1)
            is BaseBundle -> JSONObject().put("type", value.javaClass.name).put("summary", value.toString().take(512))
            is Array<*> -> JSONArray(value.map { safeValue(it, depth + 1) })
            is IntArray -> JSONArray(value.toList())
            is LongArray -> JSONArray(value.toList())
            is BooleanArray -> JSONArray(value.toList())
            is Iterable<*> -> JSONArray(value.map { safeValue(it, depth + 1) })
            else -> JSONObject().put("type", value.javaClass.name).put("summary", value.toString().take(512))
        }
    }
}

fun rawMetadata(rawJson: String): Pair<Long, String> {
    val bytes = CanonicalJson.bytes(rawJson)
    return bytes.size.toLong() to Hashing.assetId(bytes)
}
