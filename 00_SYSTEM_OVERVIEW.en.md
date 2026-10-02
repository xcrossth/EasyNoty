# Android → Windows Notification Bridge — System Overview Spec

[ภาษาไทย](00_SYSTEM_OVERVIEW.md) | [Documentation index](README.en.md)

This is the original design draft, not a completion report. The [accepted implementation plan](03_EASYNOTY_IMPLEMENTATION_PLAN.en.md) resolves later decisions; [Protocol v1](protocol/README.en.md) and its schemas define the wire format. See [current status](logs/ACCEPTANCE_STATUS.en.md) for implemented features and open work. JSON examples below are conceptual, not current wire fixtures.

Version: 0.1 MVP Draft
Date: 2026-07-03
Owner/Context: Personal-use MVP first, extensible to product later

---

## 1. Product Summary

This project is a two-app system:

1. **Android app**
   - Reads Android notifications using `NotificationListenerService`.
   - Lets the user choose which mobile apps are allowed to sync to Windows.
   - Captures raw notification snapshots and original notification assets.
   - Sends notification events to the Windows app over LAN WebSocket.
   - Supports hash-first asset sync and lazy missing-asset upload.

2. **Windows desktop app**
   - Receives notifications from one or more paired Android devices.
   - Shows native Windows toast notifications.
   - Stores a full local inbox/history with read/unread/archive/delete states.
   - Supports device/app filtering, sensitive app handling, raw payload storage, parser versioning, and re-parse capability.
   - Acts as the primary archive.

The intended MVP is **LAN-first**, **local-first**, and **privacy-first**.

---

## 2. Target Platforms

### Android

- Minimum: **Android 11 / API 30**
- Reason:
  - Personal-use MVP does not need old Android support.
  - Android 11+ is enough for default `WEBP_LOSSLESS` bitmap snapshot behavior.
- Language: **Kotlin**
- UI: Native Android UI, preferably Jetpack Compose.
- Testing:
  - Android Studio recommended.
  - Real Android phone required for real notification/background tests.
  - Emulator useful only for UI and mock flows.

### Windows

- Target: **Windows 10 + Windows 11**
- Windows app technology:
  - **C#**
  - **WinUI 3**
  - **Windows App SDK**
- The app must run as a **normal user-level tray app**, not elevated/admin.
- Must support native Windows toast notifications.
- Should not be implemented with Electron/React/Flutter/Tauri for MVP.

---

## 3. High-Level Architecture

```text
┌──────────────────────────────────────────────────────────────────┐
│ Android Device                                                    │
│                                                                  │
│ NotificationListenerService                                      │
│   ↓                                                              │
│ Raw Snapshot Capture                                             │
│   ↓                                                              │
│ Asset Extractor + Hash Store                                     │
│   ↓                                                              │
│ Android Local Queue / Retry / ACK Tracking                       │
│   ↓                                                              │
│ WebSocket Client                                                 │
└───────────────────────────────┬──────────────────────────────────┘
                                │ LAN WebSocket
                                │ QR paired, per-device key/token
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│ Windows Desktop App                                               │
│                                                                  │
│ WebSocket Server                                                  │
│   ↓                                                              │
│ Event Receiver + Validator                                        │
│   ↓                                                              │
│ SQLite DB / Raw Archive / Parsed Views / Asset Store              │
│   ↓                         ↓                                    │
│ Native Windows Toast         Inbox UI                             │
│                              Device/App Filters                   │
│                              Read/Unread/Archive/Delete           │
└──────────────────────────────────────────────────────────────────┘
```

---

## 4. Core Principles

### 4.1 Raw-first

The Android app must send a raw notification snapshot in addition to parsed fields.

Reason:
- Some apps have unusual notification formats.
- App notification formats can change over time.
- Windows must be able to re-parse old notifications after parser improvements.

### 4.2 Original-asset-first

Image/icon/avatar assets should preserve the best original representation available.

Rules:
- If Android exposes encoded original bytes, store those bytes.
- If Android only exposes `Bitmap`, `Icon`, or `Drawable`, encode a lossless snapshot.
- Default snapshot format: **WebP lossless**.
- User-selectable snapshot formats:
  - PNG
  - WebP
  - WebP lossless
- Do not resize or lossy-convert images in MVP.
- Thumbnail generation is a Windows-side future feature.

### 4.3 Hash-first asset sync

Notification events should reference images/assets by hash.

Flow:
1. Android captures asset.
2. Android computes hash from the original canonical bytes.
3. Android stores asset locally.
4. Android sends notification event with asset manifest only.
5. Windows checks which hashes it does not have.
6. Windows requests missing hashes.
7. Android uploads only missing assets.

### 4.4 Local-first privacy

