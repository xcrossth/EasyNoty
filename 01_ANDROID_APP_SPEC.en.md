# Android App Spec — Notification Bridge Android

[ภาษาไทย](01_ANDROID_APP_SPEC.md) | [Documentation index](README.en.md)

This is the original design draft, not a completion report. Later decisions in the [accepted plan](03_EASYNOTY_IMPLEMENTATION_PLAN.en.md) and [Protocol v1](protocol/README.en.md) supersede draft alternatives: assets are capped at 25 MiB, QR uses endpoint objects and a certificate fingerprint, and v1 framing uses base64 JSON. See [current status](logs/ACCEPTANCE_STATUS.en.md).

Version: 0.1 MVP Draft
Date: 2026-07-03
Target: Android 11+ / Kotlin

---

## 1. Purpose

The Android app captures notifications from selected apps and syncs them to a paired Windows desktop app.

It must:

- Request Notification Access.
- Let the user choose which apps are allowed to sync.
- Capture raw notification snapshots.
- Capture original assets from notifications.
- Store notification events/assets in a local queue/cache.
- Connect to Windows over LAN WebSocket.
- Send notification events with asset hash manifests.
- Upload only assets missing on Windows.
- Support Balanced, Realtime, and Battery Saver sync modes.

The Android app is not the primary archive. Windows is the primary archive.

---

## 2. Technology Stack

| Area | Choice |
|---|---|
| Language | Kotlin |
| Min Android | Android 11 / API 30 |
| UI | Jetpack Compose preferred |
| Notification capture | `NotificationListenerService` |
| Local DB | Room/SQLite |
| Local files | App-specific storage |
| Network | WebSocket client |
| QR scan | CameraX / ML Kit / ZXing-style scanner |
| Compression primary | Zstd JNI |
| Compression fallback | zlib / Java Deflater |
| Hash | SHA-256 |
| Image snapshot default | WebP lossless |
| Other snapshot formats | PNG, WebP |

---

## 3. Android Permissions and Settings

### 3.1 Required User-Enabled Access

The app requires Android Notification Access.

Setup flow:
1. Explain why Notification Access is required.
2. Provide button to open notification access settings.
3. Detect whether listener is enabled.
4. Show status on home screen.

### 3.2 Optional Recommended Settings

For more reliable realtime sync:
- Allow unrestricted battery/background behavior.
- Allow background data.
- Enable auto-start on OEM devices if needed.
- Realtime mode may use foreground service.

Do not force these settings in MVP. Present them as recommendations.

---

## 4. App Screens

### 4.1 Home / Status

Show:
- Notification Access status.
- Paired Windows PC list.
- Current sync mode.
- Current connection status.
- Pending notifications count.
- Pending assets count.
- Last sync time.

### 4.2 App Allowlist

Purpose:
- Choose which Android apps are allowed to sync to Windows.

Display:
- App icon.
- App name.
- Package name.
- Toggle allowed/not allowed.

Default:
- No app allowed OR ask during first setup.
- For personal-use MVP, it is acceptable to start with manual allowlist.

### 4.3 Pair Windows PC

Flow:
1. User clicks Pair.
2. Camera opens QR scanner.
3. User scans QR from Windows.
4. App validates payload.
5. App connects to Windows.
6. Pairing handshake completes.
7. PC appears in paired list.

### 4.4 Paired Windows PCs

For each PC:
- PC name.
- Connection status.
- Last seen.
- Pause sync toggle.
- Remove/revoke pairing.

### 4.5 Sync Mode

Options:
- Balanced (default)
- Realtime
- Battery Saver

### 4.6 Asset / Compression Settings

Settings:
- Bitmap snapshot format:
  - PNG
  - WebP
  - WebP lossless (default)
- Compression algorithm:
  - Zstd primary, zlib fallback
- Zstd level:
  - 6
  - 9 (default)
  - 12
- Compression threshold:
  - 5%
  - 10% (default)
  - 15%
  - 20%
- Sync images:
  - On/off
- Sync only on Wi-Fi:
  - Recommended default: On
- Max single asset size:
  - MVP may default to unlimited or configurable.

---

## 5. Core Android Services / Components

### 5.1 Notification Listener

Component:
```text
NotificationBridgeListenerService : NotificationListenerService
```

