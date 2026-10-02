# EasyNoty Protocol v1

[ภาษาไทย](README.md) | [Index](../README.en.md)

This document specifies required behavior. It does not certify implementation completeness; see [acceptance status](../logs/ACCEPTANCE_STATUS.en.md), including the unfinished pairing comparison UI.

Protocol v1 is the shared wire contract between the EasyNoty Android client and Windows server. WebSocket JSON messages use the schemas in `schemas/`; asset and large-event bytes are base64 in the v1 JSON framing. Both applications own their language-specific models and must pass these fixtures.

## Version and transport

- Transport is WSS over the local network. Android pins the SHA-256 certificate fingerprint supplied by the QR payload before sending the one-time token.
- `protocol_version` is the integer `1`. An unsupported version is rejected with `unsupported_version`; there is no best-effort field downgrade.
- JSON is UTF-8. UUIDs are lowercase canonical strings. All timestamps are UTC RFC 3339 strings ending in `Z`.
- Receivers reject unknown envelope or payload fields. New optional fields therefore require a protocol version change unless a schema explicitly allows extension data.

The common envelope is defined by `schemas/envelope.schema.json`:

```json
{
  "protocol_version": 1,
  "message_id": "uuid",
  "message_type": "notification_event",
  "device_id": "uuid",
  "sent_at_utc": "2026-07-05T06:10:01Z",
  "session_id": "uuid-or-null",
  "payload": {},
  "authentication": null
}
```

`hello`, pairing, and auth-handshake messages may have a null session/authentication. Every other message requires a non-null `session_id` and `authentication`. A receiver should allow limited clock skew for diagnostics, but replay protection is based on IDs rather than the wall clock alone.

## Pairing and session authentication

1. Windows creates a pairing session that expires after two minutes and displays `pairing.schema.json#/$defs/qr`. The QR includes every usable IPv4/IPv6 endpoint, PC identity, WSS certificate fingerprint, and a one-time token.
2. Android connects only after the WSS certificate matches the QR fingerprint, then sends `pairing_request`.
3. Both screens show the six-digit verification code. Windows waits for an explicit Accept action before returning `pairing_accept`.
4. `pairing_accept.pairing_secret` is exactly 32 random bytes encoded as unpadded base64url and is exposed only inside the pinned WSS channel. It is stored through Android Keystore and Windows DPAPI. Logs must redact it, the pairing token, notification content, and signatures.
5. On reconnect Windows sends `auth_challenge`. Android creates a fresh client nonce and computes:

```text
auth_proof = hex(HMAC-SHA256(pairing_secret,
  UTF8("auth-v1\n" + challenge_id + "\n" + server_nonce + "\n" +
       client_nonce + "\n" + android_device_id)))
```

Windows verifies the proof, rejects expired/used challenges, and returns a fresh `session_id` in `auth_success`.

## Canonical JSON, hashes, and message HMAC

Canonical JSON means RFC 8785 JSON Canonicalization Scheme (JCS), encoded as UTF-8. Implementations must not hash a serializer's default pretty-printed output. Binary asset hashes always cover the original canonical asset bytes, never compressed transport bytes.

```text
payload_sha256 = "sha256:" + lowercase_hex(SHA-256(JCS(payload)))

signing_input = UTF8(
  protocol_version + "\n" +
  message_id + "\n" +
  message_type + "\n" +
  device_id + "\n" +
  sent_at_utc + "\n" +
  session_id + "\n" +
  payload_sha256)

authentication.signature = lowercase_hex(HMAC-SHA256(pairing_secret, signing_input))
```

The authentication object itself is excluded from the digest. Verify `payload_sha256` and the HMAC with constant-time comparison before processing the payload. `vectors/hmac-sha256.json` is the normative cross-language test vector. Authenticated golden fixtures use that vector's test-only secret; it must never be copied into an application as a production key.

The notification `raw_snapshot_sha256` and `raw_snapshot_byte_size` cover `UTF8(JCS(raw_snapshot))`. Raw snapshots are immutable after persistence.

## Message catalog

| Message | Direction | Payload schema |
|---|---|---|
| `hello` | both | `auth.schema.json#/$defs/hello` |
| `pairing_request` | Android → Windows | `pairing.schema.json#/$defs/request` |
| `pairing_accept`, `pairing_reject` | Windows → Android | pairing definitions |
| `auth_challenge`, `auth_success` | Windows → Android | auth definitions |
| `auth_response` | Android → Windows | auth definition |
| `notification_event` | Android → Windows | `notification.schema.json` |
| `event_transfer_start/chunk/complete` | Android → Windows | event-transfer definitions |
| `event_ack` | Windows → Android | `ack.schema.json#/$defs/event` |
| `asset_missing_request` | Windows → Android | asset definition |
| `asset_transfer_start/chunk/complete` | Android → Windows | asset definitions |
| `asset_ack` | Windows → Android | `ack.schema.json#/$defs/asset` |
| `ping`, `pong`, `error` | both | control definitions |

