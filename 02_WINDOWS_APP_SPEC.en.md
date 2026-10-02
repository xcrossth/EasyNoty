# Windows App Spec — Notification Bridge Windows

[ภาษาไทย](02_WINDOWS_APP_SPEC.md) | [Documentation index](README.en.md)

This is the original design draft, not a completion report. The [accepted plan](03_EASYNOTY_IMPLEMENTATION_PLAN.en.md) supersedes draft alternatives: toast activation must preserve filters, storage migration retains a backup, and the fallback directory is `%LocalAppData%/EasyNoty`. [Protocol v1](protocol/README.en.md) defines actual QR/message fields. See [current status](logs/ACCEPTANCE_STATUS.en.md) for unfinished UI and acceptance work.

Version: 0.1 MVP Draft
Date: 2026-07-03
Target: Windows 10/11 / C# / WinUI 3 / Windows App SDK

---

## 1. Purpose

The Windows app receives Android notifications and becomes the primary desktop inbox/archive.

It must:

- Pair with one or more Android devices.
- Receive notification events over LAN WebSocket.
- Store raw snapshots and parsed views.
- Request and store missing assets.
- Show native Windows toast notifications.
- Provide a modern inbox UI.
- Support read/unread, archive, soft delete, sensitive apps, and filters.
- Let the user choose storage location.
- Support auto archive and auto soft delete.

---

## 2. Technology Stack

| Area | Choice |
|---|---|
| Language | C# |
| UI | WinUI 3 |
| Framework | Windows App SDK |
| Target OS | Windows 10 + Windows 11 |
| App mode | User-level tray app |
| Notifications | Native Windows app/toast notifications |
| Local DB | SQLite |
| Asset storage | Local file system |
| WebSocket | Local server |
| Compression | Zstd primary, zlib fallback |
| Hash | SHA-256 |

Important:
- The app must not require Administrator/elevated mode.
- Native Windows toast support should be tested on Windows 10.

---

## 3. Main Responsibilities

1. Run in tray/background.
2. Host LAN WebSocket server.
3. Show QR pairing.
4. Manage paired Android devices.
5. Receive notification events.
6. Persist event/raw/parsed data.
7. Show native Windows toast.
8. Request missing assets.
9. Store original assets.
10. Display inbox.
11. Apply filters.
12. Handle sensitive app content hiding.
13. Manage retention jobs.

---

## 4. Application Structure

Suggested modules:

```text
Windows App
├─ AppHost / Startup
├─ Tray Manager
├─ WebSocket Server
├─ Pairing Manager
├─ Device Manager
├─ Notification Receiver
├─ Protocol Validator
├─ SQLite Repository
├─ Asset Store
├─ Compression Service
├─ Parser Engine
├─ Toast Service
├─ Inbox UI
├─ Filter State Manager
├─ Sensitive App Manager
├─ Retention Service
└─ Settings UI
```

---

## 5. Running Mode

The app should be a normal user-level app.

Expected behavior:
- Can start with Windows.
- Shows tray icon.
- Can open inbox window.
- Can run WebSocket server while user session is active.
- Can show native Windows toast.

Do not implement as Windows Service in MVP.

---

## 6. Storage Location

### 6.1 Options

User-selectable:

1. App subfolder beside the app.
2. `%LocalAppData%`.

### 6.2 Default Behavior

Default:
- Use subfolder beside app if writable.
- If not writable, fallback to `%LocalAppData%`.
- Show fallback notice in Settings.

### 6.3 Suggested Paths

Portable/default:

```text
<AppFolder>/data/
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
  thumbs/
```

### 6.4 Migration

If user changes storage location:
- MVP may require restart.
- Future phase should support migration/copy.
- For MVP, show warning before changing.

---

## 7. Database Model

### 7.1 devices

```text
id
device_id
display_name
device_type
device_image_asset_hash
public_key
shared_secret_ref
paired_at
last_seen_at
is_paused
is_revoked
created_at
updated_at
```

### 7.2 apps

Per Android app/package.

```text
id
package_name
app_name
app_icon_asset_hash
is_sensitive
first_seen_at
last_seen_at
created_at
updated_at
```

Sensitive states:
- `normal`
- `sensitive`

MVP can store as boolean `is_sensitive`.

### 7.3 notifications

