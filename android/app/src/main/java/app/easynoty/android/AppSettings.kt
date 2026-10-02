package app.easynoty.android

import android.content.Context

enum class SyncMode { BALANCED, REALTIME, BATTERY_SAVER }

class AppSettings(context: Context) {
    private val prefs = context.getSharedPreferences("settings", Context.MODE_PRIVATE)
    var syncMode: SyncMode
        get() = runCatching { SyncMode.valueOf(prefs.getString("sync_mode", SyncMode.BALANCED.name)!!) }.getOrDefault(SyncMode.BALANCED)
        set(value) { prefs.edit().putString("sync_mode", value.name).apply() }
    var syncImages: Boolean
        get() = prefs.getBoolean("sync_images", true)
        set(value) { prefs.edit().putBoolean("sync_images", value).apply() }
    var wifiOnly: Boolean
        get() = prefs.getBoolean("wifi_only", true)
        set(value) { prefs.edit().putBoolean("wifi_only", value).apply() }
    var compressionLevel: Int
        get() = prefs.getInt("compression_level", 9)
        set(value) { require(value in listOf(6, 9, 12)); prefs.edit().putInt("compression_level", value).apply() }
    var compressionThreshold: Double
        get() = java.lang.Double.longBitsToDouble(prefs.getLong("compression_threshold", 0.10.toBits()))
        set(value) { require(value in listOf(0.05, 0.10, 0.15, 0.20)); prefs.edit().putLong("compression_threshold", value.toBits()).apply() }
}
