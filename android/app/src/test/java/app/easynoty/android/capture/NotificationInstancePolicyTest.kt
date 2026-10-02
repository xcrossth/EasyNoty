package app.easynoty.android.capture

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Test

class NotificationInstancePolicyTest {
    @Test fun `ongoing post revises active instance`() {
        val result = NotificationInstancePolicy.decide(true, "instance", 3)
        assertEquals("instance", result.instanceId)
        assertEquals(4, result.revisionIndex)
    }

    @Test fun `ordinary post always creates a new instance`() {
        val result = NotificationInstancePolicy.decide(false, "old", 3)
        assertNotEquals("old", result.instanceId)
        assertEquals(0, result.revisionIndex)
    }
}
