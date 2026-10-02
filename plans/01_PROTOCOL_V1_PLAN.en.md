# Protocol v1 implementation plan

[ภาษาไทย](01_PROTOCOL_V1_PLAN.md) | [Index](../README.en.md)

## Goal and deliverables

Create a shared contract without shared generated models. Deliver JSON Schema Draft 2020-12 definitions for QR, envelopes, authentication, notifications/raw transfer, assets, ACK, keepalive, and errors.

Include valid/invalid golden fixtures, a normative HMAC vector, canonicalization/hash/HMAC/replay/ACK/compression/framing documentation, and a dependency-free Python verifier.

## Decisions

- Envelope: protocol_version, message_id, message_type, device_id, sent_at_utc, session_id, payload, authentication.
- Payload hashes cover UTF-8 JCS; HMAC binds metadata to the hash.
- UUIDs are lowercase canonical; timestamps are RFC 3339 UTC ending in Z; SHA-256 uses sha256: plus lowercase hex.
- Event ACK follows transaction commit; asset ACK follows decompression/hash/persistence; retries preserve identity.
- Assets below 64 KiB are inline JSON; larger assets use start/chunk/complete, at most 65,536 decoded bytes per chunk.
- Event transport above 2 MiB is framed; raw has no application hard limit; assets are capped at 25 MiB.

## Verification

Run `python protocol/scripts/verify.py` to check references/schema subsets, valid/invalid fixtures, semantics, and HMAC. Schema verification is not a substitute for cross-application integration tests.
