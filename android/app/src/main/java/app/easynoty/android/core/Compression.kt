package app.easynoty.android.core

import com.github.luben.zstd.Zstd
import java.io.ByteArrayOutputStream
import java.util.zip.Deflater

data class CompressedPayload(val algorithm: String, val bytes: ByteArray, val originalSize: Int)

interface Compressor {
    val algorithm: String
    fun compress(input: ByteArray, level: Int): ByteArray
}

object ZstdCompressor : Compressor {
    override val algorithm = "zstd"
    override fun compress(input: ByteArray, level: Int): ByteArray = Zstd.compress(input, level)
}

object ZlibCompressor : Compressor {
    override val algorithm = "zlib"
    override fun compress(input: ByteArray, level: Int): ByteArray {
        val deflater = Deflater(level.coerceIn(0, 9))
        return try {
            deflater.setInput(input)
            deflater.finish()
            val output = ByteArrayOutputStream(input.size)
            val buffer = ByteArray(8 * 1024)
            while (!deflater.finished()) output.write(buffer, 0, deflater.deflate(buffer))
            output.toByteArray()
        } finally {
            deflater.end()
        }
    }
}

class CompressionPolicy(
    private val primary: Compressor = ZstdCompressor,
    private val fallback: Compressor = ZlibCompressor,
) {
    fun choose(input: ByteArray, level: Int = 9, minimumSavingFraction: Double = 0.10): CompressedPayload {
        if (input.isEmpty()) return CompressedPayload("none", input, 0)
        val candidate = runCatching { primary.compress(input, level) }
            .map { CompressedPayload(primary.algorithm, it, input.size) }
            .getOrElse { CompressedPayload(fallback.algorithm, fallback.compress(input, level), input.size) }
        return if (candidate.bytes.size <= input.size * (1.0 - minimumSavingFraction)) candidate
        else CompressedPayload("none", input, input.size)
    }
}

object Chunking {
    const val CHUNK_SIZE = 64 * 1024
    const val INLINE_LIMIT = 64 * 1024
    fun chunks(bytes: ByteArray): List<ByteArray> =
        if (bytes.size < INLINE_LIMIT) listOf(bytes) else bytes.asList().chunked(CHUNK_SIZE).map { it.toByteArray() }
}
