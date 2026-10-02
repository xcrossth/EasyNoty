# EasyNoty for Android

[ภาษาไทย](README.md) | [Index](../README.en.md)

The Android 11+ Kotlin/Compose app captures explicitly allowed notifications and sends immutable raw snapshots and assets to Windows over certificate-pinned WSS.

## Build

Use JDK 21, previously tested with this project, Android SDK Platform 36, and the Build Tools required by AGP 9.2. Java source targets 17; Gradle Wrapper is 9.4.1.

Set `JAVA_HOME` and `ANDROID_HOME`/the SDK location. Never commit `local.properties` or signing keys.

```powershell
cd android
.\gradlew.bat testDebugUnitTest assembleDebug
# Optional additional lint check
.\gradlew.bat lintDebug
```

The debug APK is `app/build/outputs/apk/debug/app-debug.apk`, intended for ADB/sideload. Play Store/AAB publishing and release signing are not configured.

## Install

From `android/`:

```powershell
adb devices
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

1. Grant Notification Access.
2. Explicitly enable apps in **Allowed apps**; none are enabled automatically.
3. Use **Scan QR code** with local CameraX/ML Kit, or enter the Windows QR JSON manually.
4. Start in Balanced mode. Realtime uses a foreground service; Battery Saver batches work through WorkManager.
5. Choose new-only delivery or seven-day cache backfill when pairing a PC.

Camera images are not uploaded. See [Getting started](../docs/GETTING_STARTED.en.md) for current pairing limitations.

## Storage and delivery

- Room stores raw snapshots/revisions and independent delivery state per PC.
- Android Keystore protects secrets; certificate pinning and HMAC follow Protocol v1.
- Events/assets finish only after valid ACKs.
- Paused PCs retain backlog; revoking a pair removes its delivery obligations.
- Assets use SHA-256 addressing and a 25 MiB limit; GC respects references and outstanding deliveries.
- Disabling image sync must still deliver app icons as required by the contract.
- Android is a cache/queue, not the primary archive.

## Testing

Local tests cover hashing, compression, notification instances, pairing payloads, HMAC, Room, and protocol fixtures. Test resources come from `../protocol/`, so clone the entire monorepo.

Unit tests and APK assembly do not validate NotificationListenerService, camera behavior, Doze, OEM background restrictions, or delivery to Windows. Follow [physical-device acceptance](../logs/ACCEPTANCE_STATUS.en.md).
