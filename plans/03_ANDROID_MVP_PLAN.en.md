# EasyNoty Android MVP Implementation Plan

[ภาษาไทย](03_ANDROID_MVP_PLAN.md) | [Index](../README.en.md)

This is a historical implementation plan, not completion evidence. See [current acceptance status](../logs/ACCEPTANCE_STATUS.en.md).

## Goal

Deliver a buildable Android 11+ Kotlin/Compose application (`app.easynoty.android`) that captures allowlisted notifications, persists immutable snapshots and per-PC delivery state, and synchronizes them securely to paired Windows peers over LAN WSS.

## Work packages

1. **Project foundation**
   - Configure Gradle wrapper, Android application module, Compose, Room, WorkManager, OkHttp, Kotlin serialization, and unit/instrumentation test dependencies.
   - Target API 36 with minSdk 30 and keep all user-facing English text in resources.
2. **Storage and capture pipeline**
   - Define Room entities/DAOs for paired PCs, allowlist, notification events/revisions, assets, and per-PC delivery queues.
   - Implement raw JSON-safe notification capture, generic parsed result, group-summary suppression, ongoing-instance revision tracking, minimal-event fallback, and removal-state closure.
   - Implement content-addressed SHA-256 asset storage with a 25 MiB limit and original/lossless snapshot metadata.
3. **Secure sync and background work**
   - Implement QR payload validation, Android Keystore secret references, certificate pinning, HMAC-signed protocol envelopes, replay-safe IDs, ACK handling, missing-asset transfers, retry state, and pause/revoke semantics.
   - Implement compression selection (Zstd adapter with zlib fallback), 64 KiB chunking, and WorkManager scheduling for Balanced, Realtime, and Battery Saver modes.
4. **Compose UX and Android integration**
   - Build onboarding/status, notification-access launcher, allowlist wizard, pairing scanner abstraction/manual QR input, paired-PC management, sync modes, and settings.
   - Declare NotificationListenerService, camera/network/foreground-service permissions, and a foreground realtime service.
5. **Verification**
   - Cover hashing, HMAC, QR validation, compression thresholds, notification instance rules, and queue behavior with local tests.
   - Run Gradle unit tests and debug APK assembly; record exact toolchain commands, results, limitations, and remaining real-device gates.

## Acceptance boundary

- The APK and unit tests build from the checked-in wrapper using the locally available Android SDK.
- Production capture, persistence, WSS/HMAC synchronization, and service declarations are present; QR camera and real-device behavior remain explicit hardware integration gates if unavailable in this workspace.
- No cloud service, phone dismissal, reply, or Android-side primary archive is introduced.
