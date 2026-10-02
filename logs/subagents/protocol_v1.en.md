# Protocol v1 worklog

[ภาษาไทย](protocol_v1.md) | [Index](../../README.en.md)

This audit describes 5 July 2026, before contract integration fixes. The findings and historical line references in the Thai record are not assertions that all issues remain today. See [current acceptance](../ACCEPTANCE_STATUS.en.md).

## 5 July 2026

Read all three specifications and the master plan; scoped work to protocol/, its plan, and this log. Documented decisions before implementation. Added nine JSON Schema Draft 2020-12 files covering common/envelope/pairing/auth/notification/event transfer/assets/ACK/control, valid/invalid golden fixtures, and an HMAC canonicalization vector.

Added a dependency-free Python verifier and PowerShell wrapper for references, schema subsets, message mapping, post-session authentication, raw hash/size, inline assets, and cryptographic vectors. Wrote the WSS/pinning/auth/JCS/HMAC/compression/idempotency/ACK/limits contract.

The first verification found a vector based on literal backslash-n rather than real newlines; corrected it. Authenticated fixtures then used real hashes/HMAC with the synthetic vector secret, and an invalid HMAC fixture was added. Python and PowerShell verifier results passed 9 schemas, 9 valid, 9 invalid, 1 HMAC vector. Python byte-compilation also passed. No Windows/Android/source-plan edits were made by that workstream.

## Historical integration audit: P0 mismatches

A read-only comparison found the two apps could not yet interoperate under v1:

1. **Envelope/auth/HMAC:** both used a standalone signature rather than the authentication object, hashed non-JCS serializer output, omitted protocol version from signing input, used a hash without its prefix, and encoded signatures differently. Windows timestamp round-tripping risked Z/offset/fraction differences. Required schema-shaped envelopes, unchanged signed timestamp strings, JCS, and the shared vector.
2. **Pairing routes/QR/secret ownership:** Windows used POST /pair and /ws, Android used /easynoty/pair and /easynoty/sync. QR fields/endpoints/fingerprint/token shapes differed. Android generated and sent a shared_secret while Windows independently generated another. Responses lacked required key/nonce/time/name fields and used incompatible encodings. Required unified routes/schema shapes and a Windows-generated 32-byte secret imported through pinned WSS after explicit acceptance.
3. **Device/session handshake:** Android used ANDROID_ID instead of a canonical app UUID, minted its own session ID, and sent a non-contract hello. Windows lacked hello/auth/event-transfer handlers. Required persisted UUIDs, complete challenge/proof flow, and server-issued session IDs.
4. **Notification JSON:** raw size/hash field names, schema version, style, parsed-result fields, and asset metadata differed from the schema. Raw hashing used serializer output instead of JCS. Windows also retained older event/parsed/asset shapes. Required exact DTOs, canonical raw size/hash, and shared fixture coverage.
5. **Framing/ACK:** large-event fields and asset transfer IDs/sizes/counts/offsets/inline semantics differed. ACK statuses/timestamps and asset_hash names did not align. Windows returned unsigned ad-hoc messages and errors outside the catalog. Required schema DTOs, ordered contiguous chunks, inline assets, and authenticated response/error envelopes.
6. **Replay/idempotency:** replay-marking results were ignored, same-ID/different-content was not rejected, cached responses were not returned, and marking occurred before durable payload work. Required digest/response cache by device/session/message, conflict rejection, and transactional event idempotency/ACK.

## Integration gates identified

Both languages must deserialize/validate valid fixtures, reject invalid cases, and reproduce the HMAC vector byte-for-byte. Minimum end-to-end flow: QR → explicit Accept → auth challenge → signed notification → durable ACK → missing asset request → verified asset ACK, using schema field names throughout.
