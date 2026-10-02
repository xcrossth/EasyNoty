# EasyNoty Windows MVP — Implementation Plan

[ภาษาไทย](02_WINDOWS_MVP_PLAN.md) | [Index](../README.en.md)

This is a historical implementation plan, not completion evidence. See [current acceptance status](../logs/ACCEPTANCE_STATUS.en.md).

## Goal

Deliver an x64, unpackaged Windows 10/11 application on .NET 10 and WinUI 3, plus a mock Android client, with a tested end-to-end path from authenticated LAN input to durable SQLite history and inbox presentation.

## Work streams

1. **Foundation** — solution, central package versions, Core/Infrastructure/Desktop/Setup/MockAndroid/Test projects, build scripts and English resources.
2. **Domain and storage** — protocol/domain models, EF Core SQLite schema and migration, WAL initialization, portable/local-app-data path resolution, immutable revisions, parsed-result history and content-addressed asset storage.
3. **Application services** — idempotent receive transaction, parser chain, inbox filtering and counts, read/archive/soft-delete, sensitive reveal policy, retention, pairing sessions, HMAC/replay validation and DPAPI-backed secrets.
4. **Transport vertical slice** — Kestrel HTTPS/WebSocket host with pair/device routes, structured ACK/error messages, missing-asset requests, chunk assembly and hash verification; mock Android CLI exercises pairing, HMAC, duplicates, malformed input and notifications.
5. **Windows shell** — basic WinUI inbox/detail/settings/pair-device UI; native toast, tray, startup, session-lock and mDNS adapters with safe fallbacks; setup helper checks runtime/firewall prerequisites without elevating the main app.
6. **Verification** — unit/integration coverage for parsers, revisions/idempotency, filters, sensitive state, retention, HMAC/replay, asset integrity, storage and WebSocket flow; restore/build/test x64 and record limitations/manual gates.

## Acceptance evidence

- `dotnet restore`, `dotnet build -c Release -p:Platform=x64`, and `dotnet test -c Release` pass.
- Duplicate event IDs ACK without duplicate logical/revision rows; ongoing notifications append immutable revisions while normal posts create logical items.
- ACK is produced only after the persistence transaction; invalid signatures/replays and asset hash mismatches are rejected.
- Inbox queries implement Inbox/Archived/All, search and AND-between/multi-select filters; sensitive content remains redacted until an explicit per-item reveal.
- Retention archives after 30 days by default and never hard-deletes; all delete behavior is soft delete.
- Desktop and MockAndroid executables launch; native integrations expose operational status and fallback behavior.
