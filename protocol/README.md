# Protocol v1 ของ EasyNoty

[English](README.en.md) | [สารบัญ](../README.md)

นี่คือ contract ที่ Android และ Windows ต้องทำตาม ไม่ใช่การยืนยันว่าทุก flow ผ่านการตรวจรับ ดู [สถานะ](../logs/ACCEPTANCE_STATUS.md) รวมถึงงานค้างหน้าจอเทียบรหัส pairing

Schema อยู่ใน `schemas/` โมเดล C#/Kotlin แยกกันแต่ต้องผ่าน fixtures เดียวกัน ข้อความ WebSocket เป็น JSON; asset และ large-event bytes ใช้ base64 ใน frame ของ v1

## เวอร์ชันและ transport

- WSS ภายใน LAN Android ตรวจ certificate SHA-256 fingerprint จาก QR ก่อนส่ง one-time token
- `protocol_version` เป็น integer 1 หากไม่รองรับให้ตอบ `unsupported_version` ไม่ลด field แบบคาดเดา
- JSON UTF-8, UUID lowercase canonical, timestamp RFC 3339 UTC ลงท้าย Z
- ปฏิเสธ field ที่ไม่รู้จัก เว้น schema เปิด extension ไว้ชัดเจน การเพิ่ม optional field โดยทั่วไปจึงต้องเปลี่ยนเวอร์ชัน

Envelope ตาม `schemas/envelope.schema.json`:

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

ตัวอย่างนี้เป็นรูปแบบแนวคิด ค่า ID ต้องเป็น UUID จริงตาม schema ส่วน hello/pairing/auth-handshake ใช้ null session/auth ได้ ข้อความอื่นต้องมีทั้งสองส่วน Clock skew อาจยอมรับเล็กน้อย แต่ replay protection อิง ID ไม่ใช่นาฬิกาอย่างเดียว

## Pairing และ session

1. Windows สร้าง session อายุ 2 นาทีและ QR ที่มี IPv4/IPv6 endpoints, PC identity, fingerprint และ token ใช้ครั้งเดียว
2. Android ตรวจ certificate ก่อนส่ง pairing_request
3. ทั้งสองหน้าจอต้องแสดงรหัส 6 หลัก และ Windows ต้องรอ Accept โดยผู้ใช้
4. Windows สร้าง pairing secret 32 random bytes ส่งเป็น unpadded base64url ภายใน pinned WSS เก็บผ่าน DPAPI/Keystore ห้าม log secret/token/content/signature
5. เมื่อ reconnect Windows ส่ง auth_challenge Android สร้าง client nonce ใหม่และคำนวณ proof:

```text
auth_proof = hex(HMAC-SHA256(pairing_secret,
  UTF8("auth-v1\n" + challenge_id + "\n" + server_nonce + "\n" +
       client_nonce + "\n" + android_device_id)))
```

Windows ตรวจ proof ปฏิเสธ challenge หมดอายุ/ใช้แล้ว และส่ง session_id ใหม่ใน auth_success

## Canonical JSON, hash และ HMAC

Canonical JSON หมายถึง RFC 8785 JCS แบบ UTF-8 ห้าม hash pretty-printed/default serializer output Asset hash อิง canonical bytes ต้นฉบับ ไม่ใช่ compressed bytes

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

ไม่รวม authentication object ใน digest ตรวจ hash/HMAC แบบ constant-time ก่อนประมวลผล `vectors/hmac-sha256.json` เป็น normative vector และ secret ในนั้นเป็น test-only ห้ามใช้จริง Raw hash/size อิง UTF8(JCS(raw_snapshot)) และ raw ต้อง immutable หลังบันทึก

## กลุ่มข้อความ

| ข้อความ | ทิศทาง | Schema |
|---|---|---|
| hello | ทั้งสอง | auth |
| pairing_request | Android → Windows | pairing request |
| pairing_accept / pairing_reject | Windows → Android | pairing |
| auth_challenge / auth_success | Windows → Android | auth |
| auth_response | Android → Windows | auth |
| notification_event | Android → Windows | notification |
| event_transfer_start/chunk/complete | Android → Windows | event-transfer |
| event_ack | Windows → Android | ack event |
| asset_missing_request | Windows → Android | asset |
| asset_transfer_start/chunk/complete | Android → Windows | asset |
| asset_ack | Windows → Android | ack asset |
| ping / pong / error | ทั้งสอง | control |

