# EasyNoty for Windows

[ภาษาไทย](README.md) | [Index](../README.en.md)

The x64 unpackaged app uses .NET 10, WinUI 3, and Windows App SDK 2.2. It stores history in SQLite and receives Protocol v1 over WSS as a normal user. This is an experimental build; read [open work](../logs/ACCEPTANCE_STATUS.en.md).

## Prerequisites

- x64 Windows 10 build 19041 or newer.
- .NET 10 SDK: `global.json` specifies 10.0.300 and allows later .NET 10 feature bands.
- Windows SDK and WinUI build tools required by Visual Studio/Windows App SDK.
- Windows App Runtime 2.2 to launch the app, plus .NET 10 Desktop/ASP.NET Core runtimes required by its dependencies; development SDK installations include the main runtimes.

## Build and test

From the repository root:

```powershell
.\windows\scripts\test.ps1
.\windows\scripts\build.ps1
```

Alternatively, `.\scripts\verify.ps1 -SkipAndroid` validates the protocol and Windows together. The build script restores/builds the solution and publishes the setup helper.

Desktop output is under `windows/src/EasyNoty.Desktop/bin/Release/net10.0-windows10.0.19041.0/` (a platform directory may appear with different build settings). Run `EasyNoty.Desktop.exe` with all companion files, not a standalone copy of the EXE.

Setup output is under `windows/src/EasyNoty.Setup/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/`. Setup is self-contained; the desktop remains framework-dependent.

## Installation and firewall

Run the desktop without elevation. If Windows reports missing Windows App Runtime, install the x64 2.2 runtime first.

Only the setup helper needs admin rights to add an inbound Private-network TCP `38492` rule. It does not yet check/install Runtime automatically. It accepts `--port <number>`; changing the helper's port does not update the receiver's configuration.

Do not expose the receiver to the internet or unnecessarily allow Public networks. See [Security](../SECURITY.en.md).

## Window behavior

- Select Inbox / Archived / All and search.
- Multi-select device and app filters.
- Open an item to read it; use Mark read, Archive, and Delete. Delete is soft deletion.
- Closing the window hides it to the tray; the tray menu contains Open and Exit.
- Size/position are persisted in `window-placement.json`; the bottom status bar cannot be dismissed.
- The EXE, window/taskbar, and tray use the same icon.

Some specified features still lack UI: rename/pause/revoke, sensitive-app settings, raw viewer/re-parse, and storage migration. Toast activation still clears filters, and the pairing-code comparison UI is incomplete.

## Local data

Storage uses `<AppFolder>/data` when writable, otherwise `%LocalAppData%/EasyNoty`. The UI displays the actual location. This includes databases, assets, logs, and keys; never commit it. Deleting a notification does not physically delete assets.

## Mock Android

The CLI in `src/EasyNoty.MockAndroid/` supports:

```text
pair <wss://host:port> <pairing-token> <pc-uuid> <sha256:fingerprint> [android-uuid]
send normal|ongoing|group|large|duplicate|malformed
asset <file> [mime]
offline
```

Pairing requires values from a pairing session and explicit Windows acceptance. The current UI displays a QR but has no copy-JSON button, so this is not yet a convenient complete testing path. Never paste tokens into issues or committed scripts.

The mock stores a secret in `mock-android-state.json` in its working directory. That file is ignored and is development-only, not a production credential-storage example. The offline command simply skips connecting; it does not prove retry behavior.