- LAN-first sync.
- Windows stores the primary archive.
- Sensitive apps hide content until explicitly opened.
- Lock screen preview should be off by default.
- Cloud relay is not part of MVP.

### 4.5 Reliability over real-time perfection

- WebSocket should be used for real-time delivery when possible.
- Do not assume the socket is always alive.
- Every notification must go through a local queue.
- Use ACK/retry.
- Android sleep/Doze may delay network activity.

---

## 5. MVP Scope

### In Scope

- Android notification access.
- Android app allowlist.
- Android raw snapshot capture.
- Android original asset capture and hash storage.
- Android local event/asset queues.
- LAN WebSocket connection.
- QR pairing.
- Per-device identity and revoke support.
- Windows native toast notifications.
- Windows SQLite inbox/history.
- Windows read/unread status.
- Windows auto archive and soft delete.
- Windows raw payload storage.
- Windows parser versioning and re-parse support.
- Multiple Android devices paired to one Windows app.
- Multi-select device filter.
- Multi-select app filter.
- Sensitive app configuration on Windows.
- Basic search/filter.

### Out of Scope for MVP

- iOS support.
- Cloud relay.
- Reply from Windows to mobile apps.
- Syncing “read” state back to phone apps.
- Syncing deletion state from phone to Windows.
- Deleting phone notifications from Windows.
- Custom toast UI.
- AI summary.
- OCR.
- Physical file cleanup for unused assets.
- Full-text indexing beyond basic search.
- Windows Hello/PIN lock.
- Cross-Windows sync.

---

## 6. Key Decisions

| Area | Decision |
|---|---|
| Android min version | Android 11+ |
| Android language | Kotlin |
| Windows technology | C# + WinUI 3 + Windows App SDK |
| Windows OS | Windows 10/11 |
| Sync | LAN WebSocket |
| Pairing | QR pairing + per-device key/token |
| Windows app mode | User-level tray app, not elevated |
| Toast | Native Windows toast first |
| Custom toast | Later only if needed |
| Phone state | Ignore phone read/delete state in MVP |
| Windows click behavior | Open inbox and focus clicked item |
| Raw notification | Store immutable raw snapshot |
| Parser | Store parser version; allow re-parse |
| Asset sync | Hash-first, missing-asset request |
| Image policy | Original-first, no resize/lossy conversion |
| Bitmap snapshot default | WebP lossless |
| Compression primary | Zstd |
| Compression fallback | zlib |
| Zstd levels | 6 / 9 / 12 |
| Zstd default | 9 |
| Compression threshold | 5 / 10 / 15 / 20% |
| Threshold default | 10% |
| Windows archive | Windows is primary archive |
| Auto archive | 30 / 60 / 90 / never / custom; default 30 days |
| Auto delete | 30 / 60 / 90 / never / custom; default never |
| Delete behavior | Soft delete only |
| File cleanup | Future “clear unused files” feature |

---

## 7. Sync Modes

### 7.1 Balanced Mode

Default mode.

Behavior:
- WebSocket connected when possible.
- Notification events are queued first.
- Send immediately when connected.
- Retry when disconnected.
- Asset upload can lag behind text event.
- No aggressive foreground service by default.

### 7.2 Realtime Mode

User-enabled mode.

Behavior:
- Tries to keep connection alive more aggressively.
- May use a persistent foreground service notification.
- App should guide user to allow background/unrestricted battery behavior.
- More battery usage is acceptable and should be explained.

### 7.3 Battery Saver Mode

Behavior:
- Avoids always-on socket.
- Syncs in batches.
- Large assets may be delayed.
- Best effort, not real-time.

---

## 8. Pairing and Device Identity

### 8.1 Pairing Flow

```text
Windows:
1. User opens Pair Device.
2. Windows starts a temporary pairing session.
3. Windows shows QR code.

Android:
4. User scans QR.
5. Android connects to Windows WebSocket pairing endpoint.
6. Both sides exchange device identity.
7. Pairing token is validated.
8. Per-device secret/key is established.
9. Windows stores Android device entry.
10. Android stores Windows device entry.
```

### 8.2 QR Payload Concept

```json
{
  "protocol_version": 1,
  "pc_device_id": "pc-uuid",
  "pc_name": "June-PC",
  "ip": "192.168.1.45",
  "port": 38492,
  "pairing_token": "one-time-token",
  "public_key": "pc-public-key",
  "expires_at": "2026-07-03T13:00:00+07:00"
}
```

### 8.3 Device Management

Windows must support:
- List paired phones.
- Rename phone.
- Revoke phone.
- Pause phone.
- View connection status.
- View unread count per phone.

Android must support:
- List paired Windows PCs.
- Revoke PC.
- Pause sync per PC.

---