Group summaries are archived for debugging/re-parse but never create a toast, Inbox row, or unread count. A normal post creates a new `notification_instance_id`; ongoing/progress/media/service updates reuse the instance and increment `revision_index`. Every revision has a unique stable `event_id` and immutable raw snapshot. Android notification removal closes its local active instance only and does not send deletion to Windows.

## Compression and framing

Compression is attempted on canonical bytes, first Zstd then zlib if Zstd is unavailable or fails. Supported Zstd levels are 6, 9, and 12 (default 9). The candidate is used only when it saves at least the configured threshold: 5%, 10% (default), 15%, or 20%; otherwise `content_encoding` is `none`.

### Large notification events/raw

- A `notification_event` whose canonical encoded payload is at most 2 MiB may be sent inline.
- If the selected transport representation exceeds 2 MiB, send `event_transfer_start`, ordered chunks, and `event_transfer_complete` instead. The reconstructed canonical JSON must validate as a `notification_event` payload.
- Each chunk carries at most 65,536 decoded bytes. `chunk_index` starts at zero, `offset` is contiguous, and exactly `chunk_count` chunks must arrive.
- The receiver concatenates encoded chunks, checks `encoded_size`, decompresses according to `content_encoding`, checks `canonical_size` and `canonical_sha256`, then parses and validates JSON.
- Raw notification data has no application hard limit. Serialization/storage failures produce a minimal notification event with `minimal_capture_error`; Windows may display an oversized badge above 2 MiB.

### Assets

- `asset_hash` is SHA-256 of original canonical bytes and assets are limited to 25 MiB canonical size each.
- Canonical asset size below 64 KiB uses one `asset_transfer_start` with `chunk_count: 0`, `chunk_size: 0`, and `inline_data_b64`.
- Canonical asset size of 64 KiB or more uses start/chunk/complete with `inline_data_b64: null` and chunks of at most 65,536 decoded bytes.
- Windows decompresses, verifies canonical size and hash, durably stores the original bytes, and only then sends `asset_ack`.
- Disabling image sync does not disable the `app_icon` role.

Incomplete, duplicate-conflicting, out-of-order, overlapping, oversized, or hash-mismatched transfers are discarded and receive a structured error. Partial transfer state should expire locally and must never be treated as a stored asset/event.

## Delivery, replay, and idempotency

Delivery is at-least-once. Android persists work before sending and retries with the same `event_id`; transfer retries reuse their logical event/asset identity. A transport retry may reuse `message_id` only when every signed byte is identical.

- Cache processed `message_id` values per device/session. An identical duplicate returns the cached response; the same ID with different signed content is `replay_detected`.
- Enforce a unique `(device_id, event_id)` persistence key. Duplicate notification delivery returns the original successful `event_ack` with `duplicate: true` and creates no new revision.
- `event_ack(status=persisted)` is sent only after the logical item, immutable revision/raw, parsed result, and asset mappings commit in one durable DB transaction.
- `asset_ack(status=stored_verified)` is sent only after decompression, SHA-256 verification, and durable asset storage complete.
- Missing or failed ACK leaves Android delivery pending. A paused but non-revoked PC retains backlog; a revoked PC is removed from future delivery obligations.

## Limits and receiver behavior

| Item | Limit/default |
|---|---|
| Pairing session | 2 minutes |
| Event inline/framed boundary | 2 MiB selected transport representation |
| Chunk decoded data | 65,536 bytes |
| Asset canonical bytes | 25 MiB |
| Small/chunked asset boundary | 64 KiB canonical bytes |
| Assets per notification | 64 |
| Zstd level | 9 default; 6/9/12 allowed |
| Compression savings | 10% default; 5/10/15/20% allowed |

Validate in this order: JSON parse → envelope schema → protocol/session/device/replay → payload hash/HMAC → message-specific schema → semantic size/hash/chunk rules → durable processing. Return `error` without crashing or ACKing success when validation/persistence fails.

## Running contract verification

Python 3.10+ is the only requirement; no third-party package is installed or downloaded.

```powershell
python protocol/scripts/verify.py
# or
./protocol/scripts/verify.ps1
```

The verifier resolves every schema reference, requires a payload schema for every envelope message type, accepts all `fixtures/valid`, rejects all `fixtures/invalid`, checks semantic invariants, and verifies the canonical JSON/HMAC vector.
