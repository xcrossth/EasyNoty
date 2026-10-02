package app.easynoty.android.sync

import app.easynoty.android.core.CanonicalJson
import app.easynoty.android.core.Hashing
import org.json.JSONObject

object ProtocolContractValidator {
    private val uuid = Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")
    private val utc = Regex("^[0-9]{4}-(0[1-9]|1[0-2])-([0-2][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,9})?Z$")
    private val sha = Regex("^sha256:[0-9a-f]{64}$")
    private val messages = setOf("hello", "pairing_request", "pairing_accept", "pairing_reject", "auth_challenge", "auth_response", "auth_success", "notification_event", "event_transfer_start", "event_transfer_chunk", "event_transfer_complete", "event_ack", "asset_missing_request", "asset_transfer_start", "asset_transfer_chunk", "asset_transfer_complete", "asset_ack", "ping", "pong", "error")
    private val preSession = setOf("hello", "pairing_request", "pairing_accept", "pairing_reject", "auth_challenge", "auth_response", "auth_success")

    fun validate(envelope: ProtocolEnvelope, secret: ByteArray? = null) {
        require(envelope.protocolVersion == 1 && envelope.messageType in messages)
        require(uuid.matches(envelope.messageId) && uuid.matches(envelope.deviceId) && utc.matches(envelope.sentAtUtc))
        if (envelope.messageType in preSession) {
            require(envelope.sessionId == null && envelope.authentication == null)
        } else {
            require(envelope.sessionId?.matches(uuid) == true)
            val auth = requireNotNull(envelope.authentication)
            require(uuid.matches(auth.keyId) && sha.matches(auth.payloadSha256) && auth.signature.matches(Regex("[0-9a-f]{64}")))
            val calculated = Hashing.assetId(CanonicalJson.bytes(envelope.payload.toString()))
            require(calculated == auth.payloadSha256)
            if (secret != null) require(envelope.verify(secret, auth.keyId, envelope.sessionId))
        }
        validatePayload(envelope.messageType, envelope.payload)
    }

    private fun validatePayload(type: String, payload: JSONObject) {
        when (type) {
            "hello" -> exact(payload, setOf("role", "device_name", "supported_protocol_versions", "capabilities"))
            "pairing_request" -> exact(payload, setOf("pairing_token", "android_device_id", "android_name", "client_nonce", "capabilities"))
            "pairing_accept" -> exact(payload, setOf("pc_device_id", "pc_name", "key_id", "pairing_secret", "verification_code", "server_nonce", "paired_at_utc"))
            "pairing_reject" -> exact(payload, setOf("code", "message", "retryable"))
            "auth_challenge" -> exact(payload, setOf("challenge_id", "server_nonce", "expires_at_utc"))
            "auth_response" -> exact(payload, setOf("challenge_id", "key_id", "client_nonce", "proof"))
            "auth_success" -> exact(payload, setOf("session_id", "authenticated_at_utc", "expires_at_utc"))
            "notification_event" -> validateNotification(payload)
            "event_transfer_start" -> {
                exact(payload, setOf("transfer_id", "event_id", "content_type", "content_encoding", "canonical_size", "encoded_size", "canonical_sha256", "chunk_size", "chunk_count"))
                require(payload.getInt("chunk_size") in 1..65536 && payload.getInt("chunk_count") >= 1)
            }
            "event_transfer_chunk", "asset_transfer_chunk" -> {
                exact(payload, setOf("transfer_id", "chunk_index", "offset", "data_b64"))
                require(payload.getInt("chunk_index") >= 0 && payload.getLong("offset") >= 0)
            }
            "event_transfer_complete" -> exact(payload, setOf("transfer_id", "event_id", "encoded_size", "canonical_sha256"))
            "event_ack" -> { exact(payload, setOf("event_id", "status", "persisted_at_utc", "duplicate")); require(payload.getString("status") == "persisted") }
            "asset_missing_request" -> exact(payload, setOf("event_id", "hashes"))
            "asset_transfer_start" -> {
                exact(payload, setOf("transfer_id", "asset_hash", "mime", "content_encoding", "canonical_size", "encoded_size", "chunk_size", "chunk_count", "inline_data_b64"))
                require(payload.getLong("canonical_size") in 1..26_214_400 && payload.getLong("encoded_size") in 1..26_214_400)
                require(payload.getInt("chunk_size") in 0..65536 && payload.getInt("chunk_count") in 0..400)
            }
            "asset_transfer_complete" -> exact(payload, setOf("transfer_id", "asset_hash", "encoded_size"))
            "asset_ack" -> { exact(payload, setOf("asset_hash", "status", "verified_at_utc", "duplicate")); require(payload.getString("status") == "stored_verified") }
            "ping", "pong" -> exact(payload, setOf("nonce"))
            "error" -> exact(payload, setOf("code", "message", "retryable", "related_message_id"), setOf("details"))
        }
    }

    private fun validateNotification(payload: JSONObject) {
        exact(payload, setOf("event_id", "notification_instance_id", "revision_index", "android_notification_key", "package_name", "app_name", "posted_at_utc", "captured_at_utc", "raw_snapshot_schema_version", "raw_snapshot_byte_size", "raw_snapshot_sha256", "raw_snapshot", "parsed_result", "assets", "style", "is_ongoing", "is_group_summary"), setOf("group_key", "sort_key", "minimal_capture_error"))
        val raw = CanonicalJson.bytes(payload.getJSONObject("raw_snapshot").toString())
        require(payload.getLong("raw_snapshot_byte_size") == raw.size.toLong())
        require(payload.getString("raw_snapshot_sha256") == Hashing.assetId(raw))
        val parsed = payload.getJSONObject("parsed_result")
        exact(parsed, setOf("parser_name", "parser_version", "title", "body", "sender_name", "conversation_title", "confidence"), setOf("preview_text"))
        val assets = payload.getJSONArray("assets")
        require(assets.length() <= 64)
        for (i in 0 until assets.length()) {
            val asset = assets.getJSONObject(i)
            exact(asset, setOf("role", "hash", "mime", "original_size", "source_kind", "originality_level"), setOf("width", "height"))
            require(asset.getLong("original_size") in 1..26_214_400)
        }
    }

    private fun exact(json: JSONObject, required: Set<String>, optional: Set<String> = emptySet()) {
        val keys = json.keys().asSequence().toSet()
        require(keys.containsAll(required) && keys.all { it in required || it in optional })
    }
}
