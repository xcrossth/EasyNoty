# Android MVP subagent work log

[ภาษาไทย](android_mvp.md) | [Index](../../README.en.md)

Historical checkpoint only. CameraX/ML Kit was added later; hardware validation remains outstanding. See [current acceptance](../ACCEPTANCE_STATUS.en.md).

## 2026-07-05

- Read `00_SYSTEM_OVERVIEW.md`, `01_ANDROID_APP_SPEC.md`, `02_WINDOWS_APP_SPEC.md`, and `03_EASYNOTY_IMPLEMENTATION_PLAN.md`.
- Confirmed ownership boundary: only `android/`, this plan, and this log.
- Environment discovery used the locally configured Android SDK; installed platforms were API 34 and `android-36.1`, with Build Tools 35.0.0, 36.1.0, and 37.0.0.
- No JDK 17 installation was found. A locally installed JDK 21 was selected per-command without changing system configuration.
- Created the Android MVP implementation plan before project implementation.
- Added the AGP 9.2 / Gradle 9.4.1 Android project configuration, Compose resources and production manifest declarations.
- Added Room v1 entities/DAO for PCs, allowlist, immutable notification revisions, assets, and per-PC event/asset delivery state.
- Added allowlist-gated `NotificationListenerService` capture with JSON-safe raw snapshots, parsed fallback, minimal-event recovery, ongoing revision tracking, group-summary metadata, app-icon/image asset extraction, SHA-256 content addressing, and 25 MiB rejection.
- Added zstd→zlib compression policy, 64 KiB chunking, QR payload validation, Android-Keystore-wrapped 256-bit pair secrets, certificate-fingerprint-pinned WSS pairing/sync, signed HMAC envelopes, ACK/retry handling, WorkManager scheduling, NSD discovery, and realtime foreground service.
- Added Compose status/onboarding, allowlist, pairing/backfill, paired-PC pause/revoke, and sync-mode/settings screens. Camera scanning is represented by a production adapter seam with manual QR payload fallback pending a real camera/device integration gate.
- Added local tests for hashing, compression fallback/threshold, chunking, HMAC binding, pairing expiry, notification instance revisions, and Room per-PC ACK behavior.
- Next verification step: generate the Gradle 9.4.1 wrapper and run unit tests plus `assembleDebug` with local JDK 21 selected per command.
- Generated the Gradle 9.4.1 wrapper. The first compile reached Kotlin and reported two local import issues; before retry, the shared Protocol v1 integration audit identified P0 wire incompatibilities.
- Reworked Android protocol integration against `protocol/README.md` and all nine schemas: strict QR endpoint objects/token/fingerprint, persistent canonical app UUID, Windows-owned base64url pair secret and key ID, hello→challenge/proof→server-session authentication, RFC 8785 payload/raw canonicalization, schema authentication object and lowercase-hex HMAC, exact notification/ACK field names, and schema-compliant large-event/asset framing.
- Aligned transport paths with the Windows implementation and master plan: pairing uses `/easynoty/pair`; authenticated delivery uses `/easynoty/sync`.
- Added the normative `vectors/hmac-sha256.json` as a Kotlin test resource and a byte-for-byte JCS/hash/signing-input/signature test.
