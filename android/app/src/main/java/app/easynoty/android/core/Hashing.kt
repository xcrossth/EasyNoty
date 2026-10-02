package app.easynoty.android.core

import java.security.MessageDigest

object Hashing {
    fun sha256(bytes: ByteArray): ByteArray = MessageDigest.getInstance("SHA-256").digest(bytes)
    fun sha256Hex(bytes: ByteArray): String = sha256(bytes).joinToString("") { "%02x".format(it) }
    fun assetId(bytes: ByteArray): String = "sha256:${sha256Hex(bytes)}"
}
