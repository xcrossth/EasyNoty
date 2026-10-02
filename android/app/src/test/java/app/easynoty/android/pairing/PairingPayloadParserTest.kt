package app.easynoty.android.pairing

import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import org.junit.Test
import java.time.Instant

class PairingPayloadParserTest {
    private val now = Instant.parse("2026-07-05T00:00:00Z")
    private fun payload(expiry: String = "2026-07-05T00:02:00Z") = """{
        "protocol_version":1,"pc_device_id":"11111111-1111-4111-8111-111111111111","pc_name":"Desk","endpoints":[{"host":"192.168.1.2","port":38492}],
        "certificate_sha256":"sha256:${"ab".repeat(32)}","pairing_token":"0123456789abcdef0123456789abcdef","expires_at_utc":"$expiry"
    }"""

    @Test fun `accepts valid unexpired payload`() {
        assertEquals("11111111-1111-4111-8111-111111111111", PairingPayloadParser { now }.parse(payload()).pcDeviceId)
    }

    @Test fun `rejects expired one-time token`() {
        try {
            PairingPayloadParser { now }.parse(payload("2026-07-04T23:59:59Z"))
            fail("Expected expired payload to be rejected")
        } catch (_: IllegalArgumentException) { }
    }
}
