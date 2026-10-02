package app.easynoty.android.storage

import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index

object DeliveryStatus {
    const val PENDING = "pending"
    const val IN_FLIGHT = "in_flight"
    const val ACKED = "acked"
    const val FAILED = "failed"
}

@Entity(tableName = "paired_windows_devices", primaryKeys = ["pcDeviceId"])
data class PairedPcEntity(
    val pcDeviceId: String,
    val pcName: String,
    val host: String,
    val port: Int,
    val certificateSha256: String,
    val keyId: String,
    val secretAlias: String,
    val pairedAtUtc: Long,
    val lastSeenAtUtc: Long? = null,
    val isPaused: Boolean = false,
    val isRevoked: Boolean = false,
    val backfillSinceUtc: Long? = null,
)

@Entity(tableName = "app_allowlist", primaryKeys = ["packageName"])
data class AllowlistedAppEntity(
    val packageName: String,
    val appName: String,
    val isAllowed: Boolean = false,
    val lastSeenAtUtc: Long? = null,
    val iconAssetHash: String? = null,
)

@Entity(
    tableName = "notification_events",
    indices = [Index("notificationKey"), Index(value = ["notificationInstanceId", "revisionIndex"], unique = true)],
)
data class NotificationEventEntity(
    @androidx.room.PrimaryKey val eventId: String,
    val notificationInstanceId: String,
    val revisionIndex: Int,
    val notificationKey: String,
    val packageName: String,
    val appName: String,
    val postedAtUtc: Long,
    val capturedAtUtc: Long,
    val rawSnapshotJson: String,
    val parsedResultJson: String,
    val rawByteSize: Long,
    val rawSha256: String,
    val groupKey: String? = null,
    val isGroupSummary: Boolean = false,
    val isOngoing: Boolean = false,
    val style: String = "generic",
    val sortKey: String? = null,
    val isActive: Boolean = true,
    val captureError: String? = null,
    val createdAtUtc: Long,
)

@Entity(
    tableName = "notification_event_delivery",
    primaryKeys = ["eventId", "pcDeviceId"],
    foreignKeys = [
        ForeignKey(entity = NotificationEventEntity::class, parentColumns = ["eventId"], childColumns = ["eventId"], onDelete = ForeignKey.CASCADE),
        ForeignKey(entity = PairedPcEntity::class, parentColumns = ["pcDeviceId"], childColumns = ["pcDeviceId"], onDelete = ForeignKey.CASCADE),
    ],
    indices = [Index("pcDeviceId"), Index("status")],
)
data class EventDeliveryEntity(
    val eventId: String,
    val pcDeviceId: String,
    val status: String = DeliveryStatus.PENDING,
    val retryCount: Int = 0,
    val nextAttemptAtUtc: Long = 0,
    val lastAttemptAtUtc: Long? = null,
    val lastError: String? = null,
    val ackedAtUtc: Long? = null,
)

@Entity(tableName = "assets", primaryKeys = ["hash"])
data class AssetEntity(
    val hash: String,
    val mime: String,
    val byteSize: Long,
    val width: Int? = null,
    val height: Int? = null,
    val sourceKind: String,
    val originalityLevel: String,
    val localPath: String,
    val createdAtUtc: Long,
    val lastSeenAtUtc: Long,
)

@Entity(
    tableName = "notification_assets",
    primaryKeys = ["eventId", "assetHash", "role"],
    foreignKeys = [
        ForeignKey(entity = NotificationEventEntity::class, parentColumns = ["eventId"], childColumns = ["eventId"], onDelete = ForeignKey.CASCADE),
        ForeignKey(entity = AssetEntity::class, parentColumns = ["hash"], childColumns = ["assetHash"], onDelete = ForeignKey.CASCADE),
    ],
    indices = [Index("assetHash")],
)
data class NotificationAssetEntity(val eventId: String, val assetHash: String, val role: String)

@Entity(
    tableName = "asset_delivery",
    primaryKeys = ["assetHash", "pcDeviceId"],
    foreignKeys = [
        ForeignKey(entity = AssetEntity::class, parentColumns = ["hash"], childColumns = ["assetHash"], onDelete = ForeignKey.CASCADE),
        ForeignKey(entity = PairedPcEntity::class, parentColumns = ["pcDeviceId"], childColumns = ["pcDeviceId"], onDelete = ForeignKey.CASCADE),
    ],
    indices = [Index("pcDeviceId"), Index("status")],
)
data class AssetDeliveryEntity(
    val assetHash: String,
    val pcDeviceId: String,
    val status: String = DeliveryStatus.PENDING,
    val retryCount: Int = 0,
    val nextAttemptAtUtc: Long = 0,
    val lastAttemptAtUtc: Long? = null,
    val lastError: String? = null,
    val ackedAtUtc: Long? = null,
)

data class PendingEvent(
    @androidx.room.Embedded val delivery: EventDeliveryEntity,
    @androidx.room.Relation(parentColumn = "eventId", entityColumn = "eventId") val event: NotificationEventEntity,
)
