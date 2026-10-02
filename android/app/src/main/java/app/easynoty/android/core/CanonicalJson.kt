package app.easynoty.android.core

import org.erdtman.jcs.JsonCanonicalizer

/** RFC 8785 JSON Canonicalization Scheme. */
object CanonicalJson {
    fun bytes(json: String): ByteArray = JsonCanonicalizer(json).encodedUTF8
    fun string(json: String): String = bytes(json).toString(Charsets.UTF_8)
}