## 9. Protocol Overview

### 9.1 Message Envelope

All WebSocket messages should use a standard envelope.

```json
{
  "protocol_version": 1,
  "message_id": "uuid",
  "message_type": "notification_event",
  "device_id": "android-device-id",
  "sent_at": "2026-07-03T13:00:00+07:00",
  "payload": {}
}
```

### 9.2 Important Message Types

| Type | Direction | Purpose |
|---|---|---|
| `hello` | both | Connection handshake |
| `pairing_request` | Android → Windows | Request pairing |
| `pairing_accept` | Windows → Android | Accept pairing |
| `notification_event` | Android → Windows | Send notification metadata/raw/parsed/asset refs |
| `event_ack` | Windows → Android | Confirm notification persisted |
| `asset_missing_request` | Windows → Android | Request missing asset hashes |
| `asset_transfer_start` | Android → Windows | Begin asset upload |
| `asset_transfer_chunk` | Android → Windows | Upload chunk |
| `asset_transfer_complete` | Android → Windows | End asset upload |
| `asset_ack` | Windows → Android | Confirm asset stored |
| `ping` / `pong` | both | Keepalive |
| `device_settings_update` | Windows → Android | Future settings push |
| `error` | both | Protocol error |

---

## 10. Notification Event Concept

```json
{
  "event_id": "uuid",
  "android_notification_key": "unique-statusbar-key",
  "device_id": "phone-uuid",
  "package_name": "com.discord",
  "app_name": "Discord",
  "posted_at": "2026-07-03T13:00:00+07:00",
  "captured_at": "2026-07-03T13:00:00+07:00",
  "raw_snapshot_schema_version": 1,
  "raw_snapshot": {},
  "parsed_result": {
    "parser_name": "generic_android",
    "parser_version": 1,
    "title": "Sender Name",
    "body": "Message preview",
    "sender_name": "Sender Name",
    "conversation_title": null,
    "confidence": 0.7
  },
  "assets": [
    {
      "role": "app_icon",
      "hash": "sha256:...",
      "mime": "image/png",
      "original_size": 18422,
      "width": 192,
      "height": 192
    }
  ]
}
```

---

## 11. Asset Handling

### 11.1 Asset Roles

| Role | Meaning |
|---|---|
| `app_icon` | Source app icon |
| `avatar` | Sender/person avatar |
| `preview_image` | Image preview |
| `big_picture` | Big notification image |
| `attachment_preview` | Attachment-like preview |
| `unknown` | Unknown image role |

### 11.2 Asset Hash

Canonical hash:

```text
asset_hash = SHA-256(original_canonical_bytes)
```

Hash should not be computed from compressed transport payload.

### 11.3 Compression Policy

Compression algorithm order:
1. Zstd
2. zlib fallback

Settings:
- Zstd levels: `6`, `9`, `12`
- Default level: `9`
- Threshold options: `5%`, `10%`, `15%`, `20%`
- Default threshold: `10%`

Rule:
```text
compressed_candidate = compress(original_bytes)

if compressed_size <= original_size * (1 - threshold):
    transfer/store compressed payload metadata
else:
    use original bytes
```

Windows must always verify:
```text
SHA-256(decompressed_or_original_bytes) == asset_hash
```

### 11.4 Asset Transfer

Small assets may be sent as one binary payload.
Large assets should be sent in chunks.

Suggested threshold:
```text
< 64 KB: single payload
>= 64 KB: chunked transfer
```

---

## 12. Windows Storage

### 12.1 Storage Location

User-selectable:

1. App subfolder beside executable/app folder.
2. `%LocalAppData%`.

Default:
- Use app subfolder if writable.
- If not writable, fallback to `%LocalAppData%` and show notice in Settings.

Example portable structure:

```text
./data/
  app.db
  assets/
  logs/
  thumbs/       # future
```

Fallback:

```text
%LocalAppData%/NotificationBridge/
  app.db
  assets/
  logs/
```

### 12.2 Delete Behavior

Delete means soft delete only.

```text
deleted_at != null
```

Soft-deleted notifications:
- Must not show in normal app UI.
- Must not physically delete assets.
- May remain in DB.

Future phase:
- Clear unused files.
- Scan soft-deleted/unreferenced assets.
- Preview reclaimable size.
- Physically delete unused assets.

---

## 13. Windows Inbox UX

### 13.1 Main Layout

```text
┌──────────────────────────────────────────────────────────────┐
│ Notification Inbox                                            │
├───────────────┬──────────────────────────────────────────────┤
│ Device Tabs   │ App Filter Tabs                               │
│               │ [All Apps 12] [LINE 5] [Discord 3] [Gmail 2] │
│ [All 20]      ├──────────────────────────────────────────────┤
│ [Phone A 8]   │ Notification List                             │
│ [Phone B 12]  │                                              │
└───────────────┴──────────────────────────────────────────────┘
```

