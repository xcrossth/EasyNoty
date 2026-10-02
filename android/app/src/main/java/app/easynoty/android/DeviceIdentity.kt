package app.easynoty.android

import android.content.Context
import java.util.UUID

class DeviceIdentity(context: Context) {
    private val preferences = context.getSharedPreferences("identity", Context.MODE_PRIVATE)
    val id: String
        get() = preferences.getString("device_uuid", null) ?: UUID.randomUUID().toString().lowercase().also {
            preferences.edit().putString("device_uuid", it).commit()
        }
}
