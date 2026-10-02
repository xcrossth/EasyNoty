package app.easynoty.android.pairing

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo

class LanDiscovery(context: Context) {
    private val manager = context.getSystemService(NsdManager::class.java)
    fun start(onFound: (NsdServiceInfo) -> Unit): NsdManager.DiscoveryListener {
        val listener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(serviceType: String) = Unit
            override fun onServiceFound(serviceInfo: NsdServiceInfo) { onFound(serviceInfo) }
            override fun onServiceLost(serviceInfo: NsdServiceInfo) = Unit
            override fun onDiscoveryStopped(serviceType: String) = Unit
            override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) = Unit
            override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) = Unit
        }
        manager.discoverServices("_easynoty._tcp.", NsdManager.PROTOCOL_DNS_SD, listener)
        return listener
    }
    fun stop(listener: NsdManager.DiscoveryListener) = manager.stopServiceDiscovery(listener)
}
