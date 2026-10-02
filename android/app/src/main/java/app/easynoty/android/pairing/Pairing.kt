package app.easynoty.android.pairing

import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import java.util.UUID

data class PairingEndpoint(val host: String, val port: Int)

data class PairingPayload(
    val protocolVersion: Int,
    val pcDeviceId: String,
    val pcName: String,
    val endpoints: List<PairingEndpoint>,
    val certificateSha256: String,
    val pairingToken: String,
    val expiresAtUtc: Instant,
)

class PairingPayloadParser(private val now: () -> Instant = Instant::now) {
    fun parse(raw: String): PairingPayload {
        val json = JSONObject(raw)
        require(json.keys().asSequence().toSet() == setOf("protocol_version", "pc_device_id", "pc_name", "endpoints", "certificate_sha256", "pairing_token", "expires_at_utc")) { "Unexpected pairing field" }
        require(json.getInt("protocol_version") == 1) { "Unsupported protocol version" }
        val expires = Instant.parse(json.getString("expires_at_utc"))
        require(expires.isAfter(now())) { "Pairing code has expired" }
        require(json.getString("expires_at_utc").endsWith('Z')) { "Pairing expiry must be UTC" }
        require(json.getString("pc_device_id") == UUID.fromString(json.getString("pc_device_id")).toString()) { "Invalid PC UUID" }
        val endpointArray = json.getJSONArray("endpoints")
        val endpoints = buildList {
            for (i in 0 until endpointArray.length()) {
                val endpoint = endpointArray.getJSONObject(i)
                require(endpoint.keys().asSequence().toSet() == setOf("host", "port")) { "Unexpected endpoint field" }
                val host = endpoint.getString("host")
                val port = endpoint.getInt("port")
                require(host.isNotBlank() && host.length <= 255 && port in 1..65535) { "Invalid endpoint" }
                add(PairingEndpoint(host, port))
            }
        }
        require(endpoints.isNotEmpty()) { "No valid LAN endpoint" }
        val fingerprint = json.getString("certificate_sha256")
        require(fingerprint.matches(Regex("sha256:[0-9a-f]{64}"))) { "Invalid certificate fingerprint" }
        val token = json.getString("pairing_token")
        require(token.matches(Regex("[A-Za-z0-9_-]{32,128}"))) { "Invalid one-time token" }
        return PairingPayload(
            1,
            json.getString("pc_device_id"),
            json.getString("pc_name"),
            endpoints,
            fingerprint,
            token,
            expires,
        )
    }
}

interface QrScanner {
    suspend fun scan(): Result<String>
}

class ManualQrScanner(private val value: () -> String) : QrScanner {
    override suspend fun scan(): Result<String> = runCatching(value)
}
