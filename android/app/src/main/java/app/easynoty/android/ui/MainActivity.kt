package app.easynoty.android.ui

import android.content.Intent
import android.Manifest
import android.content.pm.PackageManager
import android.os.Bundle
import android.provider.Settings
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Apps
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Checkbox
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.lifecycle.viewmodel.compose.viewModel
import app.easynoty.android.R
import app.easynoty.android.SyncMode

class MainActivity : ComponentActivity() {
    private val mainViewModel by viewModels<MainViewModel>()
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { MaterialTheme { EasyNotyScreen(mainViewModel) { startActivity(Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS)) } } }
    }
}

private enum class Page { STATUS, APPS, PAIR, SETTINGS }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EasyNotyScreen(vm: MainViewModel = viewModel(), openNotificationSettings: () -> Unit) {
    var page by remember { mutableStateOf(Page.STATUS) }
    val title = when (page) {
        Page.STATUS -> R.string.home_title
        Page.APPS -> R.string.allowlist_title
        Page.PAIR -> R.string.pair_title
        Page.SETTINGS -> R.string.settings_title
    }
    Scaffold(
        topBar = { TopAppBar(title = { Text(stringResource(title)) }) },
        bottomBar = {
            NavigationBar {
                NavigationBarItem(page == Page.STATUS, { page = Page.STATUS }, { Icon(Icons.Default.Home, null) }, label = { Text("Status") })
                NavigationBarItem(page == Page.APPS, { page = Page.APPS }, { Icon(Icons.Default.Apps, null) }, label = { Text("Apps") })
                NavigationBarItem(page == Page.PAIR, { page = Page.PAIR }, { Icon(Icons.Default.Add, null) }, label = { Text("Pair") })
                NavigationBarItem(page == Page.SETTINGS, { page = Page.SETTINGS }, { Icon(Icons.Default.Settings, null) }, label = { Text("Settings") })
            }
        },
    ) { padding ->
        when (page) {
            Page.STATUS -> StatusPage(vm, openNotificationSettings, Modifier.padding(padding))
            Page.APPS -> AllowlistPage(vm, Modifier.padding(padding))
            Page.PAIR -> PairPage(vm, Modifier.padding(padding))
            Page.SETTINGS -> SettingsPage(vm, Modifier.padding(padding))
        }
    }
}

@Composable
private fun StatusPage(vm: MainViewModel, openNotificationSettings: () -> Unit, modifier: Modifier) {
    val state by vm.status.collectAsState()
    val access = vm.notificationAccessEnabled()
    LazyColumn(modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        item {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(if (access) stringResource(R.string.notification_access_on) else stringResource(R.string.notification_access_off), style = MaterialTheme.typography.titleMedium)
                    if (!access) Button(openNotificationSettings) { Text(stringResource(R.string.open_notification_access)) }
                    Text(stringResource(R.string.pending_summary, state.pendingEvents, state.pendingAssets))
                    Text("Mode: ${vm.syncMode().name.replace('_', ' ')}")
                }
            }
        }
        item { Text(stringResource(R.string.privacy_notice), style = MaterialTheme.typography.bodySmall) }
        if (state.pcs.isEmpty()) item { Text(stringResource(R.string.no_pcs)) }
        items(state.pcs, key = { it.pcDeviceId }) { pc ->
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Computer, null)
                        Text(pc.pcName, Modifier.padding(start = 8.dp).weight(1f), style = MaterialTheme.typography.titleMedium)
                        Switch(!pc.isPaused, { vm.pause(pc, !it) })
                    }
                    Text(if (pc.isPaused) "Paused — backlog is retained" else "Ready on ${pc.host}:${pc.port}")
                    Button({ vm.revoke(pc) }) { Text("Revoke") }
                }
            }
        }
    }
}

@Composable
private fun AllowlistPage(vm: MainViewModel, modifier: Modifier) {
    val persisted by vm.allowlist.collectAsState()
    val installed = remember(persisted) { vm.installedApps() }
    LazyColumn(modifier.fillMaxSize().padding(horizontal = 16.dp)) {
        item { Text("Choose apps explicitly. New apps remain off by default.", Modifier.padding(vertical = 12.dp)) }
        items(installed, key = { it.packageName }) { item ->
            val allowed = persisted.firstOrNull { it.packageName == item.packageName }?.isAllowed ?: false
            Row(Modifier.fillMaxWidth().padding(vertical = 8.dp), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) { Text(item.label); Text(item.packageName, style = MaterialTheme.typography.bodySmall) }
                Switch(allowed, { vm.setAllowed(item, it) })
            }
        }
    }
}

@Composable
private fun PairPage(vm: MainViewModel, modifier: Modifier) {
    var raw by remember { mutableStateOf("") }
    var backfill by remember { mutableStateOf(false) }
    var scanning by remember { mutableStateOf(false) }
    var cameraMessage by remember { mutableStateOf<String?>(null) }
    val message by vm.pairingMessage.collectAsState()
    val context = androidx.compose.ui.platform.LocalContext.current
    val permissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (granted) scanning = true else cameraMessage = "Camera permission is required to scan a pairing QR code."
    }
    Column(modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Scan the QR code or paste its payload below. The TLS certificate fingerprint is pinned before pairing data is sent.")
        if (scanning) {
            QrCameraScanner(
                modifier = Modifier.fillMaxWidth().height(240.dp),
                onScanned = { payload -> raw = payload; scanning = false; cameraMessage = "QR code scanned. Review and pair when ready." },
                onError = { error -> scanning = false; cameraMessage = error },
            )
            Button({ scanning = false }) { Text("Cancel scan") }
        } else {
            Button({
                if (ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) scanning = true
                else permissionLauncher.launch(Manifest.permission.CAMERA)
            }) { Text("Scan QR code") }
        }
        OutlinedTextField(raw, { raw = it }, Modifier.fillMaxWidth().height(180.dp), label = { Text(stringResource(R.string.pairing_hint)) })
        Row(verticalAlignment = Alignment.CenterVertically) {
            Checkbox(backfill, { backfill = it })
            Text("Backfill the last 7 days (default: start now)")
        }
        Button({ vm.pair(raw, backfill) }, enabled = raw.isNotBlank()) { Text(stringResource(R.string.pair)) }
        cameraMessage?.let { Text(it) }
        message?.let { Text(it) }
    }
}

@Composable
private fun SettingsPage(vm: MainViewModel, modifier: Modifier) {
    var selected by remember { mutableStateOf(vm.syncMode()) }
    var images by remember { mutableStateOf(vm.syncImages()) }
    var wifi by remember { mutableStateOf(vm.wifiOnly()) }
    Column(modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Sync mode", style = MaterialTheme.typography.titleMedium)
        SyncMode.entries.forEach { mode ->
            Row(verticalAlignment = Alignment.CenterVertically) {
                RadioButton(selected == mode, { selected = mode; vm.setSyncMode(mode) })
                Text(mode.name.replace('_', ' '))
            }
        }
        Spacer(Modifier.height(8.dp))
        SettingSwitch("Sync notification images", images) { images = it; vm.setSyncImages(it) }
        SettingSwitch("Sync only on unmetered Wi-Fi", wifi) { wifi = it; vm.setWifiOnly(it) }
        Text("App icons are always synchronized. Assets larger than 25 MiB are rejected.", style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun SettingSwitch(label: String, checked: Boolean, onChange: (Boolean) -> Unit) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Text(label, Modifier.weight(1f))
        Switch(checked, onChange)
    }
}