Responsibilities:
- Receive `onNotificationPosted`.
- Ignore notifications from non-allowlisted apps.
- Capture raw snapshot immediately.
- Extract assets immediately.
- Store event to local DB.
- Store assets to local cache.
- Enqueue event for sync.
- Trigger connection manager.

Important:
- Capture as much as possible at post time.
- Do not wait until notification is removed.
- Do not rely on notification still existing later.

### 5.2 Connection Manager

Responsibilities:
- Maintain WebSocket connection when appropriate.
- Handle reconnect.
- Respect sync mode.
- Send queued events.
- Send queued assets.
- Receive ACKs.
- Receive missing asset requests.
- Handle backpressure.

### 5.3 Queue Manager

Queues:
- Notification event queue.
- Asset upload queue.
- Retry queue.

Each item tracks:
- Status.
- Retry count.
- Last attempt.
- Last error.
- Target Windows device.

### 5.4 Asset Store

Responsibilities:
- Store original/canonical bytes.
- Store metadata.
- Compute and verify SHA-256.
- Provide asset by hash when Windows requests it.
- Keep assets after sync for a configurable period.
- Delete local cache after safe retention.

Windows is the primary archive, Android cache is temporary.

---

## 6. Notification Capture

### 6.1 Event Flow

```text
onNotificationPosted(sbn)
↓
Check allowlist
↓
Build raw snapshot
↓
Build parsed result v1
↓
Extract assets
↓
Store event + assets locally
↓
Enqueue event for sync
↓
Try send if connected
```

### 6.2 Raw Snapshot

Raw snapshot should be JSON-friendly.

Must not attempt to serialize Android `Notification`/`StatusBarNotification` objects directly.

Capture fields like:

```text
capture_schema_version
android_version
device_id
package_name
app_name
notification_key
notification_id
tag
group_key
post_time
when
category
channel_id
importance
flags
is_ongoing
is_clearable
extras_raw
people_summary
actions_summary
style/template summary
asset_refs
```

### 6.3 Extras Capture

Capture common extras:
- `android.title`
- `android.text`
- `android.subText`
- `android.summaryText`
- `android.bigText`
- `android.textLines`
- `android.conversationTitle`
- `android.messages`
- `android.messages.historic`
- people/person data where available
- category/channel info

Unknown extras:
- Convert to safe JSON string/primitive if possible.
- If object is not serializable, store type name and debug summary.

### 6.4 Parsed Result v1

Android can send a first parsed result for immediate display, but Windows remains free to re-parse.

Generic parser:
- app name
- title
- text/body
- sender name if obvious
- conversation title if obvious
- preview text
- asset hash references

Confidence:
- Use a rough numeric confidence from 0 to 1.

---

## 7. Asset Capture

### 7.1 Asset Source Types

Potential source kinds:

```text
encoded_bytes
content_uri
bitmap_object
icon_bitmap
icon_data
icon_uri
icon_resource
drawable_bitmap
drawable_rendered
unknown_rendered_snapshot
```

### 7.2 Originality Levels

```text
original_encoded_bytes
lossless_snapshot_from_bitmap
rendered_snapshot_from_drawable
unknown_snapshot
```

### 7.3 Rules

If encoded original bytes are available:
- Store bytes unchanged.
- Compute hash from those bytes.

If only `Bitmap`/`Icon`/`Drawable` is available:
- Render/extract bitmap.
- Encode using configured snapshot format.
- Default: WebP lossless.
- Compute hash from encoded snapshot bytes.

Do not:
- Resize.
- Crop.
- Lossy-convert.
- Base64 embed images in JSON.

### 7.4 Asset Manifest

Each notification event includes asset manifests:

```json
{
  "role": "avatar",
  "hash": "sha256:...",
  "mime": "image/webp",
  "original_size": 91234,
  "width": 512,
  "height": 512,
  "source_kind": "icon_bitmap",
  "originality_level": "lossless_snapshot_from_bitmap"
}
```

---

## 8. Compression

### 8.1 Algorithms

Primary:
- Zstd

Fallback:
- zlib

### 8.2 Settings

Zstd levels:
- 6
- 9 default
- 12

Threshold:
- 5%
- 10% default
- 15%
- 20%

### 8.3 Rule

For each file/asset:

