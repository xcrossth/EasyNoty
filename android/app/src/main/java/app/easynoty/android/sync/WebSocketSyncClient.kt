package app.easynoty.android.sync

import app.easynoty.android.EasyNotyApplication
import app.easynoty.android.core.CanonicalJson
import app.easynoty.android.core.Chunking
import app.easynoty.android.core.CompressionPolicy
import app.easynoty.android.core.Hashing
import app.easynoty.android.storage.AssetDeliveryEntity
import app.easynoty.android.storage.NotificationEventEntity
import app.easynoty.android.storage.PairedPcEntity
import app.easynoty.android.security.HmacSigner
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.suspendCancellableCoroutine
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.security.SecureRandom
import java.time.Instant
import java.util.Base64
import java.util.UUID
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.coroutines.resume

class WebSocketSyncClient(private val application: EasyNotyApplication) {
    private val dao = application.database.dao()
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val deviceId get() = application.deviceIdentity.id

    suspend fun syncOnce(pc: PairedPcEntity): Result<Unit> = runCatching {
        val pending = dao.pendingEvents(pc.pcDeviceId, System.currentTimeMillis())
        if (pending.isEmpty()) return@runCatching
        suspendCancellableCoroutine { continuation ->
            val completed = AtomicBoolean(false)
            val trust = FingerprintTrustManager(pc.certificateSha256)
            val client = OkHttpClient.Builder().sslSocketFactory(trust.sslSocketFactory(), trust)
                .hostnameVerifier { _, _ -> true }.pingInterval(20, TimeUnit.SECONDS).build()
            val host = if (pc.host.contains(':')) "[${pc.host}]" else pc.host
            val socket = client.newWebSocket(Request.Builder().url("wss://$host:${pc.port}/easynoty/sync").build(), object : WebSocketListener() {
                private var sessionId: String? = null

                override fun onOpen(webSocket: WebSocket, response: Response) {
                    val hello = JSONObject()
                        .put("role", "android").put("device_name", android.os.Build.MODEL.take(128))
                        .put("supported_protocol_versions", JSONArray(listOf(1)))
                        .put("capabilities", JSONArray(listOf("zstd", "zlib", "event_chunking", "asset_chunking")))
                    webSocket.send(ProtocolEnvelope.unsigned("hello", deviceId, hello).toJson())
                }

                override fun onMessage(webSocket: WebSocket, text: String) {
                    scope.launch {
                        runCatching {
                            val envelope = ProtocolEnvelope.parse(text)
                            require(envelope.protocolVersion == 1 && envelope.deviceId == pc.pcDeviceId)
                            if (envelope.messageType in setOf("hello", "auth_challenge", "auth_success")) {
                                ProtocolContractValidator.validate(envelope)
                            } else {
                                ProtocolContractValidator.validate(envelope, application.secretStore.get(pc.secretAlias))
                            }
                            when (envelope.messageType) {
                                "auth_challenge" -> sendAuthResponse(webSocket, envelope, pc)
                                "hello" -> require(envelope.authentication == null && envelope.sessionId == null)
                                "auth_success" -> {
                                    require(envelope.authentication == null && envelope.sessionId == null)
                                    val issuedSession = envelope.payload.getString("session_id")
                                    require(UUID.fromString(issuedSession).toString() == issuedSession)
                                    sessionId = issuedSession
                                    sendPending(webSocket, pc, issuedSession, pending.map { it.event })
                                    delay(12_000)
                                    webSocket.close(1000, "sync window complete")
                                    finish(Result.success(Unit))
                                }
                                else -> {
                                    val activeSession = requireNotNull(sessionId)
                                    require(envelope.verify(application.secretStore.get(pc.secretAlias), pc.keyId, activeSession))
                                    handleAuthenticated(webSocket, envelope, pc, activeSession)
                                }
                            }
                        }.onFailure { fail(it) }
                    }
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) = finish(Result.success(Unit))
                override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) = fail(t)

                private fun fail(error: Throwable) {
                    scope.launch {
                        val now = System.currentTimeMillis()
                        pending.forEach { item ->
                            val delayMs = (1L shl item.delivery.retryCount.coerceAtMost(10)) * 5_000L
                            dao.failEvent(item.event.eventId, pc.pcDeviceId, now + delayMs, error.javaClass.simpleName)
                        }
                    }
                    finish(Result.failure(error))
                }

                private fun finish(result: Result<Unit>) {
                    if (completed.compareAndSet(false, true) && continuation.isActive) continuation.resume(result)
                    client.dispatcher.executorService.shutdown()
                }
            })
            continuation.invokeOnCancellation { socket.cancel(); client.dispatcher.executorService.shutdown() }
        }.getOrThrow()
    }

    private fun sendAuthResponse(socket: WebSocket, challenge: ProtocolEnvelope, pc: PairedPcEntity) {
        require(challenge.authentication == null && challenge.sessionId == null)
        val challengeId = challenge.payload.getString("challenge_id")
        val serverNonce = challenge.payload.getString("server_nonce")
        val clientNonce = Base64.getUrlEncoder().withoutPadding().encodeToString(ByteArray(16).also(SecureRandom()::nextBytes))
        val proofInput = "auth-v1\n$challengeId\n$serverNonce\n$clientNonce\n$deviceId".toByteArray()
        val proof = HmacSigner.sign(application.secretStore.get(pc.secretAlias), proofInput)
        val payload = JSONObject().put("challenge_id", challengeId).put("key_id", pc.keyId).put("client_nonce", clientNonce).put("proof", proof)
        socket.send(ProtocolEnvelope.unsigned("auth_response", deviceId, payload).toJson())
    }

    private suspend fun sendPending(socket: WebSocket, pc: PairedPcEntity, sessionId: String, events: List<NotificationEventEntity>) {
        events.forEach { event ->
            dao.markEventInFlight(event.eventId, pc.pcDeviceId, System.currentTimeMillis())
            sendEvent(socket, event, pc, sessionId)
        }
    }

    private suspend fun sendEvent(socket: WebSocket, event: NotificationEventEntity, pc: PairedPcEntity, sessionId: String) {
        val payload = eventPayload(event)
        val canonical = CanonicalJson.bytes(payload.toString())
        if (canonical.size <= 2 * 1024 * 1024) {
            socket.send(envelope("notification_event", payload, pc, sessionId).toJson())
            return
        }
        val compressed = CompressionPolicy().choose(canonical, application.settings.compressionLevel, application.settings.compressionThreshold)
        val transferId = event.eventId
        val chunks = Chunking.chunks(compressed.bytes)
        val canonicalHash = Hashing.assetId(canonical)
        socket.send(envelope("event_transfer_start", JSONObject()
            .put("transfer_id", transferId).put("event_id", event.eventId)
            .put("content_type", "application/vnd.easynoty.notification-event+json").put("content_encoding", compressed.algorithm)
            .put("canonical_size", canonical.size).put("encoded_size", compressed.bytes.size).put("canonical_sha256", canonicalHash)
            .put("chunk_size", Chunking.CHUNK_SIZE).put("chunk_count", chunks.size), pc, sessionId).toJson())
        var offset = 0
        chunks.forEachIndexed { index, chunk ->
            socket.send(envelope("event_transfer_chunk", JSONObject().put("transfer_id", transferId).put("chunk_index", index)
                .put("offset", offset).put("data_b64", Base64.getEncoder().encodeToString(chunk)), pc, sessionId).toJson())
            offset += chunk.size
        }
        socket.send(envelope("event_transfer_complete", JSONObject().put("transfer_id", transferId).put("event_id", event.eventId)
            .put("encoded_size", compressed.bytes.size).put("canonical_sha256", canonicalHash), pc, sessionId).toJson())
    }

    private suspend fun handleAuthenticated(socket: WebSocket, envelope: ProtocolEnvelope, pc: PairedPcEntity, sessionId: String) {
        dao.updatePcLastSeen(pc.pcDeviceId, System.currentTimeMillis())
        val payload = envelope.payload
        when (envelope.messageType) {
            "event_ack" -> if (payload.getString("status") == "persisted") dao.acknowledgeEvent(payload.getString("event_id"), pc.pcDeviceId, System.currentTimeMillis())
            "asset_missing_request" -> {
                val hashes = payload.getJSONArray("hashes")
                for (i in 0 until hashes.length()) sendAsset(socket, hashes.getString(i), pc, sessionId)
            }
            "asset_ack" -> if (payload.getString("status") == "stored_verified") dao.acknowledgeAsset(payload.getString("asset_hash"), pc.pcDeviceId, System.currentTimeMillis())
            "ping" -> socket.send(envelope("pong", JSONObject().put("nonce", payload.getString("nonce")), pc, sessionId).toJson())
        }
    }

    private suspend fun sendAsset(socket: WebSocket, hash: String, pc: PairedPcEntity, sessionId: String) {
        val asset = dao.asset(hash) ?: return
        dao.enqueueAssetDelivery(AssetDeliveryEntity(hash, pc.pcDeviceId))
        val canonical = File(asset.localPath).readBytes()
        require(canonical.size <= 25 * 1024 * 1024 && Hashing.assetId(canonical) == hash)
        val compressed = CompressionPolicy().choose(canonical, application.settings.compressionLevel, application.settings.compressionThreshold)
        val transferId = UUID.nameUUIDFromBytes("${pc.pcDeviceId}:$hash".toByteArray()).toString()
        val inline = canonical.size < Chunking.INLINE_LIMIT
        val chunks = if (inline) emptyList() else Chunking.chunks(compressed.bytes)
        socket.send(envelope("asset_transfer_start", JSONObject()
            .put("transfer_id", transferId).put("asset_hash", hash).put("mime", asset.mime).put("content_encoding", compressed.algorithm)
            .put("canonical_size", canonical.size).put("encoded_size", compressed.bytes.size)
            .put("chunk_size", if (inline) 0 else Chunking.CHUNK_SIZE).put("chunk_count", chunks.size)
            .put("inline_data_b64", if (inline) Base64.getEncoder().encodeToString(compressed.bytes) else JSONObject.NULL), pc, sessionId).toJson())
        if (!inline) {
            var offset = 0
            chunks.forEachIndexed { index, chunk ->
                socket.send(envelope("asset_transfer_chunk", JSONObject().put("transfer_id", transferId).put("chunk_index", index)
                    .put("offset", offset).put("data_b64", Base64.getEncoder().encodeToString(chunk)), pc, sessionId).toJson())
                offset += chunk.size
            }
            socket.send(envelope("asset_transfer_complete", JSONObject().put("transfer_id", transferId).put("asset_hash", hash)
                .put("encoded_size", compressed.bytes.size), pc, sessionId).toJson())
        }
    }

    private suspend fun eventPayload(event: NotificationEventEntity): JSONObject {
        val manifests = JSONArray()
        dao.eventAssets(event.eventId).forEach { mapping ->
            dao.asset(mapping.assetHash)?.let { asset ->
                manifests.put(JSONObject().put("role", mapping.role).put("hash", asset.hash).put("mime", asset.mime)
                    .put("original_size", asset.byteSize).put("width", asset.width ?: JSONObject.NULL).put("height", asset.height ?: JSONObject.NULL)
                    .put("source_kind", asset.sourceKind).put("originality_level", asset.originalityLevel))
            }
        }
        return JSONObject()
            .put("event_id", event.eventId).put("notification_instance_id", event.notificationInstanceId).put("revision_index", event.revisionIndex)
            .put("android_notification_key", event.notificationKey.take(4096)).put("package_name", event.packageName).put("app_name", event.appName.take(512))
            .put("posted_at_utc", Instant.ofEpochMilli(event.postedAtUtc).toString()).put("captured_at_utc", Instant.ofEpochMilli(event.capturedAtUtc).toString())
            .put("raw_snapshot_schema_version", 1).put("raw_snapshot_byte_size", event.rawByteSize).put("raw_snapshot_sha256", event.rawSha256)
            .put("raw_snapshot", JSONObject(event.rawSnapshotJson)).put("parsed_result", JSONObject(event.parsedResultJson)).put("assets", manifests)
            .put("style", event.style).put("is_ongoing", event.isOngoing).put("is_group_summary", event.isGroupSummary)
            .put("group_key", event.groupKey?.take(4096) ?: JSONObject.NULL).put("sort_key", event.sortKey?.take(4096) ?: JSONObject.NULL)
            .put("minimal_capture_error", event.captureError ?: JSONObject.NULL)
    }

    private fun envelope(type: String, payload: JSONObject, pc: PairedPcEntity, sessionId: String, messageId: String = UUID.randomUUID().toString()) =
        ProtocolEnvelope.signed(type, deviceId, sessionId, payload, pc.keyId, pc.secretAlias, application.secretStore, messageId)
}
