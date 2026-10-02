# Main development worklog

[ภาษาไทย](MAIN_WORKLOG.md) | [Index](../README.en.md)

This is historical context. Use [acceptance status](ACCEPTANCE_STATUS.en.md) for current evidence. Earlier claims that only physical testing remained were corrected on 3 October 2026.

## 5–10 July 2026

- Accepted/saved the EasyNoty master plan; started the monorepo and protocol/Windows/Android workstreams with component plans/logs.
- Original environment: Windows 10 22H2 x64, .NET SDK 10.0.300, Visual Studio 18 Insiders, and a locally configured Android SDK.
- Initialized Git and added the integration plan, without committing at that stage.
- Added root verify.ps1 for protocol, Windows, and Android checks.
- Protocol passed 9 schemas/9 valid/9 invalid/1 HMAC vector.
- Integrated work after workstreams hit usage limits; Android unit tests/APK passed using JDK 21.
- Fixed root verification to run Gradle from the Android directory.
- Added QR dialog, reveal/read behavior, toast routing, hide-to-tray, Open/Exit; Windows build/tests passed.
- Added CameraX/ML Kit and Windows DNS-SD/mDNS with endpoint fallback.
- Combined checks passed: protocol, Windows 11/11, Android unit/build; ADB detected no hardware.
- Earlier notes called this complete, but later review found unfinished UI/flows and acceptance. Those claims are not acceptance evidence.

## 11 July 2026

- Reserved caption-button space for Refresh/Pair device.
- Persisted window size/position and made the status bar non-dismissible.
- Selected the dark-v3 ribbon N icon on navy; keyed/transparent drafts remain as design history.
- Built a multi-size ICO and assigned it to EXE/AppWindow/taskbar/tray. Build passed; visual validation of every shell surface remains a separate check.

## 3 October 2026

- Compared code to the plan: missing Windows UI wiring, incomplete pairing-code flow, toast filter clearing, and no automatic Runtime installation in setup.
- Git inspection found no commits or remote.
- Prepared publication to xcrossth/EasyNoty as requested, using repository-local GitHub noreply author/committer identity.
- Made Thai the default documentation language with a separate English edition for every page, an index, and accurate limitations.
- Expanded ignore rules for runtime secrets/DB/mock state/keys/build/local settings and excluded the duplicate specification archive.
- Allowed .NET 10 SDK feature-band roll-forward and selected JDKs with javac/version at least 17 instead of a machine-specific path.
- Protocol passed; Windows build had zero warnings/errors and 11/11 tests passed. Android unit/build succeeded using up-to-date cache (existing report: 14/14).
- This repository-preparation run did not test physical devices or implement the remaining product features.
