# Security and privacy

[ภาษาไทย](SECURITY.md) | [Index](README.en.md)

EasyNoty processes potentially private notification content. This public repository contains source, documentation, design assets, and synthetic test data only.

## Never commit

- Tokens, passwords, API keys, private keys, certificates containing private keys, or Android signing keys.
- `.env`, `local.properties`, credential settings, or `mock-android-state.json`.
- `data/`, SQLite/WAL files, runtime logs, real pairing QR payloads, notification exports, or backups.
- EXE/APK outputs or build artifacts that may include machine-specific data.

`.gitignore` excludes filenames, not secrets embedded in source. Inspect the staged diff and run a secret scanner before pushing.

## Public test material

`protocol/fixtures/`, `protocol/vectors/hmac-sha256.json`, and test constants are synthetic contract/HMAC inputs, not live credentials. Production apps generate random secrets. Never reuse test keys for actual devices.

`.gitleaks.toml` extends the default rules and allows only the repeated-hex token and the HMAC key made from bytes 0–31 in exact test files. Both path and value must match. Entire tests/fixtures directories are not excluded.

## Product limitations

WSS certificate pinning, HMAC, DPAPI, and Android Keystore are implemented in code, but full end-to-end acceptance is incomplete. Pairing-code comparison and parts of the privacy UI remain open. The MVP does not encrypt notification history at rest; soft deletion does not erase disk data or assets.

## Vulnerability reports

Do not open public issues containing secrets, user data, or details of an unpatched exploit. If GitHub Security offers **Report a vulnerability**, use it. Otherwise ask the maintainer for a private channel before sharing details. No personal email is published here, and private reporting is not assumed enabled.

If a credential leaks, revoke/rotate it at its issuer first. Removing it from the latest commit alone does not remove it from Git history.
