# EasyNoty acceptance status

[ภาษาไทย](ACCEPTANCE_STATUS.md) | [Index](../README.en.md)

Last reviewed: 3 October 2026.

## Summary

Windows, Android, and protocol code and experimental builds exist, but **the MVP is not feature-complete and end-to-end acceptance has not passed**. Earlier notes saying only physical testing remained overstated the evidence. This page is the current status.

## Present in the workspace

- Monorepo, specifications, master/component plans, root verification, and bilingual documentation.
- Protocol v1: nine schemas, valid/invalid fixtures, canonical HMAC vector, C#/Kotlin tests.
- Windows: WSS receiver, SQLite/WAL, raw revisions, assets, parsers, pairing/auth services, basic Inbox, read/archive/delete, toast/tray/startup, DNS-SD/mDNS, and storage services.
- Recent Windows UI: caption-button clearance, size/position persistence, non-dismissible status bar, EXE/window/taskbar/tray icon.
- Android: allowlist capture, Room/per-PC queues, assets, Keystore, pinned WSS/HMAC, compression/chunking, WorkManager/Realtime, NSD, CameraX/ML Kit.

## Automated evidence

| Check | Result |
|---|---|
| Protocol verifier, 3 October | Passed: 9 schemas, 9 valid fixtures, 9 invalid fixtures, 1 HMAC vector |
| Windows Release, 3 October | Build passed without warnings/errors; 11/11 tests passed |
| Android, 3 October | testDebugUnitTest and assembleDebug succeeded using Gradle up-to-date results; existing report has 14/14 with no failures |
| Installation/visual validation this run | Not performed |
| ADB | No devices detected during the latest status inspection |

Builds/tests do not cover every acceptance requirement. A service lacking UI integration is not a complete user-facing feature.

## Remaining implementation

- Wire Windows device rename/pause/revoke, sensitive settings, raw viewer/re-parse/export, and storage migration into the UI.
- Complete pre-accept pairing-code comparison on both screens.
- Toast activation must focus detail without clearing filters; it currently clears them.
- Audit/complete settings, unread controls/count refresh, retention scheduling, diagnostics, and native integrations against the full plan.
- Setup currently adds a firewall rule and asks users to install Runtime themselves; planned Runtime installation/health flow is incomplete.

## Outstanding acceptance

1. Mock → Windows: pairing/auth, durable ACK, duplicates/replay, malformed/corrupt chunks, missing assets, retry, pause/revoke/backlog, migration rollback.
2. Windows manual: tray/startup, cold toast activation, focus/filter preservation, lock redaction, multi-select, storage fallback.
3. Physical Android/LAN: permissions/QR/NSD, allowed/blocked apps, grouped/ongoing notifications, assets, offline/Wi-Fi/IP recovery, Doze, Realtime, 25 MiB rejection, backfill.
4. Hardening: disk-full/DB failure, expiry/clock skew, IPv4/IPv6, Public-network handling, and private-data-free diagnostics.

## Next sequence

Complete Windows UI/flows → pass Mock acceptance → test physical hardware → record evidence and package an experimental build. Do not mark complete solely because builds pass.