```text
id
event_id
device_id
package_name
android_notification_key
posted_at
captured_at
received_at

raw_snapshot_json
raw_snapshot_schema_version

current_parser_name
current_parser_version

title
body
preview_text
sender_name
conversation_title
grouping_key
confidence

app_icon_asset_hash
avatar_asset_hash
preview_asset_hash

is_read
read_at

is_archived
archived_at

is_deleted
deleted_at

created_at
updated_at
```

Notes:
- `is_deleted` is soft delete only.
- Deleted items should not be visible in normal UI.
- Raw snapshot should not be modified after insert.

### 7.4 notification_parsed_results

Optional but recommended for re-parse history.

```text
id
notification_id
parser_name
parser_version
parsed_json
title
body
preview_text
sender_name
conversation_title
grouping_key
confidence
parsed_at
is_current
```

### 7.5 assets

```text
hash
mime
byte_size
width
height
local_path
source_kind
originality_level
first_seen_at
last_seen_at
created_at
```

### 7.6 notification_assets

Many-to-many mapping.

```text
id
notification_id
asset_hash
role
created_at
```

### 7.7 device_app_stats

Can be computed, but caching may improve UI.

```text
device_id
package_name
unread_count
total_count
last_notification_at
```

### 7.8 settings

Simple key-value.

```text
key
value_json
updated_at
```

---

## 8. WebSocket Server

### 8.1 Role

Windows app is the WebSocket server.
Android apps connect as clients.

### 8.2 Server Lifecycle

- Start when app starts.
- Listen on configurable local port.
- Port can be auto-assigned at first run.
- Show connection status in tray/settings.
- Restart server if port changes.

### 8.3 Pairing Endpoint

Used only during active pairing session.

Requirements:
- Pairing token expires.
- Only one or limited active pairing sessions.
- Validate protocol version.
- Store paired device.

### 8.4 Authenticated Device Endpoint

After pairing:
- Validate device identity.
- Validate message signature/token.
- Reject revoked devices.
- Reject paused devices if needed.

---

## 9. Pairing UI

### 9.1 Flow

1. User opens Settings → Devices → Pair New Device.
2. Windows generates pairing token.
3. Windows shows QR code.
4. Android scans QR and connects.
5. Windows displays pairing confirmation.
6. Device is added to device list.

### 9.2 QR Payload

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

### 9.3 Device Management UI

For each device:
- Device image/icon.
- Display name.
- Last seen.
- Connection status.
- Unread count.
- Pause/resume.
- Rename.
- Revoke/delete pairing.

---

## 10. Notification Receive Flow

```text
Android sends notification_event
↓
Windows validates envelope/device
↓
Windows persists raw snapshot
↓
Windows inserts/updates app/device stats
↓
Windows stores parsed v1
↓
Windows applies Windows parser if needed
↓
Windows creates notification row
↓
Windows ACKs event
↓
Windows checks asset hashes
↓
Windows requests missing assets
↓
Windows shows native toast
```

Important:
- Event ACK should occur after DB persistence.
- Toast can show before assets arrive using placeholders.
- If sensitive app, toast content must be hidden.

---

## 11. Asset Receive Flow

```text
Windows receives asset manifest in notification_event
↓
Check local asset store by hash
↓
Build missing hash list
↓
Send asset_missing_request
↓
Receive asset transfer
↓
If compressed, decompress
↓
Verify SHA-256(original_bytes) == hash
↓
Store original bytes
↓
Update asset table
↓
ACK asset
```

### 11.1 File Layout

Suggested:

```text
assets/
  sha256/
    ab/
      abcd1234....bin
```

File extension:
- Can use MIME-derived extension for convenience.
- Hash remains source of truth.

Examples:
```text
assets/sha256/ab/abcdef...png
assets/sha256/12/123456...webp
```

### 11.2 Clear Unused Files

Not MVP.

Future feature:
- Scan assets not referenced by non-deleted notifications.
- Show reclaimable size.
- Let user clear physically.

---

## 12. Compression Handling

### 12.1 Supported Encodings

- none
- zstd
- zlib

### 12.2 Windows Behavior

When receiving:
1. Read transfer encoding.
2. Decompress if required.
3. Verify hash from decompressed/original bytes.
4. Store original bytes as canonical asset.

Windows should not store only compressed bytes as canonical unless it also keeps enough metadata to reconstruct and verify original. Recommended canonical store is original bytes.

### 12.3 Zstd Settings

Windows should store current setting:
- Level 6 / 9 / 12
- Default 9

Android performs upload compression, but Windows may also use compression for internal storage decisions later.

---

## 13. Native Toast Notifications

