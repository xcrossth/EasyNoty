# บันทึกงาน Protocol v1

[English](protocol_v1.en.md) | [สารบัญ](../../README.md)

นี่คือ audit ณ 5 กรกฎาคม 2026 ก่อนแก้ contract integration เลขบรรทัดและข้อผิดพลาดด้านล่างเป็นหลักฐานในอดีต ไม่ใช่ข้อสรุปว่าโค้ดปัจจุบันยังผิดทั้งหมด ดู [สถานะปัจจุบัน](../ACCEPTANCE_STATUS.md)

## 2026-07-05

- อ่าน `00_SYSTEM_OVERVIEW.md`, `01_ANDROID_APP_SPEC.md`, `02_WINDOWS_APP_SPEC.md` และ `03_EASYNOTY_IMPLEMENTATION_PLAN.md` ครบถ้วน
- ล็อกขอบเขตเฉพาะ `protocol/`, `plans/01_PROTOCOL_V1_PLAN.md` และไฟล์ log นี้
- สรุป contract decisions และบันทึกแผนย่อยก่อนเริ่ม implementation
- เพิ่ม JSON Schema Draft 2020-12 จำนวน 9 ไฟล์ ครอบคลุม common types, envelope, pairing, auth, notification, large-event transfer, assets, ACK และ control/error
- เพิ่ม golden fixtures ฝั่ง valid/invalid และ HMAC-SHA256 canonicalization test vector
- เพิ่ม dependency-free verifier (`protocol/scripts/verify.py`) พร้อม PowerShell wrapper; verifier ตรวจ `$ref`, schema subset, payload schema mapping, post-session authentication, raw snapshot hash/size, inline asset invariants และ cryptographic vector
- สถานะ checkpoint: เตรียมเขียน protocol README และจะรัน verifier หลังตรวจ contract consistency
- เขียน `protocol/README.md` ล็อก WSS/pinning, pairing/auth proof, RFC 8785 canonicalization, HMAC signing input, compression/framing, idempotency/replay, ACK timing, limits และ validation order
- การ verify รอบแรกพบ HMAC vector mismatch เพราะค่าคาดหมายเดิมคำนวณจากตัวอักษร `\\n` แทน newline จริง; แก้ signature vector แล้ว
- Hardening เพิ่ม: authenticated valid fixtures ทุกไฟล์ใช้ payload hash/HMAC จริงจาก test-only secret ใน vector และเพิ่ม invalid fixture สำหรับ bad HMAC
- ผลตรวจสุดท้าย:
  - `python protocol/scripts/verify.py` → `PASS: 9 schemas, 9 valid fixtures, 9 invalid fixtures, 1 HMAC vector`
  - `./protocol/scripts/verify.ps1` → PASS ด้วยผลเดียวกัน
  - `python -m py_compile protocol/scripts/verify.py` → exit code 0 (ลบ generated `__pycache__` หลังตรวจแล้ว)
- งาน Protocol v1 ตามขอบเขตเสร็จสมบูรณ์; ไม่มีการแก้ `windows/`, `android/`, root specs หรือ master plan

## ตรวจ integration — 2026-07-05

ตรวจ current state ของ Windows/Android แบบ read-only เทียบ `protocol/schemas/` และ `protocol/README.md` แล้ว สรุปว่า **สองแอปยังไม่สามารถ interoperate ตาม Protocol v1 ได้**; เป็น P0 contract mismatch ไม่ใช่เพียง optional-field drift

### ข้อไม่ตรงกันระดับ P0

1. **Envelope/authentication คนละ wire shape และ HMAC คนละอัลกอริทึม**
   - Windows `ProtocolEnvelope` มี `signature` string เดี่ยว (`windows/src/EasyNoty.Core/Protocol.cs:21`) และ Android ส่ง `signature` (`.../sync/ProtocolEnvelope.kt:17,27`) แต่ schema บังคับ `authentication={algorithm,key_id,payload_sha256,signature}` พร้อม `session_id`/`authentication` แม้ค่า null
   - ทั้ง Windows (`EasyNoty.Core/Services.cs:130-144`) และ Android (`security/HmacSigner.kt:9-16`) hash serializer output ที่ไม่ใช่ RFC 8785, signing input ขาด `protocol_version`, payload hash ขาด `sha256:` และ signature เป็น Base64 แทน lowercase hex
   - Windows แปลง `sent_at_utc` เป็น `DateTimeOffset` แล้ว canonicalize ด้วย `ToString("O")`; Android sign ข้อความ `Instant` เดิม จึงมีโอกาส `Z` vs `+00:00` และ fractional precision ต่างกันแม้เวลาเดียวกัน
   - Required patch: ใช้ envelope/auth object ตาม schema, เก็บ signed timestamp เป็น wire string, เพิ่ม RFC 8785 implementation และให้ C#/Kotlin ผ่าน `vectors/hmac-sha256.json` ชุดเดียวกัน

