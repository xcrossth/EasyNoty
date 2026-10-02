package app.easynoty.android.ui

import android.app.Application
import android.content.ComponentName
import android.content.Intent
import android.provider.Settings
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import app.easynoty.android.EasyNotyApplication
import app.easynoty.android.SyncMode
import app.easynoty.android.capture.EasyNotyNotificationListenerService
import app.easynoty.android.storage.AllowlistedAppEntity
import app.easynoty.android.storage.PairedPcEntity
import app.easynoty.android.pairing.PairingManager
import app.easynoty.android.sync.RealtimeSyncService
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

data class InstalledApp(val packageName: String, val label: String, val allowed: Boolean)
data class StatusState(val pcs: List<PairedPcEntity> = emptyList(), val pendingEvents: Int = 0, val pendingAssets: Int = 0)

class MainViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as EasyNotyApplication
    private val dao = app.database.dao()
    val status: StateFlow<StatusState> = combine(
        dao.observePairedPcs(), dao.observePendingEventCount(), dao.observePendingAssetCount(),
    ) { pcs, events, assets -> StatusState(pcs, events, assets) }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), StatusState())
    val allowlist = dao.observeAllowlist().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())
    val pairingMessage = MutableStateFlow<String?>(null)

    fun notificationAccessEnabled(): Boolean {
        val component = ComponentName(getApplication(), EasyNotyNotificationListenerService::class.java)
        return Settings.Secure.getString(getApplication<Application>().contentResolver, "enabled_notification_listeners")
            ?.split(':')?.any { ComponentName.unflattenFromString(it) == component } == true
    }

    fun installedApps(): List<InstalledApp> {
        val pm = getApplication<Application>().packageManager
        val allowed = allowlist.value.associateBy { it.packageName }
        return pm.queryIntentActivities(Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_LAUNCHER), 0)
            .map { InstalledApp(it.activityInfo.packageName, it.loadLabel(pm).toString(), allowed[it.activityInfo.packageName]?.isAllowed == true) }
            .distinctBy { it.packageName }
            .sortedBy { it.label.lowercase() }
    }

    fun setAllowed(item: InstalledApp, allowed: Boolean) = viewModelScope.launch {
        dao.upsertAllowlist(AllowlistedAppEntity(item.packageName, item.label, allowed, System.currentTimeMillis()))
    }

    fun pair(raw: String, backfill: Boolean) = viewModelScope.launch {
        pairingMessage.value = "Pairing…"
        val result = PairingManager(app).pair(raw, backfill)
        pairingMessage.value = result.fold({ "Paired with ${it.pcName}" }, { "Pairing failed: ${it.message}" })
    }

    fun pause(pc: PairedPcEntity, paused: Boolean) = viewModelScope.launch { dao.setPcPaused(pc.pcDeviceId, paused) }

    fun revoke(pc: PairedPcEntity) = viewModelScope.launch {
        app.repository.revokePc(pc.pcDeviceId) { app.secretStore.delete(pc.secretAlias) }
    }

    fun syncMode() = app.settings.syncMode
    fun setSyncMode(mode: SyncMode) {
        app.settings.syncMode = mode
        app.scheduler.configure()
        val intent = Intent(app, RealtimeSyncService::class.java)
        if (mode == SyncMode.REALTIME) app.startForegroundService(intent) else app.stopService(intent)
    }
    fun syncImages() = app.settings.syncImages
    fun setSyncImages(enabled: Boolean) { app.settings.syncImages = enabled }
    fun wifiOnly() = app.settings.wifiOnly
    fun setWifiOnly(enabled: Boolean) { app.settings.wifiOnly = enabled; app.scheduler.configure() }
}
