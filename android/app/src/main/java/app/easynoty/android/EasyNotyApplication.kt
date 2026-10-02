package app.easynoty.android

import android.app.Application
import app.easynoty.android.storage.EasyNotyDatabase
import app.easynoty.android.storage.NotificationRepository
import app.easynoty.android.security.PairSecretStore
import app.easynoty.android.sync.SyncScheduler

class EasyNotyApplication : Application() {
    val database by lazy { EasyNotyDatabase.create(this) }
    val repository by lazy { NotificationRepository(database) }
    val settings by lazy { AppSettings(this) }
    val secretStore by lazy { PairSecretStore(this) }
    val deviceIdentity by lazy { DeviceIdentity(this) }
    val scheduler by lazy { SyncScheduler(this, settings) }

    override fun onCreate() {
        super.onCreate()
        scheduler.configure()
    }
}
