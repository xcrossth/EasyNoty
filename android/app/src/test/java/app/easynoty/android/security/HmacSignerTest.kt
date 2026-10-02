package app.easynoty.android.security

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class HmacSignerTest {
    @Test fun `signature binds metadata and payload hash`() {
        val secret = ByteArray(32) { it.toByte() }
        val canonical = HmacSigner.canonical(1, "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "notification_event", "22222222-2222-4222-8222-222222222222", "2026-07-05T00:00:00Z", "55555555-5555-4555-8555-555555555555", "sha256:${"ab".repeat(32)}")
        val signature = HmacSigner.sign(secret, canonical)
        assertTrue(HmacSigner.verify(secret, canonical, signature))
        assertFalse(HmacSigner.verify(secret, canonical + 1.toByte(), signature))
    }

    @Test fun `matches normative protocol vector`() {
        val json = org.json.JSONObject(requireNotNull(javaClass.classLoader?.getResource("vectors/hmac-sha256.json")).readText())
        val envelope = json.getJSONObject("envelope")
        val payloadCanonical = app.easynoty.android.core.CanonicalJson.string(envelope.getJSONObject("payload").toString())
        assertTrue(payloadCanonical == json.getString("canonical_payload_utf8"))
        val payloadHash = app.easynoty.android.core.Hashing.assetId(payloadCanonical.toByteArray())
        val input = HmacSigner.canonical(
            envelope.getInt("protocol_version"), envelope.getString("message_id"), envelope.getString("message_type"),
            envelope.getString("device_id"), envelope.getString("sent_at_utc"), envelope.getString("session_id"), payloadHash,
        )
        assertTrue(input.toString(Charsets.UTF_8) == json.getString("signing_input_utf8"))
        val secret = java.util.Base64.getUrlDecoder().decode(json.getString("secret_b64url"))
        assertTrue(HmacSigner.sign(secret, input) == json.getString("signature_hex"))
    }
}
