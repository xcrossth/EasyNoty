# EasyNoty

[ภาษาไทย](README.md)

![EasyNoty icon](assets/branding/easynoty-icon-dark-v3-64.png)

EasyNoty sends notifications from explicitly selected Android apps to Windows over a LAN. Windows is the primary inbox and archive. The MVP has no cloud relay.

This **monorepo** contains the Windows app, Android app, shared protocol, tests, branding, and development documentation.

## Status

The MVP is under development. Source and experimental builds exist, but **full acceptance has not passed**. Some Windows services lack UI integration, and physical-device end-to-end testing is outstanding. Read [status and open work](logs/ACCEPTANCE_STATUS.en.md) before testing.

The application UI is English. Documentation defaults to Thai; every Markdown page has a separate `.en.md` edition and reciprocal language links.

## Getting started

```powershell
git clone https://github.com/xcrossth/EasyNoty.git
cd EasyNoty
```

Read [Getting started](docs/GETTING_STARTED.en.md), then build using the [Windows](windows/README.en.md) and [Android](android/README.en.md) guides. Use synthetic notifications during testing.

The repository contains source, not EXE/APK outputs, notification databases, keys, pairing state, or machine-specific settings.

## Structure

| Directory | Purpose |
|---|---|
| `windows/` | C# / .NET 10 / WinUI 3 desktop app, receiver, SQLite, setup helper, Android mock, tests |
| `android/` | Kotlin / Compose: Notification Access, allowlist, Room queues, QR pairing, sync |
| `protocol/` | Shared Protocol v1 schemas, fixtures, HMAC vector |
| `assets/branding/` | Approved icon source and design drafts |
| `scripts/` | Project and bilingual documentation verification |
| `docs/` | Getting started and troubleshooting |
| `plans/` | Integration and component plans |
| `logs/` | Development and acceptance records, not users' notification logs |

Android captures notifications → persists its queue → sends over paired WSS → Windows validates and stores before ACK → Inbox/toast presentation. Assets are sent when Windows lacks their hash.

## Documentation index

| Topic | English | ภาษาไทย |
|---|---|---|
| Getting started | [Read](docs/GETTING_STARTED.en.md) | [อ่าน](docs/GETTING_STARTED.md) |
| Windows | [Read](windows/README.en.md) | [อ่าน](windows/README.md) |
| Android | [Read](android/README.en.md) | [อ่าน](android/README.md) |
| Troubleshooting | [Read](docs/TROUBLESHOOTING.en.md) | [อ่าน](docs/TROUBLESHOOTING.md) |
| Contributing and Git | [Read](CONTRIBUTING.en.md) | [อ่าน](CONTRIBUTING.md) |
| Security | [Read](SECURITY.en.md) | [อ่าน](SECURITY.md) |
| Status and open work | [Read](logs/ACCEPTANCE_STATUS.en.md) | [อ่าน](logs/ACCEPTANCE_STATUS.md) |
| System overview | [Read](00_SYSTEM_OVERVIEW.en.md) | [อ่าน](00_SYSTEM_OVERVIEW.md) |
| Android specification | [Read](01_ANDROID_APP_SPEC.en.md) | [อ่าน](01_ANDROID_APP_SPEC.md) |
| Windows specification | [Read](02_WINDOWS_APP_SPEC.en.md) | [อ่าน](02_WINDOWS_APP_SPEC.md) |
| Master plan | [Read](03_EASYNOTY_IMPLEMENTATION_PLAN.en.md) | [อ่าน](03_EASYNOTY_IMPLEMENTATION_PLAN.md) |
| Protocol v1 | [Read](protocol/README.en.md) | [อ่าน](protocol/README.md) |
| Integration plan | [Read](plans/00_INTEGRATION_PLAN.en.md) | [อ่าน](plans/00_INTEGRATION_PLAN.md) |
| Protocol plan | [Read](plans/01_PROTOCOL_V1_PLAN.en.md) | [อ่าน](plans/01_PROTOCOL_V1_PLAN.md) |
| Windows plan | [Read](plans/02_WINDOWS_MVP_PLAN.en.md) | [อ่าน](plans/02_WINDOWS_MVP_PLAN.md) |
| Android plan | [Read](plans/03_ANDROID_MVP_PLAN.en.md) | [อ่าน](plans/03_ANDROID_MVP_PLAN.md) |
| Main worklog | [Read](logs/MAIN_WORKLOG.en.md) | [อ่าน](logs/MAIN_WORKLOG.md) |
| Protocol worklog | [Read](logs/subagents/protocol_v1.en.md) | [อ่าน](logs/subagents/protocol_v1.md) |
| Windows worklog | [Read](logs/subagents/windows_mvp.en.md) | [อ่าน](logs/subagents/windows_mvp.md) |
| Android worklog | [Read](logs/subagents/android_mvp.en.md) | [อ่าน](logs/subagents/android_mvp.md) |
| Branding assets | [Read](assets/branding/README.en.md) | [อ่าน](assets/branding/README.md) |

Specifications `00`–`02` are original design drafts. The master plan resolves later decisions; `protocol/schemas/` defines wire fields and rules. A specification entry is not a completion claim.

## Verification

```powershell
python scripts/check_docs.py
.\scripts\verify.ps1
```

Python 3.10+, the .NET 10 SDK, a JDK, and the Android SDK are required as described in the guides. `verify.ps1` validates the protocol, restores/builds/tests Windows, and runs Android unit tests and APK assembly. Use `-SkipAndroid` or `-SkipWindows` for partial checks.

No source-code license has been selected. A public repository does not automatically grant an MIT or other open-source license.