### 13.1 Default Behavior

When notification arrives:
- Show native Windows toast.
- Toast click opens inbox.
- Inbox focuses the clicked item.

### 13.2 Toast Content for Normal Apps

Include where available:
- App name.
- Sender/title.
- Message preview.
- Avatar/app icon if asset already available.
- Device name if useful.

### 13.3 Toast Content for Sensitive Apps

Content hidden.

Example:
```text
LINE
Content hidden
```

Do not show body/sender preview in toast.

### 13.4 If Asset Missing

Use placeholder:
- App icon if available.
- Generic icon otherwise.
- Update inbox later when asset arrives.

Toast does not need to update after asset arrives in MVP.

### 13.5 Custom Toast

Not MVP.

Only consider custom toast if native Windows toast cannot represent needed UX.

---

## 14. Inbox UI

### 14.1 Main Layout

```text
┌──────────────────────────────────────────────────────────────┐
│ Notification Inbox                                            │
├───────────────┬──────────────────────────────────────────────┤
│ Device Tabs   │ App Tabs                                      │
│               │ [All Apps 12] [LINE 5] [Discord 3] [Gmail 2] │
│ [All 20]      ├──────────────────────────────────────────────┤
│ [Phone A 8]   │ Toolbar / Search / View selector              │
│ [Phone B 12]  ├──────────────────────────────────────────────┤
│               │ Notification List                             │
│               │ Detail Pane optional                          │
└───────────────┴──────────────────────────────────────────────┘
```

### 14.2 Device Tabs

Left side.

Each item:
- Device image/icon.
- Device name.
- Unread count.

Examples:
- All Devices
- Samsung S24
- Redmi Note
- Tablet

Requirements:
- Multi-select supported.
- `All Devices` acts as no device filter.

### 14.3 App Tabs

Top area under device filter.

Each item:
- App icon.
- App name.
- Unread count.

Examples:
- All Apps
- LINE
- Discord
- Gmail

Requirements:
- Multi-select supported.
- `All Apps` acts as no app filter.
- Counts reflect current device filter if possible.

### 14.4 Notification List Row

Normal app row:
```text
[avatar/icon] App · Device · Time
Sender or Title
Message preview
```

Sensitive app row:
```text
[icon] App · Device · Time
Content hidden. Click to reveal.
```

Unread row:
- Visually distinct.
- Bold text or unread indicator.

Read row:
- Normal weight.

Archived:
- Hidden from Inbox.
- Visible in Archive/All views.

Deleted:
- Hidden from all normal app UI.

### 14.5 Click Behavior

When user clicks a toast:
- Open main inbox window if not open.
- Apply filters if needed or keep current filters but focus item.
- Scroll to notification.
- Highlight/focus row.
- Mark item read according to read policy.

For sensitive app:
- Open detail/focused row.
- Content can be revealed after explicit item click.

---

## 15. Views

MVP views:

1. Inbox
2. Archived
3. All

Deleted view:
- Not required for MVP because soft-deleted items should not be visible.
- Developer/debug view can be added later if needed.

---

## 16. Read/Unread

Windows-only state.

Rules:
- New notification defaults to unread.
- Opening/focusing detail can mark as read.
- User can manually mark read/unread.
- Read state does not sync to Android.
- Do not care whether phone notification is read/deleted.

---

## 17. Archive / Delete

### 17.1 Archive

Archive means:
- Hide from Inbox.
- Show in Archived/All.
- Keep all data/assets.

Manual archive:
- User action.

Auto archive:
- Based on retention setting.

Default:
- 30 days.

### 17.2 Delete

Delete means soft delete:
- Set `is_deleted = true`.
- Set `deleted_at`.
- Hide from app UI.
- Do not delete assets.
- Do not delete raw DB row physically in MVP.

Auto delete:
- Default never.
- Options 30/60/90/custom/never.

Physical cleanup:
- Future “Clear unused files”.

---

## 18. Sensitive App Manager

Settings UI:
- List apps seen from Android notifications.
- Each app has state:
  - Normal
  - Sensitive

Sensitive behavior:
- Hide content in toast.
- Hide content in list.
- Reveal only when opening/clicking item.
- Should apply to old and new notifications.

Potential setting:
- “Always hide on lock screen” should be default.

---

## 19. Search and Filters

MVP search:
- Basic search over:
  - app name
  - title
  - sender name
  - body/preview text
