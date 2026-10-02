package app.easynoty.android.sync

import app.easynoty.android.core.Hashing
import java.security.cert.CertificateException
import java.security.cert.X509Certificate
import javax.net.ssl.SSLContext
import javax.net.ssl.X509TrustManager

class FingerprintTrustManager(private val expectedSha256: String) : X509TrustManager {
    override fun checkClientTrusted(chain: Array<out X509Certificate>?, authType: String?) = Unit
    override fun checkServerTrusted(chain: Array<out X509Certificate>?, authType: String?) {
        val leaf = chain?.firstOrNull() ?: throw CertificateException("Server did not provide a certificate")
        val actual = Hashing.sha256Hex(leaf.encoded)
        if (!actual.equals(expectedSha256.removePrefix("sha256:").replace(":", ""), ignoreCase = true)) {
            throw CertificateException("Pinned certificate fingerprint mismatch")
        }
        leaf.checkValidity()
    }
    override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()

    fun sslSocketFactory() = SSLContext.getInstance("TLS").apply { init(null, arrayOf(this@FingerprintTrustManager), null) }.socketFactory
}
