# บันทึกงาน Android MVP

[English](android_mvp.en.md) | [สารบัญ](../../README.md)

เป็นบันทึก checkpoint เดิม ไม่ใช่สถานะล่าสุด ดู [การตรวจรับ](../ACCEPTANCE_STATUS.md) กล้องที่บันทึกเดิมระบุว่ารอ adapter ต่อมามี CameraX/ML Kit แล้ว แต่ยังต้องทดสอบบน hardware

## 5 กรกฎาคม 2026

- อ่านสเปกสามหน้าและแผนหลัก จำกัดขอบเขต android/, แผน Android และบันทึกนี้
- ตรวจ SDK พบ API 34/36.1 และ Build Tools 35.0.0/36.1.0/37.0.0 ไม่มี JDK 17 ในตอนนั้น จึงใช้ local JDK 21 โดยไม่เปลี่ยนค่าระบบ
- เขียนแผนก่อนเริ่ม เพิ่ม AGP 9.2/Gradle 9.4.1, Compose resources และ manifest
- สร้าง Room v1 สำหรับ PC, allowlist, immutable revisions, assets และ event/asset delivery แยก PC
- เพิ่ม listener ที่ตรวจ allowlist, JSON-safe raw, parsed fallback/minimal event, ongoing/group tracking และ asset extraction/hash/25 MiB limit
- เพิ่ม compression/chunks, QR validation, Keystore, pinned WSS/HMAC, ACK/retry, WorkManager, NSD และ Realtime service
- เพิ่ม Compose onboarding/status/allowlist/pairing/backfill/PC pause-revoke/modes/settings ตอนบันทึกนี้กล้องยังเป็น adapter seam พร้อม manual JSON
- เพิ่ม tests สำหรับ hash, compression, chunking, HMAC, expiry, instance revisions และ Room ACK
- สร้าง wrapper; compile แรกพบ imports สองจุด แล้วพบ protocol mismatch สำคัญในการ audit
- ปรับ QR/envelope/UUID/pair secret ownership/auth/session/JCS/HMAC/notification/ACK และ transfer framing ให้ตรง schema
- ใช้ routes /easynoty/pair และ /easynoty/sync ตรงกับ Windows
- เพิ่ม normative HMAC vector เป็น test resource ตรวจ canonical bytes/hash/signing input/signature แบบ byte-for-byte
