package app.easynoty.android.security

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyStore
import java.security.SecureRandom
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.Mac
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

/** Keeps exportable pair secrets encrypted by a non-exportable Android Keystore key. */
class PairSecretStore(context: Context) {
    private val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
    private val preferences = context.getSharedPreferences("pair_secrets", Context.MODE_PRIVATE)

    fun alias(pcDeviceId: String) = "easynoty-pair-${pcDeviceId.filter { it.isLetterOrDigit() || it == '-' }}"

    fun import(pcDeviceId: String, secret: ByteArray): String {
        require(secret.size == 32) { "Pairing secret must be 32 bytes" }
        val alias = alias(pcDeviceId)
        ensureWrappingKey(alias)
        preferences.edit().putString(alias, encrypt(alias, secret)).apply()
        return alias
    }

    fun get(alias: String): ByteArray = decrypt(alias, requireNotNull(preferences.getString(alias, null)))

    fun sign(alias: String, canonical: ByteArray): String {
        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(get(alias), "HmacSHA256"))
        return mac.doFinal(canonical).joinToString("") { "%02x".format(it) }
    }

    fun delete(alias: String) {
        preferences.edit().remove(alias).apply()
        if (keyStore.containsAlias(alias)) keyStore.deleteEntry(alias)
    }

    private fun ensureWrappingKey(alias: String) {
        if (keyStore.containsAlias(alias)) return
        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        generator.init(
            KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build(),
        )
        generator.generateKey()
    }

    private fun encrypt(alias: String, bytes: ByteArray): String {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, keyStore.getKey(alias, null) as SecretKey)
        return Base64.getEncoder().encodeToString(cipher.iv + cipher.doFinal(bytes))
    }

    private fun decrypt(alias: String, value: String): ByteArray {
        val packed = Base64.getDecoder().decode(value)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, keyStore.getKey(alias, null) as SecretKey, GCMParameterSpec(128, packed.copyOfRange(0, 12)))
        return cipher.doFinal(packed.copyOfRange(12, packed.size))
    }
}