2. **Pairing ใช้ route, QR, request และ secret ownership คนละแบบ**
   - Windows เปิด HTTP `POST /pair` และ WSS `/ws` (`Infrastructure/Transport.cs:75,81`) แต่ Android ต่อ WSS `/easynoty/pair` และ `/easynoty/sync` (`pairing/PairingManager.kt:49`, `sync/WebSocketSyncClient.kt:47`) จึงเชื่อมกันไม่ได้
   - Windows QR model เป็น `Token`, `ExpiresAt`, `CertificateFingerprint`, string `endpoints` + root `port` (`PairingService.cs:8`) ขณะที่ schema ต้อง `pairing_token`, `expires_at_utc`, `certificate_sha256`, endpoint objects `{host,port}`; token/fingerprint serialization ปัจจุบันไม่มีรูปแบบ schema (`PairingService.cs:14`)
   - Android parser ยังอ่าน string endpoints + root port และยอม token 16 ตัว (`pairing/Pairing.kt:25-41`) ไม่ตรง schema 32–128 base64url chars
   - Android สร้าง secret เองและส่ง `shared_secret` (`PairingManager.kt:60`) ซึ่งไม่มีใน request schema; Windows ignore secret นั้นแล้วสร้าง secretใหม่ของตน (`PairingService.cs:48`) ทำให้ HMAC key คนละตัวแน่นอน
   - Pairing request/accept models Windows (`Protocol.cs:71-72`) ไม่ตรง required fields; accept ใช้ standard Base64 และ UUID `N` ไม่มีขีด แทน unpadded base64url secret + canonical UUID และขาด `key_id/server_nonce/paired_at_utc/pc_name`
   - Required patch: เลือก route เดียว, ใช้ QR/request/accept definitions ตรง schema, ให้ Windows เป็นผู้สร้าง 32-byte secretหลัง explicit Accept แล้ว Android import secret จาก pinned WSS response; ห้ามส่ง `shared_secret` ใน request

3. **Device/session handshake ไม่ผ่าน schema/security flow**
   - Android ใช้ `ANDROID_ID` เป็น `device_id` ซึ่งไม่ใช่ canonical UUID และสร้าง `session_id` เอง; `hello` payload `{challenge,client}` (`WebSocketSyncClient.kt:51-52`) ไม่ตรง hello schema และไม่มี auth challenge/response/success flow
   - Windowsไม่มี handler สำหรับ `hello`, pairing/auth message enums หรือ `event_transfer_*`; switch รองรับเพียง ping/notification/asset (`Transport.cs:123-143`)
   - Required patch: สร้างและ persist app UUID, implement challenge flow ครบ และใช้ `session_id` ที่ Windows ออกหลัง auth success

4. **Notification model/JSON ไม่ตรง schema**
   - Android ส่ง `raw_byte_size`/`raw_sha256` (`WebSocketSyncClient.kt:148-149`) แทน `raw_snapshot_byte_size`/`raw_snapshot_sha256`, ขาด `raw_snapshot_schema_version` และ required `style`
   - Android parsed result ปกติมี extra `app_name`; minimal parsed result ขาด required title/body/sender/conversation/confidence (`capture/SnapshotBuilder.kt:56-73`) จึงถูก strict schema ปฏิเสธ
   - Raw size/hash คำนวณจาก `JSONObject.toString()` (`SnapshotBuilder.kt:117`) ไม่ใช่ UTF-8 RFC 8785 JCS ตาม contract
   - Windows `NotificationEvent`, `ParsedNotification`, `AssetManifestItem` (`Protocol.cs:32-69`) ใช้ชื่อ/shape เก่า: payload มี `device_id`, `posted_at/captured_at`, `raw_snapshot_json/raw_byte_size/raw_sha256`, `notification_style/android_parsed_result`; parsed resultมี `grouping_key`; asset manifest ขาด required `source_kind/originality_level`
   - Required patch: mirror `notification.schema.json` แบบ exact, canonicalize raw ก่อน size/hash และเพิ่ม contract fixture tests ในทั้งสองโปรเจกต์

5. **Large-event/asset framing และ ACK คนละ fields**
   - Android event transfer ใช้ `algorithm/original_size/sha256/index/data_base64` (`WebSocketSyncClient.kt:98-105`) แทน `content_type/content_encoding/canonical_size/encoded_size/canonical_sha256/chunk_count/chunk_index/offset/data_b64`; Windowsยังไม่มี event-transfer handlers
   - Windows asset records (`Protocol.cs:76-78`) และ Android asset messages (`WebSocketSyncClient.kt:126-133`) ไม่ตรง schema; Androidไม่มี `transfer_id`, Windowsต้องการ `transfer_id`, และทั้งคู่ขาด encoded sizes/count/offset/inline semantics
   - Windows `EventAck`/`AssetAck` (`Protocol.cs:73-74`) ขาด contract status/timestamps และ Androidอ่าน asset field `hash` (`WebSocketSyncClient.kt:116`) แทน `asset_hash`
   - Windowsตอบ anonymous objects เช่น `transfer_ready`, `chunk_ack`, pong และ ACK (`Transport.cs:123-142`) ซึ่งไม่อยู่ใน message enumและไม่ใช่ signed envelope; errors ก็ใช้ code นอก enumและขาด `retryable`
   - Required patch: replace DTOs/handlers ด้วย schemas, implement ordered/contiguous chunk semantics + inline asset path และส่ง response/errorทุกชนิดเป็น authenticated envelope

6. **Replay/idempotency semantics ยังไม่ถูกใช้**
   - Windowsเรียก `TryMarkMessageAsync` แล้วทิ้งผลลัพธ์ (`Transport.cs:119`) จึงประมวลผล duplicate ต่อ, ไม่ตรวจ same-ID/different-content และไม่คืน cached response ตาม contract; message ถูก mark ก่อน durable payload processing ทำให้ retry หลัง persistence failureคลุมเครือ
   - Required patch: cache digest + response ต่อ `(device_id,session_id,message_id)`, reject conflicting replay, return cached identical response และให้ event idempotency/ACK ผูกกับ durable transaction

### เกณฑ์ integration ที่ต้องเพิ่ม

- C# และ Kotlin ต้องอ่าน `protocol/fixtures/valid/*.json` แล้ว deserialize/validateได้ และ reject `fixtures/invalid/*.json`
- ทั้งคู่ต้อง reproduce HMAC vector byte-for-byte
- End-to-end gate ขั้นต่ำ: QR → explicit Accept → auth challenge → signed notification → durable event ACK → missing asset request → verified asset ACK โดยใช้ field names จาก schemas เท่านั้น
