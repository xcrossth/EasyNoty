package app.easynoty.android.sync

import app.easynoty.android.pairing.PairingPayloadParser
import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import org.junit.Test
import java.io.File
import java.time.Instant
import java.util.Base64

class ProtocolFixtureTest {
    private val secret = Base64.getUrlDecoder().decode("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8")

    @Test fun `all shared valid fixtures are accepted by Kotlin contract`() {
        val files = resourceFiles("fixtures/valid")
        assertEquals(9, files.size)
        files.forEach { file ->
            val text = file.readText()
            if (file.name == "pairing-qr.json") {
                PairingPayloadParser { Instant.parse("2026-07-05T06:00:00Z") }.parse(text)
            } else {
                ProtocolContractValidator.validate(ProtocolEnvelope.parse(text), secret)
            }
        }
    }

    @Test fun `all shared invalid fixtures are rejected by Kotlin contract`() {
        val files = resourceFiles("fixtures/invalid")
        assertEquals(9, files.size)
        files.forEach { file ->
            val accepted = runCatching {
                val text = file.readText()
                if (file.name.startsWith("pairing-qr")) PairingPayloadParser { Instant.parse("2026-07-05T06:00:00Z") }.parse(text)
                else ProtocolContractValidator.validate(ProtocolEnvelope.parse(text), secret)
            }.isSuccess
            if (accepted) fail("Invalid fixture was accepted: ${file.name}")
        }
    }

    private fun resourceFiles(path: String): List<File> {
        val url = requireNotNull(javaClass.classLoader?.getResource(path))
        return File(url.toURI()).listFiles { file -> file.extension == "json" }!!.sortedBy { it.name }
    }
}
