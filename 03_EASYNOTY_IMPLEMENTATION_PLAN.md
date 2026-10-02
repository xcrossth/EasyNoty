# แผน Implement EasyNoty MVP

[English](03_EASYNOTY_IMPLEMENTATION_PLAN.en.md) | [สารบัญ](README.md)

นี่คือแผนที่ตกลงแล้ว ไม่ใช่รายการงานที่เสร็จทั้งหมด ดู [สถานะการตรวจรับ](logs/ACCEPTANCE_STATUS.md) สำหรับหลักฐานและงานค้าง คู่มือ build อธิบาย toolchain ที่ใช้ปัจจุบันแยกจาก baseline ในแผน

## 1. โครงสร้างและ Toolchain

- สร้าง monorepo เดียว แบ่งเป็น `windows/`, `android/`, `protocol/` และคงสเปกเดิมไว้ที่ root
- ใช้ identifier:
  - Android: `app.easynoty.android`
  - Windows AUMID: `app.easynoty.desktop`
  - C# namespace: `EasyNoty`
- Windows: .NET 10 LTS, WinUI 3, Windows App SDK 2.2 stable, x64, Windows 10 build 19041+
- Windows เป็น unpackaged/framework-dependent; `EasyNoty.Setup.exe` แบบ self-contained ขอ UAC เฉพาะติดตั้ง Windows App Runtime และ firewall rule บน Private networks
- Android: JDK 17, AGP 9.2/Gradle 9.4.1, minSdk 30, target stable API 36, Compose BOM 2026.05, Room 2.8.4 และ WorkManager 2.11.2
- UI เป็น English เท่านั้น แต่เก็บข้อความใน resource files เพื่อเพิ่มภาษาในอนาคต

## 2. Protocol, Security และ Storage Contract

- กำหนด Protocol v1 เป็น JSON Schema + golden fixtures; C# และ Kotlin มี models แยกกันแต่ผ่าน contract tests ชุดเดียวกัน
- Envelope ใช้ `protocol_version`, `message_id`, `message_type`, `device_id`, `sent_at_utc`, `session_id`, `payload`
- QR pairing เพิ่ม endpoint IPv4/IPv6, port, PC identity, certificate SHA-256 fingerprint, one-time token และเวลาหมดอายุ 2 นาที
- Windows สร้าง certificate เอง; Android เชื่อม WSS ด้วย certificate pinningจาก QR/mDNS จากนั้นแสดง short verification code และรอผู้ใช้กด Accept บน Windows
- หลัง pairing ใช้ secret 256-bit, HMAC challenge/session authentication และ HMAC ผูก message metadata กับ payload hash; ป้องกัน replay ด้วย `message_id` และตอบ ACK เดิมได้เมื่อ retry
- เก็บ private key/secret ด้วย Windows DPAPI และ Android Keystore; log ต้อง redact content, token และ secret
- Delivery เป็น at-least-once:
  - `event_ack` หลัง transaction บันทึก raw/revision สำเร็จ
  - `asset_ack` หลัง decompress และตรวจ SHA-256 สำเร็จ
  - retry ต้องใช้ event ID เดิมและไม่สร้างข้อมูลซ้ำ
- Notification event เพิ่ม `notification_instance_id`, `revision_index`, group/style/ongoing metadata และ raw byte size/hash:
  - notification ปกติสร้าง logical item ใหม่ทุก post
  - ongoing/progress/media/service key เดิมอัปเดต logical item แต่ทุก raw revision immutable
  - `onNotificationRemoved` ใช้ปิด instance ฝั่ง Android เท่านั้น ไม่ sync การลบไป Windows
  - group summaries ถูกเก็บเพื่อ re-parse/debug แต่ไม่แสดง toast, Inbox หรือ unread count
- Event/raw ใช้ Zstd → zlib fallback และ threshold เดียวกับ asset; payload ที่บีบอัดหรือยังเกิน 2 MB ใช้ start/chunk/complete framing ขนาด chunk 64 KiB พร้อม canonical SHA-256
- Raw ไม่มี application hard limit; Windows ติด oversized badge เมื่อเกิน 2 MB ส่วน serialization/disk failure ให้บันทึก minimal event พร้อม error
- Asset จำกัด 25 MB ต่อไฟล์, original-first, hash canonical bytes, `<64 KB` ส่งก้อนเดียวและไฟล์ใหญ่แบ่ง chunk; ปิด Sync images แล้วยังคงส่ง app icon
- Windows ใช้ EF Core 10 + SQLite WAL และ migrations โดยมีตาราง logical notifications, immutable revisions, parsed results, devices/apps, assets/mappings, pairing sessions และ settings
- Android ใช้ Room พร้อม delivery state แยกต่อ PC; PC ที่ pause ยังมี pending backlog และ Android GC หลังทุก PC ที่ไม่ revoke ACK ครบแล้ว 7 วัน

## 3. Implementation Milestones

1. Foundation และ contract
   - เริ่ม Git repo, central dependency versions, build/test scripts และ protocol fixtures
   - สร้าง Windows solution, Core/Infrastructure/App/Setup และ Mock Android CLI
   - Mock รองรับ pairing, HMAC, retry, malformed messages, assets, large raw และ offline simulation

