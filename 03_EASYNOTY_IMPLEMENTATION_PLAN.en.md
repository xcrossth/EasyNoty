# EasyNoty MVP implementation plan

[ภาษาไทย](03_EASYNOTY_IMPLEMENTATION_PLAN.md) | [Index](README.en.md)

This is the accepted plan, not a completion checklist. See [acceptance status](logs/ACCEPTANCE_STATUS.en.md) for evidence and open work. Build guides distinguish current toolchain use from this plan's baseline.

## 1. Structure and toolchain

- One monorepo with `windows/`, `android/`, and `protocol/`; original specifications remain at the root.
- Android identifier: `app.easynoty.android`; Windows AUMID: `app.easynoty.desktop`; C# namespace: `EasyNoty`.
- Windows: .NET 10 LTS, WinUI 3, Windows App SDK 2.2 stable, x64, Windows 10 build 19041+.
- Desktop: unpackaged/framework-dependent. Self-contained `EasyNoty.Setup.exe` requests UAC only for Windows App Runtime installation and Private-network firewall rules.
- Android baseline: JDK 17, AGP 9.2 / Gradle 9.4.1, minSdk 30, target API 36, Compose BOM 2026.05, Room 2.8.4, WorkManager 2.11.2.
- Application UI is English, with strings in resources for future localization. Repository documentation is bilingual with Thai as the default.

## 2. Protocol, security, and storage

- Protocol v1 uses JSON Schema and shared golden fixtures. C# and Kotlin own separate models validated against the same contract.
- Envelope fields include `protocol_version`, `message_id`, `message_type`, `device_id`, `sent_at_utc`, `session_id`, and `payload`; authenticated messages also carry the schema's authentication object.
- QR includes IPv4/IPv6 endpoints/port, PC identity, certificate SHA-256 fingerprint, a one-time token, and a two-minute expiry.
- Windows creates its certificate. Android connects over pinned WSS using QR/discovery information; both show a short verification code and Windows waits for explicit Accept.
- Pairing uses a 256-bit secret, HMAC challenge/session authentication, and message HMAC binding metadata to the payload hash. Replay prevention uses message IDs; identical retries can receive the same ACK.
- Protect keys/secrets using Windows DPAPI and Android Keystore; redact content, tokens, and secrets from logs.
- At-least-once delivery: event ACK after raw/revision transaction commit; asset ACK after decompression/hash verification and storage. Retry preserves event identity without duplicate records.
- Events include `notification_instance_id`, `revision_index`, group/style/ongoing metadata, raw size, and hash. Normal posts create new logical items; ongoing/progress/media/service updates append immutable revisions to the same logical item.
- Android removal closes the local active instance only; it does not delete Windows history. Group summaries are archived for debug/re-parse but excluded from toast/Inbox/unread counts.
- Event/raw compression uses Zstd then zlib fallback with the asset threshold policy. Compressed or oversized representations use start/chunk/complete framing when required; chunks are 64 KiB with canonical SHA-256.
- Raw snapshots have no application hard limit. Windows marks raw above 2 MiB as oversized; serialization/storage failure should preserve a minimal event and error.
- Assets are original-first, capped at 25 MiB, and hashed from canonical bytes. Below 64 KiB send inline; larger assets are chunked. Disabling image sync still sends app icons.
- Windows uses EF Core 10, SQLite WAL, and migrations for logical notifications, immutable revisions, parsed results, devices/apps, asset mappings, pairing sessions, and settings.
- Android uses Room with per-PC delivery state. Paused PCs retain backlog; GC waits until every non-revoked PC ACKs and seven days have elapsed.

## 3. Milestones

### 1. Foundation and contract

Initialize Git, central dependency versions, build/test scripts, and fixtures. Create Windows Core/Infrastructure/Desktop/Setup and Mock Android CLI. Mock scenarios cover pairing, HMAC, retry, malformed messages, assets, large raw, and offline simulation.

### 2. Windows foundation

Generic Host and dual-stack Kestrel WSS; Win32 DNS-SD/mDNS; single-instance lifecycle; EF migrations; storage resolver; content-addressed assets; redacted rolling logs; tray/startup; setup/runtime/firewall health checks. Prefer writable `<AppFolder>/data`, otherwise `%LocalAppData%/EasyNoty`.

### 3. Windows feature completion with Mock Android

- Pairing QR/Accept, device pause/rename/revoke, certificate/HMAC authentication.
- Receiver, idempotency, immutable raw revisions, Windows-first parsing with Android fallback.
- Generic, MessagingStyle, Gmail fallback; re-parse, raw viewer, export.
- Native notifications, click-to-focus, ongoing toast updates using tag/group.
- Inbox/Archived/All, virtualized list, multi-device/app filters, counts, search, read/unread, archive, soft delete.
- Sensitive status shared by package; hide toast/list content and reveal only one selected item until selection loss, minimize, close, or Windows lock.
- Redact all toast content during Windows lock and guide users to disable OS lock-screen notification content.
- Retention uses posted time: archive after 30 days and never auto-delete by default.
- Storage migration stops receiving, backs up SQLite, copies/verifies DB/assets/keys, restarts, and retains a timestamped source backup.
- Windows is complete only when all Mock acceptance gates pass, before physical Android acceptance.

### 4. Android application

- Compose onboarding: Notification Access → explicit allowlist → pairing; no apps are enabled outside the user's choices.
- Immediate raw/asset capture, active-instance tracking, group metadata, minimal-event fallback.
- Room queues, per-PC ACK/retry, compression workers, cache GC.
- OkHttp WSS, certificate pinning, HMAC session, NSD discovery, CameraX/ML Kit QR.
- Balanced default, Realtime foreground service, Battery Saver WorkManager batches.
- New PC asks new-only versus seven-day cache backfill, default new-only.
- Revoking destroys the pair's secret/queue while Windows archive and device tombstone remain.

### 5. Packaging and hardening

Unsigned local x64 Windows build plus setup helper and Unknown Publisher explanation; debug/local Android APK for ADB/sideload, no Play Store/AAB. Check disk-full, DB failure, hash mismatch, expiry, clock skew, reconnect, IP changes, IPv4/IPv6, and Public-network warnings.

## 4. Tests and acceptance

- Unit tests: parser fixtures, compression policy/fallback, hashes, HMAC/replay, revision rules, filters, sensitive state, retention.
- Integration: WSS pairing, duplicates, ACK timing, retry, corrupt chunks, missing assets, revoke/pause/backlog, migration rollback.
- Windows 10 22H2 manual gates: tray, startup, cold toast activation, focused detail, lock redaction, multi-select, portable fallback.
- Android: Room migrations/DAO, workers, snapshots/assets, shared fixtures.
- Prepare JDK, Android Studio/Wrapper, USB debugging, and ADB drivers before physical tests.
- Physical gates: allowlisted/blocked apps, groups/ongoing, offline recovery, Wi-Fi/IP changes, Doze, Realtime, 25 MiB rejection, backfill.
- Final acceptance: real Android → Windows, no cloud, no elevated desktop process, no sensitive content in default diagnostics.

## 5. Locked defaults

Windows is the primary archive/parser. Every non-revoked PC has delivery obligations; pause retains backlog. Toast clicks temporarily focus detail without changing filters and mark read when detail opens. Sensitive state is package-wide across devices. Diagnostic exports redact by default; raw samples require explicit selection/confirmation. Migration preserves a backup. No history encryption at rest, cloud, reply, phone dismissal, physical asset cleanup, or code signing in the MVP.
