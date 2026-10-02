package app.easynoty.android.capture

import java.util.UUID

data class InstanceRevision(val instanceId: String, val revisionIndex: Int)

object NotificationInstancePolicy {
    fun decide(isOngoing: Boolean, previousInstanceId: String?, previousRevisionIndex: Int?): InstanceRevision =
        if (isOngoing && previousInstanceId != null && previousRevisionIndex != null) {
            InstanceRevision(previousInstanceId, previousRevisionIndex + 1)
        } else {
            InstanceRevision(UUID.randomUUID().toString(), 0)
        }
}