2. Windows foundation
   - Generic Host + Kestrel dual-stack WSS, Win32 DNS-SD/mDNS advertisement และ single-instance lifecycle
   - EF migrations, storage resolver, content-addressed asset store, redacted rolling logs
   - Win32 tray icon/menu, startup registration, setup/runtime/firewall health checks
   - Default storage เป็น `<AppFolder>/data` เมื่อเขียนได้ มิฉะนั้น `%LocalAppData%\EasyNoty`

3. Windows feature-complete ด้วย Mock Android
   - Pairing QR/Accept, device pause/rename/revoke, certificate/HMAC authentication
   - Receiver pipeline, idempotency, raw revisions, Windows-first parser และ Android parsed-result fallback
   - Generic, MessagingStyle และ Gmail fallback parsers พร้อม re-parse/raw viewer/export
   - Native app notifications, click-to-open focused detail, ongoing toast updateด้วย tag/group
   - Inbox/Archived/All, virtualized list, multi-device/app filters, counts, basic search, read/unread, archive และ soft delete
   - Sensitive app เป็นค่าระดับ package: hide toast/list และ reveal เฉพาะ item จนเสีย selection, minimize, close หรือ Windows lock
   - ขณะ Windows lock ให้ redact toast ทุกแอป และแสดงคำแนะนำให้ปิด lock-screen content ใน Windows settings
   - Retention job นับจาก `posted_at`: archive 30 วัน, delete never เป็น default
   - Storage migration หยุด receiver, backup SQLite, copy/verify DB-assets-keys, restart และเก็บต้นทางเป็นโฟลเดอร์ backup timestamped
   - ถือว่า Windows “ครบ” เมื่อ acceptance ทั้งหมดผ่านด้วย Mock ก่อนเริ่ม Android

4. Android application
   - Compose onboarding: Notification Access → allowlist wizard → pairing; ไม่มีแอปถูกเปิดโดยอัตโนมัตินอกเหนือจากที่เลือกใน wizard
   - NotificationListenerService จับ raw/assets ทันที, active-instance tracking, group metadata และ minimal-event fallback
   - Room queues, per-PC ACK/retry, compression workers และ cache GC
   - OkHttp WSS client, certificate pinning, HMAC session, Android `NsdManager` discovery และ QR scannerด้วย CameraX/ML Kit
   - Balanced default, Realtime foreground service และ Battery Saver ผ่าน WorkManager batching
   - Pair PC ใหม่ให้ถามว่าจะเริ่มจากปัจจุบันหรือ backfill cache 7 วัน โดย default เป็นเริ่มจากปัจจุบัน
   - Revoke ทำลาย secret/queue ของคู่นั้น แต่ Windows archive และ device tombstone ยังคงอยู่

5. Packaging และ hardening
   - Windows ส่งเป็น unsigned local x64 build พร้อม setup helper; อธิบาย Unknown Publisher warning
   - Android ส่ง debug/local release APK สำหรับ ADB/sideload ไม่รวม Play Store/AAB
   - ตรวจ disk-full, DB failure, hash mismatch, expired pairing, clock skew, reconnect, IP change, IPv4/IPv6 และ public-network warnings

## 4. Test และ Acceptance

- Unit tests: parser fixtures, compression threshold/fallback, hashing, HMAC/replay, notification revision rules, filters, sensitive state และ retention
- Integration tests: WSS pairing, duplicate delivery, ACK timing, disconnect/retry, chunk corruption, missing assets, revoke/pause/backlog และ storage migration rollback
- Windows UI/manual gates บน Windows 10 22H2: tray, startup, toast cold activation, focused detail, lock redaction, multi-select filters และ portable fallback path
- Android tests: Room migrations/DAO, queue workers, snapshot conversion, asset extraction และ protocol fixtures
- ตั้งค่า JDK 17, Android Studio/Gradle Wrapper, USB debugging และ ADB driver ก่อน real-device testing
- Real-device gates: allowlisted/non-allowlisted apps, grouped/ongoing notifications, offline recovery, Wi-Fi/IP change, Doze, foreground realtime mode, 25 MB rejection และ backfill pairing
- Final end-to-end acceptance ต้องผ่าน Android จริง → EasyNoty Windows โดยไม่มี cloud, ไม่มี elevated app process และไม่มีข้อมูล sensitive ใน default diagnostic bundle

## 5. Defaults ที่ล็อกแล้ว

- Windows เป็น primary archive และ parser ของ Windows เป็น current view
- PC ทุกเครื่องที่ยังไม่ revoke ได้ delivery; pause หมายถึงเก็บ backlog
- Toast click เปิด focused detail ชั่วคราวโดยไม่เปลี่ยน filters และ mark read เมื่อเปิด detail
- Sensitive state ใช้ร่วมกันทุก device ตาม package name
- Diagnostic export redact เป็น default; raw sample ต้องเลือกเองและยืนยันก่อนสร้าง
- Storage migration เก็บต้นทางเป็น backup
- ไม่ทำ encryption at rest, cloud, reply, phone dismissal, physical asset cleanup หรือ code signing ใน MVP
