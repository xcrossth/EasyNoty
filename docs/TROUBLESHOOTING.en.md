# Troubleshooting

[ภาษาไทย](TROUBLESHOOTING.md) | [Index](../README.en.md)

| Symptom | Check |
|---|---|
| EXE reports missing Windows App Runtime | Install x64 Runtime 2.2; the helper does not install it automatically. Keep all companion files. |
| .NET SDK 10.0.300 not found | Use a .NET 10 SDK feature band at least 10.0.300. `windows/global.json` allows later feature bands within the same major/minor. A runtime is not an SDK. |
| Gradle cannot find Java/SDK | Check `JAVA_HOME/bin/javac.exe` and set the SDK through the environment or ignored `android/local.properties`. |
| `adb devices` is empty/unauthorized | Enable USB debugging, check drivers/cable, and accept the prompt on the phone. |
| Expired QR | Start a new pairing session; lifetime is two minutes. Never publish QR payloads or tokens. |
| PC not discovered | Check same LAN, Wi-Fi client isolation, Private firewall TCP 38492, and mDNS. Multicast may be blocked. |
| No Android notifications | Check Notification Access, allowlist, pause, pending queue, background policy, and Windows notification settings. |
| Pinned icon is stale | Exit and launch the new build; if only the pinned shortcut is stale, unpin and pin it again. |
| Process remains after closing window | This is hide-to-tray behavior. Choose Exit from the tray menu. |
| Window size is not restored | Check storage write permission; corrupt placement or an off-screen rectangle is ignored. |
| Device management/raw viewer menu is missing | These are open implementation items, not machine configuration errors. See project status. |

Reports should include OS, reproduction steps, commit/build, and redacted error text. Never attach `data/`, databases, credentials, raw notifications, or screenshots containing private messages. See [Security](../SECURITY.en.md).
