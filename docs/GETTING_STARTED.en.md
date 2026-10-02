# Getting started with EasyNoty

[ภาษาไทย](GETTING_STARTED.md) | [Index](../README.en.md)

This guide covers experimental local builds, not a general release. Read [current limitations](../logs/ACCEPTANCE_STATUS.en.md), especially pairing and settings UI.

## Prerequisites

- x64 Windows 10 build 19041+ or Windows 11, .NET 10 SDK, and Windows App Runtime 2.2.
- Android 11+ with USB debugging, JDK 21 (Java source target is 17), Android SDK Platform 36, and Build Tools required by AGP.
- Python 3.10+ and Git.
- Phone and PC on the same LAN without Wi-Fi client isolation; the receiver defaults to TCP `38492`.

## Build and start Windows

```powershell
git clone https://github.com/xcrossth/EasyNoty.git
cd EasyNoty
python scripts/check_docs.py
.\scripts\verify.ps1
```

Set `JAVA_HOME` and `ANDROID_HOME`/`ANDROID_SDK_ROOT`, or specify the Android SDK in ignored `android/local.properties`. See the [Windows](../windows/README.en.md) and [Android](../android/README.en.md) guides.

Run `EasyNoty.Desktop.exe` from Release with all companion files. The app runs as a normal user. Closing the window hides it to the tray; choose **Exit** to end the process.

If blocked by the firewall, use the setup helper as documented to add a Private-network rule. Only the helper needs elevation; it does not install Runtime automatically yet.

Data uses `data/` beside the app if writable, otherwise `%LocalAppData%/EasyNoty`. Check Settings for the actual path and never upload that data.

## Install and test Android

```powershell
adb devices
adb install -r android/app/build/outputs/apk/debug/app-debug.apk
```

1. Grant Notification Access and explicitly allow a test app. Nothing is allowed automatically.
2. Begin in Balanced mode. Select **Pair device** on Windows, then scan its QR from Android within two minutes.
3. Grant camera permission and select **Accept pending device** on Windows when a device is waiting.
4. Trigger a synthetic notification and inspect the Inbox, toast, and delivery queue.

Limitation: the pre-accept six-digit comparison UI is incomplete. Test only trusted devices on a trusted LAN. This entire flow has not yet been verified on physical hardware.

Check grouped/ongoing notifications, assets, offline retry, Wi-Fi/IP changes, Doze, Realtime, and privacy using the [acceptance list](../logs/ACCEPTANCE_STATUS.en.md). Do not include QR payloads, tokens, or private messages in reports.