### 13.2 Device Filter

Left-side tabs:
- Device image/icon.
- Device name.
- Unread count.
- Supports multi-select.

Examples:
- All Devices
- Samsung S24
- Redmi Note
- Tablet

### 13.3 App Filter

Top tabs under device area:
- App icon.
- App name.
- Unread count.
- Supports multi-select.

Examples:
- All Apps
- LINE
- Discord
- Gmail

### 13.4 Filter Semantics

```text
selected_devices = [Phone A, Phone B]
selected_apps = [LINE, Discord]

result =
notifications where
device_id in selected_devices
and app_package in selected_apps
and deleted_at is null
```

If `All Devices` is selected:
- Do not filter device.

If `All Apps` is selected:
- Do not filter app.

---

## 14. Sensitive App Handling

Sensitive app status is configured on Windows per app:

1. Normal app
2. Sensitive app

Sensitive app behavior:
- Toast hides content.
- Inbox list hides content.
- User must open inbox and click the specific item to reveal content.
- Revealing one item does not necessarily reveal all sensitive items.
- Lock screen preview must be off by default.

Example toast for sensitive app:

```text
LINE
Content hidden
```

Example inbox row:

```text
LINE · Phone A · 13:42
Content hidden. Click to reveal.
```

---

## 15. Retention Settings

### 15.1 Auto Archive

Options:
- 30 days
- 60 days
- 90 days
- Never
- Custom

Default:
- 30 days

Archive behavior:
- Sets archive flag/date.
- Hidden from main Inbox.
- Visible in Archived/All views.

### 15.2 Auto Delete

Options:
- 30 days
- 60 days
- 90 days
- Never
- Custom

Default:
- Never

Delete behavior:
- Soft delete only.
- Set `deleted_at`.
- Hide from app UI.
- Do not delete files/assets.

---

## 16. Re-parse Design

Raw snapshot must be immutable. Parsed results are derived.

```text
Raw Archive → Parser → Parsed View → Renderer
```

Windows should support:
- Re-parse selected notification.
- Re-parse all notifications from an app.
- Re-parse all history.
- Show raw payload.
- Export raw sample for debugging.

MVP may expose these under an Advanced/Developer section.

---

## 17. Security and Privacy Notes

- Notification content is highly sensitive.
- Do not send to cloud in MVP.
- Pairing must be explicit.
- Each device pair should have a revocable identity.
- Messages should include authentication.
- Encryption should be implemented as early as practical, even on LAN.
- Sensitive app handling must be easy to configure.
- Storage path should be transparent to user.

---

## 18. MVP Acceptance Criteria

### Android → Windows Notification

- Given Android Notification Access is enabled.
- And an app is allowlisted.
- When a notification arrives.
- Then Windows receives it.
- And Windows stores raw + parsed data.
- And Windows shows a native toast.
- And clicking the toast opens the inbox focused on that item.

### Asset Sync

- Given a notification references an avatar hash.
- And Windows does not have that hash.
- When Windows requests the missing hash.
- Then Android uploads the asset.
- And Windows verifies the SHA-256 hash.
- And Windows stores the original asset.

### Sensitive App

- Given LINE is marked sensitive on Windows.
- When a LINE notification arrives.
- Then toast content is hidden.
- And list preview is hidden.
- And content is revealed only when the item is opened.

### Multi-device Filter

- Given two phones are paired.
- When the user selects Phone A + Phone B and LINE + Discord.
- Then the inbox shows only LINE/Discord notifications from Phone A/B.

### Retention

- Given auto archive is 30 days.
- When a notification is older than 30 days.
- Then it is archived.
- Given auto delete is never.
- Then no notification is soft-deleted automatically.

---

## 19. Suggested File/Project Names

Working names:
- `NotificationBridge`
- `PhoneNotifyBridge`
- `WinNotifyRelay`
- `NotifyBridge`

Suggested repos:
```text
notify-bridge-android/
notify-bridge-windows/
notify-bridge-specs/
```

---

## 20. Recommended Next Steps for Codex

1. Create Windows app skeleton.
2. Create Android app skeleton.
3. Define shared protocol document/types manually in both projects.
4. Implement mock WebSocket notification event.
5. Implement Windows SQLite store and inbox list.
6. Implement native Windows toast click-to-focus behavior.
7. Implement Android NotificationListenerService capture.
8. Implement QR pairing.
9. Implement event queue/ACK.
10. Implement asset hash manifest and missing asset request.
11. Implement Zstd/zlib compression job queue.
12. Implement sensitive app behavior.
13. Implement retention jobs.
