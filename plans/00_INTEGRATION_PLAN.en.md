# EasyNoty Integration Plan

[ภาษาไทย](00_INTEGRATION_PLAN.md) | [Index](../README.en.md)

This is a historical implementation plan, not completion evidence. See [current acceptance status](../logs/ACCEPTANCE_STATUS.en.md).

## Goal

Integrate the Protocol v1, Windows MVP, and Android MVP workstreams into one reproducible monorepo and prove the cross-platform acceptance path.

## Work sequence

1. Keep the root specifications and master plan authoritative.
2. Land isolated workstreams under `protocol/`, `windows/`, and `android/` with their own plans and logs.
3. Reconcile generated C# and Kotlin protocol models against the checked-in schemas and golden fixtures.
4. Add a root verification entrypoint that runs protocol validation, Windows restore/build/test, and Android unit/build checks.
5. Exercise the Windows receiver with the mock Android client before attempting the physical Android path.
6. Connect the real Android device through ADB, install the debug APK, pair over LAN, and validate notification delivery, persistence, assets, toast activation, pause/retry, and privacy behavior.
7. Record any device-only acceptance evidence and remaining environment limitations in the main worklog.

## Integration gates

- All source projects restore and compile from a clean checkout.
- Automated unit/contract tests pass.
- Protocol fixtures deserialize on both platforms.
- Windows persists before ACK and deduplicates retries.
- Android queues while offline/paused and retries after reconnect.
- No default diagnostic output contains notification content or secrets.
- The app processes run without elevation; only the setup helper may request UAC.

## Ownership

- Primary agent: repository integration, root verification, end-to-end testing, acceptance audit.
- Protocol agent: schemas, fixtures, protocol verifier, protocol log.
- Windows agent: Windows application, mock client, tests, Windows log.
- Android agent: Android application, tests, Android log.
