# สถานะการตรวจรับ EasyNoty

[English](ACCEPTANCE_STATUS.en.md) | [สารบัญ](../README.md)

ตรวจล่าสุด: 3 ตุลาคม 2026

## สรุป

มีโค้ด Windows/Android/Protocol และ build ทดลองแล้ว แต่ **MVP ยังไม่ครบตามแผนและยังไม่ผ่าน end-to-end acceptance** บันทึกเก่าที่ระบุว่าเหลือเพียงมือถือจริงสรุปเกินกว่าหลักฐาน ต้องใช้หน้านี้เป็นสถานะล่าสุด

## สิ่งที่มีแล้ว

- Monorepo, สเปก, แผนหลัก/แผนย่อย, root verification และคู่มือสองภาษา
- Protocol v1: 9 schemas, valid/invalid fixtures, canonical HMAC vector และ tests ทั้ง C#/Kotlin
- Windows: WSS receiver, SQLite/WAL, raw revisions, assets, parsers, pairing/auth services, Inbox พื้นฐาน, read/archive/delete, toast/tray/startup, DNS-SD/mDNS และ storage services
- Windows UI ล่าสุด: เว้นปุ่ม caption, จำขนาด/ตำแหน่ง, แถบสถานะปิดไม่ได้, icon สำหรับ EXE/window/taskbar/tray
- Android: allowlist capture, Room/per-PC queues, assets, Keystore, pinned WSS/HMAC, compression/chunking, WorkManager/Realtime, NSD และ CameraX/ML Kit

## หลักฐาน automated

| การตรวจ | ผล |
|---|---|
| Protocol verifier รอบ 3 ต.ค. | ผ่าน: 9 schemas, 9 valid, 9 invalid, 1 HMAC vector |
| Windows Release รอบ 3 ต.ค. | build ผ่าน ไม่มี warning/error; tests 11/11 |
| Android รอบ 3 ต.ค. | testDebugUnitTest และ assembleDebug สำเร็จโดยใช้ Gradle up-to-date results; รายงานเดิม 14/14 ไม่มี failure |
| การติดตั้งจริง/ภาพหน้าจอรอบนี้ | ไม่ได้ทดสอบ |
| ADB | ไม่พบอุปกรณ์ในการตรวจสถานะล่าสุด |

Build/tests ไม่ได้ครอบคลุมทุก acceptance ในแผน ไม่ควรนับ service ที่ยังไม่มี UI เป็นฟีเจอร์สมบูรณ์

## งาน implement ที่ยังค้าง

- เชื่อม Windows UI สำหรับ device rename/pause/revoke, sensitive settings, raw viewer/re-parse/export และ storage migration
- ทำ flow เทียบรหัส pairing ก่อน Accept ให้ครบทั้งสองหน้าจอ
- คลิก toast ต้องเปิด focused detail โดยไม่ล้าง filters; ปัจจุบันยังล้าง
- ตรวจ/เติม settings, unread controls/count refresh, retention scheduling, diagnostics และ native integrations เทียบข้อกำหนดทั้งหมด
- Setup ปัจจุบันเพิ่ม firewall rule และบอกให้ติดตั้ง Runtime เอง ยังไม่มี runtime installation/health flow ครบตามแผน

## งานตรวจรับที่ค้าง

1. Mock → Windows: pairing/auth, durable ACK, duplicates/replay, malformed/chunk corruption, missing assets, disconnect/retry, pause/revoke/backlog และ migration rollback
2. Windows manual: tray/startup, cold toast activation, focus/filter retention, lock redaction, multi-select และ storage fallback
3. Android จริงบน LAN: permissions/QR/NSD, allowlisted/blocked apps, grouped/ongoing, assets, offline/Wi-Fi/IP recovery, Doze, Realtime, 25 MiB rejection และ backfill
4. Hardening: disk-full/DB failure, expiry/clock skew, IPv4/IPv6, public-network handling และตรวจ diagnostic ว่าไม่มีข้อมูลส่วนตัว

## ลำดับถัดไป

เก็บ Windows UI/flow ให้ตรงแผน → ทดสอบ Mock ให้ครบ → ทดสอบมือถือจริง → บันทึกหลักฐานและจัดแพ็กเกจทดลอง ไม่เปลี่ยนสถานะเป็น complete เพียงเพราะ build ผ่าน
