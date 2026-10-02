package app.easynoty.android.capture

import android.app.Notification
import android.content.Context
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.drawable.BitmapDrawable
import android.graphics.drawable.Drawable
import android.graphics.drawable.Icon
import app.easynoty.android.core.Hashing
import app.easynoty.android.storage.AssetEntity
import java.io.ByteArrayOutputStream
import java.io.File

class AssetStore(private val context: Context) {
    companion object { const val MAX_ASSET_BYTES = 25L * 1024 * 1024 }

    fun capture(notification: Notification, packageName: String, syncImages: Boolean): List<Pair<AssetEntity, String>> {
        return buildList {
            runCatching { persistSnapshot(context.packageManager.getApplicationIcon(packageName), "app_icon", "drawable_rendered") }.getOrNull()?.let(::add)
            if (syncImages) {
                notification.getLargeIcon()?.let { runCatching { persistIcon(it, "avatar") }.getOrNull()?.let(::add) }
                (notification.extras.getParcelable(Notification.EXTRA_PICTURE) as? Bitmap)?.let {
                    runCatching { persistSnapshot(BitmapDrawable(context.resources, it), "big_picture", "bitmap_object") }.getOrNull()?.let(::add)
                }
                (notification.extras.getParcelable(Notification.EXTRA_LARGE_ICON_BIG) as? Icon)?.let {
                    runCatching { persistIcon(it, "preview_image") }.getOrNull()?.let(::add)
                }
            }
        }
    }

    private fun persistIcon(icon: Icon, role: String): Pair<AssetEntity, String>? {
        if (icon.type == Icon.TYPE_URI || icon.type == Icon.TYPE_URI_ADAPTIVE_BITMAP) {
            val uri = icon.uri
            val bytes = context.contentResolver.openInputStream(uri)?.use(::readLimited) ?: return null
            val mime = context.contentResolver.getType(uri) ?: "application/octet-stream"
            return persistBytes(bytes, mime, role, "icon_uri", "original_encoded_bytes", null, null)
        }
        return persistSnapshot(icon.loadDrawable(context) ?: return null, role, when (icon.type) {
            Icon.TYPE_RESOURCE -> "icon_resource"
            Icon.TYPE_DATA -> "icon_data"
            else -> "icon_bitmap"
        })
    }

    private fun persistSnapshot(drawable: Drawable, role: String, sourceKind: String): Pair<AssetEntity, String>? {
        val bitmap = toBitmap(drawable)
        val output = ByteArrayOutputStream()
        check(bitmap.compress(Bitmap.CompressFormat.WEBP_LOSSLESS, 100, output)) { "Bitmap encoding failed" }
        return persistBytes(output.toByteArray(), "image/webp", role, sourceKind,
            if (sourceKind == "drawable_rendered") "rendered_snapshot_from_drawable" else "lossless_snapshot_from_bitmap", bitmap.width, bitmap.height)
    }

    private fun persistBytes(bytes: ByteArray, mime: String, role: String, sourceKind: String, originalityLevel: String, width: Int?, height: Int?): Pair<AssetEntity, String>? {
        if (bytes.isEmpty()) return null
        if (bytes.size > MAX_ASSET_BYTES) return null
        val hash = Hashing.assetId(bytes)
        val dir = File(context.filesDir, "assets/${hash.substring(7, 9)}").apply { mkdirs() }
        val file = File(dir, hash.removePrefix("sha256:"))
        if (!file.exists()) file.writeBytes(bytes)
        val now = System.currentTimeMillis()
        return AssetEntity(
            hash = hash,
            mime = mime,
            byteSize = bytes.size.toLong(),
            width = width,
            height = height,
            sourceKind = sourceKind,
            originalityLevel = originalityLevel,
            localPath = file.absolutePath,
            createdAtUtc = now,
            lastSeenAtUtc = now,
        ) to role
    }

    private fun readLimited(input: java.io.InputStream): ByteArray {
        val output = ByteArrayOutputStream()
        val buffer = ByteArray(16 * 1024)
        while (true) {
            val count = input.read(buffer)
            if (count < 0) break
            output.write(buffer, 0, count)
            require(output.size().toLong() <= MAX_ASSET_BYTES) { "Asset exceeds 25 MiB" }
        }
        return output.toByteArray()
    }

    private fun toBitmap(drawable: Drawable): Bitmap {
        if (drawable is BitmapDrawable && drawable.bitmap != null) return drawable.bitmap
        val width = drawable.intrinsicWidth.coerceAtLeast(1)
        val height = drawable.intrinsicHeight.coerceAtLeast(1)
        return Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888).also { bitmap ->
            val canvas = Canvas(bitmap)
            drawable.setBounds(0, 0, width, height)
            drawable.draw(canvas)
        }
    }
}
