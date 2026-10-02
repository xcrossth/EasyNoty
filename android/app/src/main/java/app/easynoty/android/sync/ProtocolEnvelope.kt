package app.easynoty.android.sync

import app.easynoty.android.core.CanonicalJson
import app.easynoty.android.core.Hashing
import app.easynoty.android.security.HmacSigner
import app.easynoty.android.security.PairSecretStore
import org.json.JSONObject
import java.time.Instant
import java.util.UUID

data class ProtocolAuthentication(val keyId: String, val payloadSha256: String, val signature: String) {
    fun toJson() = JSONObject()
        .put("algorithm", "hmac-sha256")
        .put("key_id", keyId)
        .put("payload_sha256", payloadSha256)
        .put("signature", signature)
}

data class ProtocolEnvelope(
    val protocolVersion: Int,
    val messageId: String,
    val messageType: String,
    val deviceId: String,
    val sentAtUtc: String,
    val sessionId: String?,
    val payload: JSONObject,
    val authentication: ProtocolAuthentication?,
) {
    fun toJson(): String = JSONObject()
        .put("protocol_version", protocolVersion)
        .put("message_id", messageId)
        .put("message_type", messageType)
        .put("device_id", deviceId)
        .put("sent_at_utc", sentAtUtc)
        .put("session_id", sessionId ?: JSONObject.NULL)
        .put("payload", payload)
        .put("authentication", authentication?.toJson() ?: JSONObject.NULL)
        .toString()

    fun verify(secret: ByteArray, expectedKeyId: String, expectedSessionId: String): Boolean {
        val auth = authentication ?: return false
        if (sessionId != expectedSessionId || auth.keyId != expectedKeyId) return false
        val canonicalPayload = CanonicalJson.bytes(payload.toString())
        val payloadHash = Hashing.assetId(canonicalPayload)
        if (payloadHash != auth.payloadSha256) return false
        val signingInput = HmacSigner.canonical(protocolVersion, messageId, messageType, deviceId, sentAtUtc, expectedSessionId, payloadHash)
        return HmacSigner.verify(secret, signingInput, auth.signature)
    }

    companion object {
        fun unsigned(type: String, deviceId: String, payload: JSONObject): ProtocolEnvelope = ProtocolEnvelope(
            1, UUID.randomUUID().toString(), type, deviceId, Instant.now().toString(), null, payload, null,
        )

        fun signed(
            type: String,
            deviceId: String,
            sessionId: String,
            payload: JSONObject,
            keyId: String,
            secretAlias: String,
            secrets: PairSecretStore,
            messageId: String = UUID.randomUUID().toString(),
            sentAtUtc: String = Instant.now().toString(),
        ): ProtocolEnvelope {
            val payloadHash = Hashing.assetId(CanonicalJson.bytes(payload.toString()))
            val signingInput = HmacSigner.canonical(1, messageId, type, deviceId, sentAtUtc, sessionId, payloadHash)
            val auth = ProtocolAuthentication(keyId, payloadHash, secrets.sign(secretAlias, signingInput))
            return ProtocolEnvelope(1, messageId, type, deviceId, sentAtUtc, sessionId, payload, auth)
        }

        fun parse(text: String): ProtocolEnvelope {
            val json = JSONObject(text)
            val authJson = json.optJSONObject("authentication")
            return ProtocolEnvelope(
                json.getInt("protocol_version"), json.getString("message_id"), json.getString("message_type"),
                json.getString("device_id"), json.getString("sent_at_utc"), json.optString("session_id").takeIf { it.isNotEmpty() && it != "null" },
                json.getJSONObject("payload"), authJson?.let { ProtocolAuthentication(it.getString("key_id"), it.getString("payload_sha256"), it.getString("signature")) },
            )
        }
    }
}
