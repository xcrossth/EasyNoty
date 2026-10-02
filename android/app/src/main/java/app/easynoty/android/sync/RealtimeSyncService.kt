package app.easynoty.android.sync

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.os.IBinder
import androidx.core.app.NotificationCompat
import app.easynoty.android.R
import app.easynoty.android.EasyNotyApplication
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

class RealtimeSyncService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    override fun onCreate() {
        super.onCreate()
        val manager = getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(NotificationChannel("realtime-sync", getString(R.string.realtime_channel), NotificationManager.IMPORTANCE_LOW))
        startForeground(42, NotificationCompat.Builder(this, "realtime-sync")
            .setSmallIcon(R.drawable.ic_easynoty)
            .setContentTitle(getString(R.string.app_name))
            .setContentText(getString(R.string.realtime_running))
            .setOngoing(true)
            .build())
        scope.launch {
            while (isActive) {
                (application as EasyNotyApplication).scheduler.requestImmediateSync()
                delay(15_000)
            }
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int = START_STICKY
    override fun onBind(intent: Intent?): IBinder? = null
    override fun onDestroy() { scope.cancel(); super.onDestroy() }
}
