package app.easynoty.android.core

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Test

class CompressionPolicyTest {
    private class Fake(override val algorithm: String, private val output: ByteArray, private val fail: Boolean = false) : Compressor {
        override fun compress(input: ByteArray, level: Int): ByteArray = if (fail) error("no codec") else output
    }

    @Test fun `keeps original when saving is below threshold`() {
        val input = ByteArray(100)
        val result = CompressionPolicy(Fake("zstd", ByteArray(95)), Fake("zlib", ByteArray(50))).choose(input, minimumSavingFraction = .10)
        assertEquals("none", result.algorithm)
        assertArrayEquals(input, result.bytes)
    }

    @Test fun `falls back to zlib when primary fails`() {
        val result = CompressionPolicy(Fake("zstd", byteArrayOf(), true), Fake("zlib", ByteArray(50))).choose(ByteArray(100))
        assertEquals("zlib", result.algorithm)
    }

    @Test fun `chunks at 64 KiB boundary`() {
        assertEquals(2, Chunking.chunks(ByteArray(Chunking.CHUNK_SIZE + 1)).size)
    }
}
