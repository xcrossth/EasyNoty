# แผน Android MVP

[English](03_ANDROID_MVP_PLAN.en.md) | [สารบัญ](../README.md)

## เป้าหมาย

แอป Kotlin/Compose สำหรับ Android 11+ `app.easynoty.android` จับเฉพาะ allowlist เก็บ immutable snapshot/assets และสถานะส่งแยก PC แล้ว sync ผ่าน WSS ภายใน LAN

## ชุดงาน

1. Gradle wrapper, Compose, Room, WorkManager, OkHttp และ unit/instrumentation dependencies; minSdk 30/target 36 และ English resources
2. Room สำหรับ PC/allowlist/events/revisions/assets/per-PC queues; raw JSON-safe capture, fallback parser/minimal event, group/ongoing tracking และ close instance เมื่อ Android ลบ
3. Asset hash store แบบ original/lossless พร้อม limit 25 MiB
4. QR validation, Keystore, TLS pinning, HMAC/session, ACK/missing assets/retry/pause/revoke, Zstd→zlib และ 64 KiB chunks
5. Balanced/Realtime/Battery Saver, Compose onboarding/status/allowlist/pairing/backfill/PC/settings, NSD และ CameraX/ML Kit
6. Tests สำหรับ hashing/HMAC/QR/compression/instance/queue พร้อม build APK และบันทึกข้อจำกัด device-only

## ขอบเขตการตรวจรับ

Wrapper build APK และ local tests ได้; capture/storage/transport/service declarations มีจริง; กล้อง, NotificationListenerService, Doze และ end-to-end ต้องมีหลักฐานจากมือถือ ไม่เพิ่ม cloud, reply, phone dismissal หรือ Android archive หลัก

บันทึกเดิมกล่าวถึง scanner adapter ที่ยังรอ implementation ต่อมามี CameraX/ML Kit แล้ว แต่ยังต้องตรวจบน hardware ดู [สถานะ](../logs/ACCEPTANCE_STATUS.md)
