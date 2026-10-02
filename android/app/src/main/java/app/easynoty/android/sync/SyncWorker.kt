package app.easynoty.android.sync

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import app.easynoty.android.EasyNotyApplication
import java.util.concurrent.TimeUnit

class SyncWorker(appContext: Context, params: WorkerParameters) : CoroutineWorker(appContext, params) {
    override suspend fun doWork(): Result {
        val app = applicationContext as EasyNotyApplication
        val pcs = app.database.dao().activePcs()
        if (pcs.isEmpty()) return Result.success()
        val client = WebSocketSyncClient(app)
        val failures = pcs.count { client.syncOnce(it).isFailure }
        return if (failures == 0) Result.success() else Result.retry()
    }
}

class CacheGcWorker(appContext: Context, params: WorkerParameters) : CoroutineWorker(appContext, params) {
    override suspend fun doWork(): Result {
        val app = applicationContext as EasyNotyApplication
        val cutoff = System.currentTimeMillis() - TimeUnit.DAYS.toMillis(7)
        app.database.dao().deleteFullyDeliveredEventsBefore(cutoff)
        app.database.dao().orphanAssetsBefore(cutoff).forEach { asset ->
            if (java.io.File(asset.localPath).delete() || !java.io.File(asset.localPath).exists()) app.database.dao().deleteAsset(asset.hash)
        }
        return Result.success()
    }
}
