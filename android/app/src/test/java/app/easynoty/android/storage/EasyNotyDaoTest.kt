package app.easynoty.android.storage

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(application = android.app.Application::class)
class EasyNotyDaoTest {
    private lateinit var db: EasyNotyDatabase
    @Before fun setup() { db = Room.inMemoryDatabaseBuilder(ApplicationProvider.getApplicationContext<Context>(), EasyNotyDatabase::class.java).allowMainThreadQueries().build() }
    @After fun close() = db.close()

    @Test fun `pending delivery is scoped per pc and acked idempotently`() = runTest {
        val dao = db.dao()
        dao.upsertPc(PairedPcEntity("pc", "Desk", "127.0.0.1", 1234, "sha256:${"ab".repeat(32)}", "44444444-4444-4444-8444-444444444444", "alias", 1))
        dao.insertEvent(NotificationEventEntity("event", "instance", 0, "key", "pkg", "App", 1, 1, "{}", "{}", 2, "sha256:x", createdAtUtc = 1))
        dao.insertEventDeliveries(listOf(EventDeliveryEntity("event", "pc")))
        assertEquals(1, dao.pendingEvents("pc", 1).size)
        dao.acknowledgeEvent("event", "pc", 2)
        dao.acknowledgeEvent("event", "pc", 3)
        assertEquals(0, dao.pendingEvents("pc", 3).size)
    }

    @Test fun `paused pc retains backlog and backfill is bounded`() = runTest {
        val dao = db.dao()
        dao.upsertPc(PairedPcEntity("paused", "Desk", "127.0.0.1", 1234, "sha256:${"ab".repeat(32)}", "44444444-4444-4444-8444-444444444444", "alias", 1, isPaused = true))
        val repository = NotificationRepository(db)
        repository.persist(NotificationEventEntity("new", "instance-new", 0, "key", "pkg", "App", 100, 100, "{}", "{}", 2, "sha256:x", createdAtUtc = 100), emptyList())
        assertEquals(1, dao.pendingEvents("paused", 100).size)

        dao.upsertPc(PairedPcEntity("backfill", "Desk 2", "127.0.0.1", 1235, "sha256:${"cd".repeat(32)}", "55555555-5555-4555-8555-555555555555", "alias2", 1))
        dao.enqueueBackfill("backfill", 50)
        assertEquals(1, dao.pendingEvents("backfill", 100).size)
    }
}
