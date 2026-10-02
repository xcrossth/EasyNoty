package app.easynoty.android.capture

import android.provider.Settings
import android.service.notification.NotificationListenerService
import android.service.notification.StatusBarNotification
import app.easynoty.android.EasyNotyApplication
import app.easynoty.android.storage.NotificationEventEntity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import java.util.UUID

class EasyNotyNotificationListenerService : NotificationListenerService() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    override fun onNotificationPosted(sbn: StatusBarNotification) {
        scope.launch { capture(sbn) }
    }

    override fun onNotificationRemoved(sbn: StatusBarNotification) {
        scope.launch { app().database.dao().closeNotification(sbn.key) }
    }

    private suspend fun capture(sbn: StatusBarNotification) {
        val application = app()
        val dao = application.database.dao()
        if (!dao.isAllowed(sbn.packageName)) return
        val appName = runCatching {
            packageManager.getApplicationLabel(packageManager.getApplicationInfo(sbn.packageName, 0)).toString()
        }.getOrDefault(sbn.packageName)
        val deviceId = application.deviceIdentity.id
        var captureError: Throwable? = null
        val snapshot = runCatching { SnapshotBuilder().build(sbn, appName, deviceId) }.getOrElse {
            captureError = it
            SnapshotBuilder().minimal(sbn, appName, deviceId, it)
        }
        val previous = if (snapshot.isOngoing) dao.latestOngoing(sbn.key) else null
        val decision = NotificationInstancePolicy.decide(snapshot.isOngoing, previous?.notificationInstanceId, previous?.revisionIndex)
        val eventId = UUID.randomUUID().toString()
        val (rawSize, rawHash) = rawMetadata(snapshot.rawJson)
        val now = System.currentTimeMillis()
        val event = NotificationEventEntity(
            eventId = eventId,
            notificationInstanceId = decision.instanceId,
            revisionIndex = decision.revisionIndex,
            notificationKey = sbn.key,
            packageName = sbn.packageName,
            appName = appName,
            postedAtUtc = sbn.postTime,
            capturedAtUtc = now,
            rawSnapshotJson = snapshot.rawJson,
            parsedResultJson = snapshot.parsedJson,
            rawByteSize = rawSize,
            rawSha256 = rawHash,
            groupKey = snapshot.groupKey,
            isGroupSummary = snapshot.isGroupSummary,
            isOngoing = snapshot.isOngoing,
            style = snapshot.style,
            sortKey = snapshot.sortKey,
            captureError = captureError?.javaClass?.simpleName,
            createdAtUtc = now,
        )
        val syncImages = application.settings.syncImages
        val assets = runCatching { AssetStore(this).capture(sbn.notification, sbn.packageName, syncImages) }.getOrDefault(emptyList())
        application.repository.persist(event, assets)
        application.scheduler.requestImmediateSync()
    }

    private fun app() = application as EasyNotyApplication

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }
}
