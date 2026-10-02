# แผน Windows MVP

[English](02_WINDOWS_MVP_PLAN.en.md) | [สารบัญ](../README.md)

## เป้าหมาย

แอป x64 unpackaged สำหรับ Windows 10/11 ใช้ .NET 10/WinUI 3 พร้อม Mock Android และเส้นทางที่ทดสอบแล้วตั้งแต่รับข้อมูล LAN ที่ยืนยันตัวตนจนเก็บ SQLite และแสดง Inbox

## ชุดงาน

1. โครง solution, Core/Infrastructure/Desktop/Setup/Mock/Tests, scripts และ English resources
2. Domain/protocol models, SQLite migration/WAL, storage fallback, immutable revisions, parsed history และ asset store
3. Receiver transaction/idempotency, parser, Inbox/counts/read/archive/delete, sensitive, retention, pairing และ DPAPI
4. Kestrel WSS pair/sync, ACK/error, missing assets, chunk/hash และ mock scenarios
5. WinUI Inbox/detail/settings/pairing พร้อม toast/tray/startup/lock/mDNS และ setup helper
6. Tests สำหรับ parser/revisions/filters/sensitive/retention/HMAC/assets/storage/WSS พร้อมบันทึก manual gates

## เกณฑ์ผ่าน

Restore/build x64 และ tests ผ่าน; duplicate ACK โดยไม่สร้างซ้ำ; ongoing append revision; ACK หลัง persist; signature/replay/hash mismatch ถูกปฏิเสธ; filters เป็น intersection และ sensitive reveal เฉพาะ item; retention เริ่ม archive 30 วัน/ไม่ hard delete; desktop และ mock เปิดได้และ native integrations แสดงสถานะตามจริง

รายการนี้เป็นเป้าหมาย ดูหลักฐานที่ทำแล้วและงานค้างใน [สถานะ](../logs/ACCEPTANCE_STATUS.md)
