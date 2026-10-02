# แผน Protocol v1

[English](01_PROTOCOL_V1_PLAN.en.md) | [สารบัญ](../README.md)

## เป้าหมายและผลส่งมอบ

สร้าง contract กลางสำหรับ Android/Windows โดยไม่แชร์ generated models พร้อม JSON Schema Draft 2020-12 สำหรับ QR, envelope, auth, notification/raw transfer, assets, ACK, keepalive และ errors

มี valid/invalid golden fixtures, normative HMAC vector, คู่มือ canonicalization/hash/HMAC/replay/ACK/compression/framing และ Python verifier ที่ไม่ต้องติดตั้ง dependency

## ข้อกำหนด

- Envelope: protocol_version, message_id, message_type, device_id, sent_at_utc, session_id, payload, authentication
- Hash payload จาก UTF-8 JCS และ HMAC ผูก metadata กับ hash
- UUID lowercase canonical, เวลา RFC 3339 UTC ลงท้าย Z, SHA-256 เป็น sha256: + lowercase hex
- Event ACK หลัง transaction commit; asset ACK หลัง decompress/hash/persist; retry ใช้ identity เดิม
- Asset ต่ำกว่า 64 KiB ส่ง inline ใน JSON; ตั้งแต่ 64 KiB แบ่ง start/chunk/complete สูงสุด 65,536 decoded bytes ต่อ chunk
- Event transport เกิน 2 MiB ต้องแบ่ง frame; raw ไม่มี hard limit; asset ไม่เกิน 25 MiB

## ตรวจสอบ

รัน `python protocol/scripts/verify.py` ตรวจ references/schema subset, valid/invalid fixtures, semantic constraints และ HMAC vector การตรวจ schema ผ่านไม่แทน integration test กับสองแอป
