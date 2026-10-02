package app.easynoty.android.security

import app.easynoty.android.core.Hashing
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

object HmacSigner {
    fun canonical(protocolVersion: Int, messageId: String, messageType: String, deviceId: String, sentAtUtc: String, sessionId: String, payloadSha256: String): ByteArray =
        listOf(protocolVersion.toString(), messageId, messageType, deviceId, sentAtUtc, sessionId, payloadSha256)
            .joinToString("\n").toByteArray(Charsets.UTF_8)

    fun sign(secret: ByteArray, canonical: ByteArray): String {
        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(secret, "HmacSHA256"))
        return mac.doFinal(canonical).joinToString("") { "%02x".format(it) }
    }

    fun verify(secret: ByteArray, canonical: ByteArray, signature: String): Boolean = runCatching {
        val expected = sign(secret, canonical).chunked(2).map { it.toInt(16).toByte() }.toByteArray()
        val actual = signature.chunked(2).map { it.toInt(16).toByte() }.toByteArray()
        java.security.MessageDigest.isEqual(expected, actual)
    }.getOrDefault(false)
}
