package app.easynoty.android.storage

import androidx.room.withTransaction

class NotificationRepository(private val database: EasyNotyDatabase) {
    val dao: EasyNotyDao = database.dao()

    suspend fun persist(event: NotificationEventEntity, assets: List<Pair<AssetEntity, String>>) {
        database.withTransaction {
            if (dao.insertEvent(event) == -1L) return@withTransaction
            assets.forEach { (asset, _) -> dao.insertAsset(asset) }
            dao.insertNotificationAssets(assets.map { (asset, role) -> NotificationAssetEntity(event.eventId, asset.hash, role) })
            val pcs = dao.deliveryPcs()
            dao.insertEventDeliveries(pcs.map { EventDeliveryEntity(event.eventId, it.pcDeviceId) })
        }
    }

    suspend fun revokePc(pcId: String, deleteSecret: () -> Unit) {
        database.withTransaction {
            dao.markPcRevoked(pcId)
            dao.deletePcEventQueue(pcId)
            dao.deletePcAssetQueue(pcId)
        }
        deleteSecret()
    }
}
