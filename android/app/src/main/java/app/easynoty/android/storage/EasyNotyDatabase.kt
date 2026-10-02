package app.easynoty.android.storage

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase

@Database(
    entities = [
        PairedPcEntity::class,
        AllowlistedAppEntity::class,
        NotificationEventEntity::class,
        EventDeliveryEntity::class,
        AssetEntity::class,
        NotificationAssetEntity::class,
        AssetDeliveryEntity::class,
    ],
    version = 1,
    exportSchema = true,
)
abstract class EasyNotyDatabase : RoomDatabase() {
    abstract fun dao(): EasyNotyDao

    companion object {
        fun create(context: Context): EasyNotyDatabase =
            Room.databaseBuilder(context, EasyNotyDatabase::class.java, "easynoty.db")
                .setJournalMode(JournalMode.WRITE_AHEAD_LOGGING)
                .build()
    }
}
