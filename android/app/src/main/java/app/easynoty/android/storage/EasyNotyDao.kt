package app.easynoty.android.storage

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import kotlinx.coroutines.flow.Flow

@Dao
interface EasyNotyDao {
    @Query("SELECT * FROM paired_windows_devices WHERE isRevoked = 0 ORDER BY pcName")
    fun observePairedPcs(): Flow<List<PairedPcEntity>>

    @Query("SELECT * FROM paired_windows_devices WHERE isRevoked = 0 AND isPaused = 0")
    suspend fun activePcs(): List<PairedPcEntity>

    @Query("SELECT * FROM paired_windows_devices WHERE isRevoked = 0")
    suspend fun deliveryPcs(): List<PairedPcEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertPc(pc: PairedPcEntity)

    @Query("UPDATE paired_windows_devices SET isPaused = :paused WHERE pcDeviceId = :pcId")
    suspend fun setPcPaused(pcId: String, paused: Boolean)

    @Query("UPDATE paired_windows_devices SET isRevoked = 1, isPaused = 1 WHERE pcDeviceId = :pcId")
    suspend fun markPcRevoked(pcId: String)

    @Query("UPDATE paired_windows_devices SET lastSeenAtUtc = :now WHERE pcDeviceId = :pcId")
    suspend fun updatePcLastSeen(pcId: String, now: Long)

    @Query("DELETE FROM notification_event_delivery WHERE pcDeviceId = :pcId")
    suspend fun deletePcEventQueue(pcId: String)

    @Query("DELETE FROM asset_delivery WHERE pcDeviceId = :pcId")
    suspend fun deletePcAssetQueue(pcId: String)

    @Query("SELECT * FROM app_allowlist ORDER BY appName COLLATE NOCASE")
    fun observeAllowlist(): Flow<List<AllowlistedAppEntity>>

    @Query("SELECT EXISTS(SELECT 1 FROM app_allowlist WHERE packageName = :packageName AND isAllowed = 1)")
    suspend fun isAllowed(packageName: String): Boolean

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAllowlist(item: AllowlistedAppEntity)

    @Query("SELECT * FROM notification_events WHERE notificationKey = :key AND isOngoing = 1 ORDER BY revisionIndex DESC LIMIT 1")
    suspend fun latestOngoing(key: String): NotificationEventEntity?

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertEvent(event: NotificationEventEntity): Long

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertEventDeliveries(deliveries: List<EventDeliveryEntity>)

    @Query("INSERT OR IGNORE INTO notification_event_delivery (eventId, pcDeviceId, status, retryCount, nextAttemptAtUtc) SELECT eventId, :pcId, 'pending', 0, 0 FROM notification_events WHERE createdAtUtc >= :sinceUtc")
    suspend fun enqueueBackfill(pcId: String, sinceUtc: Long)

    @Query("UPDATE notification_events SET isActive = 0 WHERE notificationKey = :key AND isActive = 1")
    suspend fun closeNotification(key: String)

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertAsset(asset: AssetEntity): Long

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertNotificationAssets(assets: List<NotificationAssetEntity>)

    @Query("SELECT * FROM assets WHERE hash = :hash LIMIT 1")
    suspend fun asset(hash: String): AssetEntity?

    @Query("SELECT * FROM notification_assets WHERE eventId = :eventId")
    suspend fun eventAssets(eventId: String): List<NotificationAssetEntity>

    @Query("SELECT COUNT(*) FROM notification_event_delivery WHERE status != 'acked'")
    fun observePendingEventCount(): Flow<Int>

    @Query("SELECT COUNT(*) FROM asset_delivery WHERE status != 'acked'")
    fun observePendingAssetCount(): Flow<Int>

    @Transaction
    @Query("SELECT * FROM notification_event_delivery WHERE pcDeviceId = :pcId AND status != 'acked' AND nextAttemptAtUtc <= :now ORDER BY rowid LIMIT :limit")
    suspend fun pendingEvents(pcId: String, now: Long, limit: Int = 50): List<PendingEvent>

    @Query("UPDATE notification_event_delivery SET status = 'in_flight', lastAttemptAtUtc = :now WHERE eventId = :eventId AND pcDeviceId = :pcId")
    suspend fun markEventInFlight(eventId: String, pcId: String, now: Long)

    @Query("UPDATE notification_event_delivery SET status = 'acked', ackedAtUtc = :now, lastError = NULL WHERE eventId = :eventId AND pcDeviceId = :pcId")
    suspend fun acknowledgeEvent(eventId: String, pcId: String, now: Long)

    @Query("UPDATE notification_event_delivery SET status = 'failed', retryCount = retryCount + 1, nextAttemptAtUtc = :retryAt, lastError = :error WHERE eventId = :eventId AND pcDeviceId = :pcId")
    suspend fun failEvent(eventId: String, pcId: String, retryAt: Long, error: String)

    @Query("UPDATE asset_delivery SET status = 'acked', ackedAtUtc = :now, lastError = NULL WHERE assetHash = :hash AND pcDeviceId = :pcId")
    suspend fun acknowledgeAsset(hash: String, pcId: String, now: Long)

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun enqueueAssetDelivery(delivery: AssetDeliveryEntity)

    @Query("DELETE FROM notification_events WHERE createdAtUtc < :cutoff AND NOT EXISTS (SELECT 1 FROM notification_event_delivery d WHERE d.eventId = notification_events.eventId AND d.status != 'acked')")
    suspend fun deleteFullyDeliveredEventsBefore(cutoff: Long): Int

    @Query("SELECT * FROM assets WHERE createdAtUtc < :cutoff AND NOT EXISTS (SELECT 1 FROM notification_assets n WHERE n.assetHash = assets.hash) AND NOT EXISTS (SELECT 1 FROM asset_delivery d WHERE d.assetHash = assets.hash AND d.status != 'acked')")
    suspend fun orphanAssetsBefore(cutoff: Long): List<AssetEntity>

    @Query("DELETE FROM assets WHERE hash = :hash")
    suspend fun deleteAsset(hash: String)
}
