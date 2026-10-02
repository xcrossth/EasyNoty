# Contributing to EasyNoty

[ภาษาไทย](CONTRIBUTING.md) | [Index](README.en.md)

## Structure and scope

Work in this single monorepo. Read the [master plan](03_EASYNOTY_IMPLEMENTATION_PLAN.en.md) and [current status](logs/ACCEPTANCE_STATUS.en.md) before changing behavior. A specification requirement is not evidence of an implemented feature.

Wire changes must update schemas, fixtures, C#, and Kotlin together and validate both platforms. Do not silently change Protocol v1 semantics.

## Git identity

Configure your own GitHub login/noreply address locally before committing. Use the actual address from GitHub Settings → Emails, not a personal Gmail address.

```powershell
git config --local user.name "YOUR_GITHUB_LOGIN"
git config --local user.email "YOUR_GITHUB_NOREPLY_EMAIL"
git config --local user.useConfigOnly true
git var GIT_AUTHOR_IDENT
git var GIT_COMMITTER_IDENT
```

Check both author and committer, including environment overrides. Never embed tokens in remote URLs; use `gh auth login`/the normal credential manager.

## Bilingual documentation

`NAME.md` is Thai and `NAME.en.md` is English. Keep meaning aligned, add reciprocal language links, and update both indexes when adding guides. Preserve historical specifications with notes explaining superseding plan/contract decisions.

## Before submitting

```powershell
python scripts/check_docs.py
.\scripts\verify.ps1
git diff --check
git diff --cached --stat
git diff --cached
```

If Gitleaks is available, run `gitleaks git --redact` after committing and inspect staged files before committing as described by the tool. Never print secrets in logs. Any false-positive allowance must be limited to reviewed test files/values.

Do not commit builds, runtime data, credentials, or machine-specific files; see [Security](SECURITY.en.md). Mark physical-device/LAN checks as untested unless they actually ran.

No source license has been selected. Do not add a LICENSE or grant rights on the owner's behalf without agreement.
