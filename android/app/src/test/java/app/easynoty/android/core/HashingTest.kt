package app.easynoty.android.core

import org.junit.Assert.assertEquals
import org.junit.Test

class HashingTest {
    @Test fun `sha256 uses canonical bytes`() {
        assertEquals("sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hashing.assetId("abc".toByteArray()))
    }
}