```text
original_bytes = canonical asset bytes
original_hash = SHA-256(original_bytes)

compressed = Zstd(original_bytes, level)

if compression fails:
    compressed = zlib(original_bytes)

if compressed_size <= original_size * (1 - threshold):
    send compressed
else:
    send original
```

### 8.4 Job Queue

Compression should run in a background job queue.

Do not block notification capture path.

Priority:
1. Notification text/event.
2. App icon/avatar.
3. Preview image.
4. Big image.
5. Other/debug assets.

---

## 9. WebSocket Sync

### 9.1 Android Role

Android is the WebSocket client.
Windows is the WebSocket server.

### 9.2 Connection Behavior

Balanced:
- Connect when possible.
- Reconnect on network availability.
- Send queued items.

Realtime:
- Try to keep connection alive.
- May use foreground service.
- Recommend battery unrestricted setting.

Battery Saver:
- Connect less often.
- Batch sync.

### 9.3 Event Delivery

Android sends:
- Notification event.
- Raw snapshot.
- Parsed v1.
- Asset manifests.

Windows responds:
- `event_ack`
- `asset_missing_request`

Android then:
- Uploads requested assets.

### 9.4 ACK Semantics

Notification event:
- Android marks event as synced only after Windows ACK confirms persisted.

Asset:
- Android marks asset delivered to that Windows device only after asset ACK.

---

## 10. Pairing

### 10.1 QR Scan

QR payload includes:
- protocol version
- PC device ID
- PC name
- IP
- port
- one-time pairing token
- public key
- expiration

### 10.2 Pairing Result

After successful pairing Android stores:
- Windows device ID.
- Windows display name.
- Connection endpoint info.
- Public key / shared secret.
- Pairing time.
- Last seen.
- Enabled/paused status.

---

## 11. Local Database Concept

### 11.1 Tables

#### paired_windows_devices

```text
id
pc_device_id
pc_name
last_ip
last_port
public_key
shared_secret_ref
paired_at
last_seen_at
is_paused
is_revoked
```

#### app_allowlist

```text
package_name
app_name
is_allowed
last_seen_at
icon_asset_hash
```

#### notification_events

```text
id
event_id
android_notification_key
package_name
app_name
posted_at
captured_at
raw_snapshot_json
parsed_result_json
sync_status
created_at
```

#### notification_event_delivery

```text
id
event_id
pc_device_id
status
retry_count
last_attempt_at
last_error
acked_at
```

#### assets

```text
hash
mime
byte_size
width
height
source_kind
originality_level
local_path
created_at
last_seen_at
```

#### asset_delivery

```text
id
asset_hash
pc_device_id
status
retry_count
last_attempt_at
last_error
acked_at
```

---

## 12. Error Handling

### 12.1 Notification Capture Errors

If raw capture fails:
- Store minimal event with error.
- Do not crash listener.

If asset extraction fails:
- Store event without asset.
- Add extraction error in debug metadata.

### 12.2 Network Errors

- Queue remains pending.
- Retry with backoff.
- Show connection status.

### 12.3 Asset Transfer Errors

- Retry asset.
- Do not resend notification event if already ACKed unless needed.
- Windows can request missing assets again later.

---

## 13. Privacy

Android must make clear:
- Notifications may contain private content.
- Only allowlisted apps are synced.
- Data is sent to paired Windows devices only.
- User can pause/revoke sync.

No cloud sync in MVP.

---

## 14. MVP Acceptance Criteria

### Notification Access

- User can enable Notification Access.
- App detects enabled/disabled state.

### App Allowlist

- User can select apps to sync.
- Non-allowlisted apps are ignored.

### Pairing

- User scans Windows QR.
- Pairing completes.
- PC appears in paired list.

### Notification Sync

- Allowlisted notification appears on Windows.
- Raw snapshot is sent.
- Parsed result is sent.
- Event ACK is processed.

### Asset Sync

- Asset hash manifest is sent.
- Windows requests missing asset.
- Android uploads asset.
- Windows ACK is processed.

### Queue Reliability

- If Windows is offline, event remains pending.
- When Windows returns, event syncs.

---

## 15. Non-MVP Future Items

- Cloud relay.
- Reply from Windows.
- Dismiss phone notification from Windows.
- Per-PC custom allowlist.
- Rich remote actions.
- Android local raw viewer.
- Advanced battery diagnostics.
- Export diagnostic bundle.