Group summaries เก็บสำหรับ debug/re-parse แต่ไม่แสดง toast/Inbox/unread Normal post สร้าง notification_instance_id ใหม่ ส่วน ongoing/progress/media/service ใช้ instance เดิมและเพิ่ม revision_index ทุก revision มี event_id คงที่และ raw immutable การลบบน Android ปิด local instance ไม่ส่งคำสั่งลบ Windows

## Compression และ framing

ลอง Zstd ก่อน หากไม่มี/ล้มเหลวใช้ zlib Level 6/9/12 เริ่ม 9 ใช้ compressed เมื่อประหยัดถึง threshold 5/10/15/20% เริ่ม 10% มิฉะนั้น encoding none

### Event/raw ขนาดใหญ่

- Payload canonical ไม่เกิน 2 MiB ส่ง notification_event inline ได้
- หาก transport representation ที่เลือกเกิน 2 MiB ให้ส่ง start/chunk/complete แล้วประกอบกลับเป็น notification_event payload
- Chunk decoded ไม่เกิน 65,536 bytes, index เริ่ม 0, offset ต่อเนื่อง และจำนวนตรง chunk_count
- รวม encoded chunks ตรวจ encoded_size → decompress → ตรวจ canonical_size/hash → parse/validate
- Raw ไม่มี application hard limit; serialization/storage failure ต้องเก็บ minimal event พร้อม minimal_capture_error และอาจแสดง oversized badge เมื่อเกิน 2 MiB

### Assets

- hash อิง original canonical bytes; จำกัด canonical size 25 MiB
- ต่ำกว่า 64 KiB ใช้ asset_transfer_start เดียว มี chunk_count/chunk_size เป็น 0 และ inline_data_b64
- ตั้งแต่ 64 KiB ใช้ start/chunk/complete, inline_data_b64 เป็น null และ chunk สูงสุด 65,536 bytes
- ตรวจ decompress/size/hash และเก็บสำเร็จก่อน asset_ack
- ปิด image sync แล้วยังคง app_icon

Transfer ไม่ครบ/ซ้ำแต่ข้อมูลขัดกัน/ผิดลำดับ/ทับ offset/เกิน limit/hash ผิดต้องทิ้งและตอบ structured error Partial state ต้องหมดอายุและไม่ถือว่าเก็บสำเร็จ

## Delivery, replay และ idempotency

เป็น at-least-once Android persist ก่อนส่ง retry ใช้ event_id เดิม Retry transfer รักษา logical event/asset identity และ reuse message_id ได้เฉพาะ signed bytes เหมือนเดิมทั้งหมด

- Cache message_id ต่อ device/session คืน cached response เมื่อซ้ำเหมือนเดิม และตอบ replay_detected เมื่อ ID เดิมแต่เนื้อหาต่าง
- unique (device_id,event_id) ป้องกันซ้ำ คืน ACK เดิมพร้อม duplicate true โดยไม่เพิ่ม revision
- event_ack persisted หลัง logical item/raw/revision/parsed/asset mappings commit ใน transaction เดียว
- asset_ack stored_verified หลัง decompress/SHA-256/durable storage
- ไม่ได้รับ ACK ยังคง pending; pause เก็บ backlog; revoke ยกเลิก delivery obligations ของคู่นั้น

## ขอบเขตและลำดับตรวจ

| รายการ | ค่า |
|---|---|
| Pairing expiry | 2 นาที |
| Event inline/framed | 2 MiB transport representation |
| Chunk | 65,536 decoded bytes |
| Asset | 25 MiB canonical bytes |
| Asset inline/chunked | 64 KiB |
| Assets ต่อ notification | 64 |
| Zstd | default 9; 6/9/12 |
| Savings threshold | default 10%; 5/10/15/20% |

ตรวจ JSON parse → envelope schema → protocol/session/device/replay → payload hash/HMAC → payload schema → semantic size/hash/chunk → durable processing ผิดให้ตอบ error ไม่ crash และไม่ ACK success

## ตรวจ contract

ใช้ Python 3.10+ ไม่ติดตั้ง dependency เพิ่ม:

```powershell
python protocol/scripts/verify.py
# หรือ
.\protocol\scripts\verify.ps1
```

Verifier ตรวจ schema refs, payload mapping, valid/invalid fixtures, semantic invariants และ canonical JSON/HMAC vector แต่ไม่ได้ทดสอบ runtime network ทั้งสองแอปแทน integration gate
