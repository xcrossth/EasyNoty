package app.easynoty.android.sync

import android.content.Context
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import app.easynoty.android.AppSettings
import app.easynoty.android.SyncMode
import java.util.concurrent.TimeUnit

class SyncScheduler(private val context: Context, private val settings: AppSettings) {
    private val workManager get() = WorkManager.getInstance(context)

    fun configure() {
        val constraints = constraints()
        val interval = if (settings.syncMode == SyncMode.BATTERY_SAVER) 30L else 15L
        workManager.enqueueUniquePeriodicWork(
            "periodic-sync",
            ExistingPeriodicWorkPolicy.UPDATE,
            PeriodicWorkRequestBuilder<SyncWorker>(interval, TimeUnit.MINUTES).setConstraints(constraints).build(),
        )
        workManager.enqueueUniquePeriodicWork(
            "cache-gc",
            ExistingPeriodicWorkPolicy.UPDATE,
            PeriodicWorkRequestBuilder<CacheGcWorker>(24, TimeUnit.HOURS).build(),
        )
    }

    fun requestImmediateSync() {
        if (settings.syncMode == SyncMode.BATTERY_SAVER) return
        workManager.enqueueUniqueWork(
            "immediate-sync",
            ExistingWorkPolicy.APPEND_OR_REPLACE,
            OneTimeWorkRequestBuilder<SyncWorker>().setConstraints(constraints()).build(),
        )
    }

    private fun constraints() = Constraints.Builder()
        .setRequiredNetworkType(if (settings.wifiOnly) NetworkType.UNMETERED else NetworkType.CONNECTED)
        .build()
}