- Exclude deleted by default.
- Respect selected devices and apps.
- Sensitive content should not be previewed in results unless item is opened.

Advanced full-text search can be later.

---

## 20. Parser Engine

### 20.1 Purpose

Windows must be able to re-parse old notifications from raw snapshots.

### 20.2 Parser Layers

1. Generic parser
2. Style-aware parser
3. App-specific parser
4. Future user rules

### 20.3 MVP Parsers

Implement:
- Generic Android parser.
- Basic MessagingStyle-like parser from raw snapshot.
- Basic Gmail/email-style fallback if obvious.
- App-specific parsers can be added later.

### 20.4 Re-parse UI

Advanced/Developer section:
- Re-parse selected notification.
- Re-parse all from selected app.
- Re-parse all history.
- Show raw payload.
- Export raw sample.

---

## 21. Settings

### 21.1 General

- Launch on startup.
- Theme/system theme.
- Storage location.
- Port.
- Device name.

### 21.2 Notifications

- Show native toast on/off.
- Show on lock screen: off by default.
- Hide sensitive content: on by default.
- Click behavior: open inbox and focus item.

### 21.3 Retention

Auto archive:
- 30 default
- 60
- 90
- never
- custom

Auto delete:
- never default
- 30
- 60
- 90
- custom

### 21.4 Devices

- Pair new device.
- Device list.
- Rename/pause/revoke.

### 21.5 Apps

- App list.
- Sensitive state normal/sensitive.
- Unread count per app.

### 21.6 Advanced

- Re-parse.
- Raw viewer.
- Export diagnostics.
- Compression settings display.
- Clear unused files placeholder/future.

---

## 22. Tray Icon

Tray menu should include:
- Open Inbox.
- Pair Device.
- Pause Sync.
- Settings.
- Quit.

Tray state:
- Connected.
- Disconnected.
- New unread count if feasible.

---

## 23. Retention Job

Runs periodically.

Archive job:
```text
Find notifications older than archive threshold
where is_archived = false
and is_deleted = false
set is_archived = true
set archived_at = now
```

Delete job:
```text
If auto delete != never:
Find notifications older than delete threshold
where is_deleted = false
set is_deleted = true
set deleted_at = now
```

Do not remove DB rows physically.
Do not delete asset files.

---

## 24. Error Handling

### 24.1 Protocol Errors

- Return structured error.
- Log error.
- Do not crash server.

### 24.2 DB Errors

- Show app-level warning.
- Keep raw incoming event if possible.
- Avoid ACK until persisted.

### 24.3 Asset Verification Failure

- Reject asset.
- Do not store.
- Request retry or mark failed.
- Log hash mismatch.

### 24.4 Pairing Failure

- Show clear reason.
- Expired token.
- Version mismatch.
- Connection failed.

---

## 25. Privacy / Security

- Store all data locally.
- No cloud in MVP.
- Device pairs must be revocable.
- Sensitive apps must hide content.
- App should make storage path visible.
- Consider DB encryption later.
- If Windows is locked, lock screen preview must be off by default.
- Future: Windows Hello/PIN to open sensitive content.

---

## 26. MVP Acceptance Criteria

### Startup

- App starts as user-level app.
- Tray icon appears.
- WebSocket server starts.

### Pairing

- User can show QR.
- Android can pair.
- Device appears in device list.

### Receive Notification

- Windows receives notification event.
- Event is persisted before ACK.
- Native toast appears.
- Clicking toast opens inbox focused on item.

### Inbox

- New item appears unread.
- User can mark read/unread.
- User can archive.
- Soft-deleted items are hidden.

### Device/App Filters

- Left device tab shows All Devices and each phone.
- Top app tab shows All Apps and app list.
- Multi-select device and app filters work.
- Counts update.

### Sensitive App

- User marks app as sensitive.
- Sensitive toast hides content.
- Sensitive list row hides content.
- Clicking item reveals content.

### Asset Sync

- Missing asset request is sent.
- Asset received and verified by hash.
- Asset stored.
- Notification row can use asset after available.

### Retention

- Auto archive default 30 days.
- Auto delete default never.
- Delete is soft delete only.

---

## 27. Non-MVP Future Items

- Custom toast renderer.
- Clear unused files.
- Thumbnail generation.
- Full-text search.
- DB encryption.
- Windows Hello/PIN lock.
- Reply from Windows.
- Dismiss phone notification from Windows.
- Cloud relay.
- Multiple Windows clients.
- Export/import backup.
- AI summary/grouping.
