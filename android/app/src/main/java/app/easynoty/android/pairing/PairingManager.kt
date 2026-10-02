package app.easynoty.android.pairing

import app.easynoty.android.EasyNotyApplication
import app.easynoty.android.storage.PairedPcEntity
import app.easynoty.android.sync.FingerprintTrustManager
import app.easynoty.android.sync.ProtocolEnvelope
import app.easynoty.android.sync.ProtocolContractValidator
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeout
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import org.json.JSONArray
import org.json.JSONObject
import java.security.SecureRandom
import java.util.Base64
import java.util.concurrent.TimeUnit
import kotlin.coroutines.resume

private data class PairingAccept(val keyId: String, val secret: ByteArray, val verificationCode: String, val serverNonce: String, val pairedAtUtc: String)

class PairingManager(private val application: EasyNotyApplication) {
    suspend fun pair(rawPayload: String, backfillSevenDays: Boolean = false): Result<PairedPcEntity> = runCatching {
        val qr = PairingPayloadParser().parse(rawPayload)
        val accept = withTimeout(125_000) { performHandshake(qr) }
        val alias = application.secretStore.import(qr.pcDeviceId, accept.secret)
        val now = System.currentTimeMillis()
        PairedPcEntity(
            pcDeviceId = qr.pcDeviceId,
            pcName = qr.pcName,
            host = qr.endpoints.first().host,
            port = qr.endpoints.first().port,
            certificateSha256 = qr.certificateSha256,
            keyId = accept.keyId,
            secretAlias = alias,
            pairedAtUtc = now,
            backfillSinceUtc = if (backfillSevenDays) now - TimeUnit.DAYS.toMillis(7) else null,
        ).also {
            application.database.dao().upsertPc(it)
            if (it.backfillSinceUtc != null) application.database.dao().enqueueBackfill(it.pcDeviceId, it.backfillSinceUtc)
        }
    }

    private suspend fun performHandshake(qr: PairingPayload): PairingAccept = suspendCancellableCoroutine { continuation ->
        val trust = FingerprintTrustManager(qr.certificateSha256)
        val client = OkHttpClient.Builder().sslSocketFactory(trust.sslSocketFactory(), trust).hostnameVerifier { _, _ -> true }.connectTimeout(10, TimeUnit.SECONDS).build()
        val endpoint = qr.endpoints.first()
        val host = if (endpoint.host.contains(':')) "[${endpoint.host}]" else endpoint.host
        val request = Request.Builder().url("wss://$host:${endpoint.port}/easynoty/pair").build()
        val socket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                val nonce = Base64.getUrlEncoder().withoutPadding().encodeToString(ByteArray(16).also(SecureRandom()::nextBytes))
                val body = JSONObject()
                    .put("pairing_token", qr.pairingToken)
                    .put("android_device_id", application.deviceIdentity.id)
                    .put("android_name", android.os.Build.MODEL.take(128))
                    .put("client_nonce", nonce)
                    .put("capabilities", JSONArray(listOf("zstd", "zlib", "event_chunking", "asset_chunking")))
                webSocket.send(ProtocolEnvelope.unsigned("pairing_request", application.deviceIdentity.id, body).toJson())
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                val envelope = ProtocolEnvelope.parse(text)
                ProtocolContractValidator.validate(envelope)
                when (envelope.messageType) {
                    "hello" -> Unit
                    "pairing_accept" -> runCatching {
                        require(envelope.deviceId == qr.pcDeviceId && envelope.authentication == null && envelope.sessionId == null)
                        val body = envelope.payload
                        require(body.getString("pc_device_id") == qr.pcDeviceId)
                        val secret = Base64.getUrlDecoder().decode(body.getString("pairing_secret"))
                        require(secret.size == 32)
                        PairingAccept(body.getString("key_id"), secret, body.getString("verification_code"), body.getString("server_nonce"), body.getString("paired_at_utc"))
                    }.fold({
                        webSocket.close(1000, "paired")
                        if (continuation.isActive) continuation.resume(it)
                    }, { if (continuation.isActive) continuation.resumeWith(Result.failure(it)) })
                    "pairing_reject" -> if (continuation.isActive) continuation.resumeWith(Result.failure(IllegalStateException(envelope.payload.getString("message"))))
                }
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                if (continuation.isActive) continuation.resumeWith(Result.failure(t))
            }
        })
        continuation.invokeOnCancellation { socket.cancel(); client.dispatcher.executorService.shutdown() }
    }
}
