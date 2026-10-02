# สเปกภาพรวมระบบ Android → Windows Notification Bridge

[English](00_SYSTEM_OVERVIEW.en.md) | [สารบัญ](README.md)

รุ่นร่าง 0.1 MVP · วันที่ 3 กรกฎาคม 2026 · เริ่มจากใช้งานส่วนตัวและออกแบบให้พัฒนาต่อได้

เอกสารนี้เรียบเรียงข้อกำหนดของร่างเดิม ไม่ใช่รายงานว่าทำเสร็จแล้ว หากมีตัวเลือกเก่าหรือข้อขัดแย้งให้ยึด [แผนหลักที่ตกลงแล้ว](03_EASYNOTY_IMPLEMENTATION_PLAN.md) ส่วนรูปแบบข้อมูลจริงให้ยึด [Protocol v1](protocol/README.md) และ schema ดู [สถานะปัจจุบัน](logs/ACCEPTANCE_STATUS.md) แยกต่างหาก

## 1. ภาพรวมผลิตภัณฑ์

ระบบประกอบด้วยสองแอป Android อ่านแจ้งเตือนผ่าน `NotificationListenerService` เฉพาะแอปที่ผู้ใช้เลือก เก็บ raw snapshot และ assets ต้นฉบับ แล้วส่งผ่าน LAN WebSocket โดยส่งรูปเฉพาะ hash ที่ Windows ยังไม่มี

Windows รับจาก Android หลายเครื่อง แสดง native toast เก็บประวัติในเครื่อง พร้อมสถานะอ่าน/ยังไม่อ่าน/เก็บถาวร/ลบ ตัวกรองอุปกรณ์และแอป การซ่อนข้อมูล sensitive และระบบ parser ที่เก็บเวอร์ชันเพื่อ re-parse ย้อนหลัง Windows เป็น archive หลัก แนวทาง MVP คือใช้ LAN และเก็บข้อมูลในเครื่อง

## 2. แพลตฟอร์มเป้าหมาย

Android ขั้นต่ำ Android 11/API 30 ใช้ Kotlin และแนะนำ Jetpack Compose เพื่อรองรับ WebP lossless ได้ตามค่าเริ่มต้น ใช้ emulator ตรวจ UI ได้ แต่การแจ้งเตือนและ background ต้องตรวจด้วยมือถือจริง

Windows เป้าหมาย Windows 10/11 ใช้ C#, WinUI 3 และ Windows App SDK เป็น tray app ด้วยสิทธิ์ผู้ใช้ปกติ รองรับ native toast ไม่ใช้ Electron/React/Flutter/Tauri ใน MVP

## 3. สถาปัตยกรรม

```text
Android: NotificationListenerService → Raw snapshot → Asset/hash store
         → Local queue / retry / ACK → WebSocket client
                                      ↓ LAN WSS + pairing
Windows: WebSocket server → Validate → SQLite / raw / parsed / assets
                                      → Native toast + Inbox + filters
```

## 4. หลักการ

- **Raw-first:** ส่ง raw ควบคู่ข้อมูลที่ parse แล้วเพื่อรองรับรูปแบบแจ้งเตือนที่เปลี่ยนและ re-parse ประวัติ
- **Original-asset-first:** เก็บ encoded bytes เดิมหากมี หากได้เพียง Bitmap/Icon/Drawable ให้สร้าง lossless snapshot ค่าเริ่มต้น WebP lossless และเลือก PNG/WebP ได้ ไม่ resize/crop หรือแปลงสูญเสียคุณภาพใน MVP; thumbnail เป็นงานอนาคต
- **Hash-first:** จับ asset → hash canonical bytes → เก็บใน Android → ส่ง manifest → Windows ขอ hash ที่ขาด → อัปโหลดเฉพาะที่ขาด
- **ความเป็นส่วนตัวในเครื่อง:** Windows เก็บประวัติหลัก ซ่อนข้อมูล sensitive จนเปิดรายการ ปิดเนื้อหาบน lock screen เป็นค่าเริ่มต้น ไม่มี cloud relay
- **ความทนทาน:** ทุก event เข้าคิวก่อน ส่งด้วย ACK/retry ไม่สมมติว่า socket อยู่ตลอด และยอมรับว่า Doze อาจทำให้ล่าช้า

## 5. ขอบเขต MVP

รวม Notification Access, allowlist, raw/assets, local queues, LAN WebSocket, QR pairing, identity/revoke แยกอุปกรณ์, native toast, SQLite history, read/unread, archive/soft delete, parser version/re-parse, หลายมือถือ, ตัวกรองหลายอุปกรณ์/หลายแอป, sensitive settings และค้นหาพื้นฐาน

ไม่รวม iOS, cloud, reply, sync สถานะอ่าน/ลบของมือถือ, สั่งลบแจ้งเตือนบนมือถือ, custom toast, AI/OCR, ล้าง asset ที่ไม่ใช้จริง, full-text index ขั้นสูง, Windows Hello/PIN และ sync archive ระหว่าง Windows

## 6. ข้อสรุปหลัก

| เรื่อง | ค่าเป้าหมาย |
|---|---|
| การส่ง | LAN WebSocket; แผนภายหลังล็อกเป็น WSS + pinning/HMAC |
| Pairing | QR และ secret แยกคู่ |
| Windows | แอปผู้ใช้ปกติ มี tray และ native toast |
| คลิก toast | เปิด Inbox ไปยังรายการนั้น; แผนภายหลังให้คงตัวกรอง |
| Raw | immutable พร้อมเวอร์ชัน parser |
| รูปภาพ | original-first, WebP lossless เป็น snapshot เริ่มต้น |
| Compression | Zstd ก่อน, zlib fallback; level 6/9/12 เริ่มที่ 9 |
| Threshold | 5/10/15/20% เริ่มที่ 10% |
| Archive | 30/60/90 วัน, never, custom; เริ่ม 30 วัน |
| Delete | ตัวเลือกเวลาเช่นเดียวกัน เริ่ม never และเป็น soft delete เท่านั้น |

## 7. โหมด sync

Balanced เป็นค่าเริ่มต้น เข้าคิวก่อน ส่งเมื่อเชื่อมต่อและ retry เมื่อหลุด รูปอาจมาทีหลังข้อความ ไม่บังคับ foreground service แบบเข้มงวด

Realtime ผู้ใช้เปิดเอง พยายามรักษาการเชื่อมต่อ อาจใช้ foreground notification และแนะนำ unrestricted battery พร้อมอธิบายการใช้แบตเพิ่ม

Battery Saver หลีกเลี่ยง socket ตลอดเวลา ส่งเป็นชุดและอาจชะลอรูปใหญ่ เป็น best effort

## 8. Pairing และ identity

Windows เปิด Pair Device สร้าง session ชั่วคราวและ QR; Android สแกน เชื่อม pairing endpoint แลก identity ตรวจ token สร้าง secret ของคู่ แล้วทั้งสองฝั่งเก็บอุปกรณ์

QR ในร่างเดิมกล่าวถึง protocol version, PC ID/name, IP, port, token, public key และ expiry ภายหลังเปลี่ยนเป็น endpoint objects และ certificate SHA-256 fingerprint ตาม schema ไม่ควรนำตัวอย่าง JSON ในร่างเดิมไปส่งจริง

Windows ต้องแสดงรายชื่อมือถือ เปลี่ยนชื่อ/revoke/pause ดู connection และ unread count ส่วน Android ต้องแสดงรายชื่อ PC และ pause/revoke แยกคู่

## 9. Protocol

ทุกข้อความมี envelope กลาง ร่างเดิมใช้ `sent_at`; contract ที่ใช้งานจริงใช้ `sent_at_utc`, `session_id` และ `authentication` เพิ่มเติม

| กลุ่มข้อความ | หน้าที่ |
|---|---|
| hello / pairing request / accept | handshake และจับคู่ |
| notification_event / event_ack | ส่งแจ้งเตือนและยืนยันบันทึก |
| asset_missing_request | ขอรูปที่ยังไม่มี |
| asset_transfer_start/chunk/complete / asset_ack | ส่งและตรวจรับ asset |
| ping / pong / error | keepalive และข้อผิดพลาด |
| device_settings_update | แนวคิดอนาคต ไม่ใช่ message ของ v1 ปัจจุบัน |

## 10. Notification event

ข้อมูลแนวคิดประกอบด้วย event ID, Android notification key, device ID, package/app, เวลาที่โพสต์/จับ, raw schema version, raw snapshot, parsed result และ asset manifests

Parsed result ระบุ parser name/version, title/body, sender, conversation และ confidence; asset ระบุ role/hash/MIME/ขนาด/มิติ ฟิลด์จริงและ metadata instance/revision ต้องตาม schema v1

## 11. Assets

Role ได้แก่ app_icon, avatar, preview_image, big_picture, attachment_preview และ unknown คำนวณ SHA-256 จาก canonical bytes ต้นฉบับ ไม่ใช่ compressed transport

บีบอัดด้วย Zstd → zlib แล้วเลือกใช้เฉพาะเมื่อประหยัดถึง threshold หากไม่ถึงให้ส่งเดิม Windows ตรวจ hash หลัง decompress เสมอ ขอบเขตแบ่งชิ้น 64 KiB; contract ภายหลังล็อก start/chunk/complete แบบ JSON base64 และจำกัด asset 25 MiB

## 12. Storage Windows

เลือกโฟลเดอร์ข้างแอปหรือ LocalAppData เริ่มจาก `<AppFolder>/data` หากเขียนไม่ได้ใช้ `%LocalAppData%/EasyNoty` และแจ้งเหตุผลใน Settings โครงสร้างมีฐานข้อมูล, assets, logs และอาจมี thumbs ในอนาคต

Delete คือ soft delete ซ่อนจาก UI แต่คงแถวและ assets ไว้ การสแกนไฟล์ที่ไม่ใช้ แสดงพื้นที่คืนได้ และลบจริงเป็นงานอนาคต

## 13. Inbox และตัวกรอง

เป้าหมายมีแถบอุปกรณ์ด้านซ้าย แถบแอปและรายการแจ้งเตือน แสดง icon/name/unread count เลือกหลายรายการได้ All Devices/All Apps หมายถึงไม่กรองมิตินั้น

ผลลัพธ์ใช้ `(อุปกรณ์ที่เลือก) AND (แอปที่เลือก) AND ไม่ถูกลบ` ภายในแต่ละกลุ่มเป็น OR

## 14. แอป sensitive

ตั้งค่าเป็น normal/sensitive ตาม package บน Windows ซ่อนข้อความใน toast และรายการ ต้องเปิด/reveal รายการนั้นโดยชัดแจ้ง การเปิดหนึ่งรายการไม่เปิดทั้งหมด และต้องป้องกัน preview บน lock screen

## 15. Retention

Auto archive เริ่ม 30 วัน เลือก 30/60/90/never/custom ได้ ซ่อนจาก Inbox แต่ยังอยู่ Archived/All Auto delete เริ่ม never เลือกเวลาเช่นเดียวกันและทำ soft delete ไม่ลบไฟล์จริง

## 16. Re-parse

Raw ห้ามแก้ไข ผล parse เป็นข้อมูลที่สร้างใหม่ได้ เป้าหมายรองรับ re-parse รายการเดียว/ทั้งแอป/ทั้งประวัติ, raw viewer และ export ตัวอย่าง อาจวางใน Advanced/Developer

## 17. Security/privacy

จับคู่โดย explicit action มี identity ที่ revoke ได้ต่อคู่ ตรวจความถูกต้องข้อความและเข้ารหัส transport ไม่มี cloud แสดงตำแหน่งเก็บข้อมูลให้ผู้ใช้ทราบ เนื้อหาแจ้งเตือนเป็นข้อมูลอ่อนไหว

## 18. เกณฑ์ตรวจรับ

- แจ้งเตือนจากแอปที่อนุญาตต้องถึง Windows เก็บ raw/parsed แสดง toast และคลิกเปิดรายการได้
- Asset ที่ขาดถูกขอ ส่ง ตรวจ SHA-256 และเก็บต้นฉบับ
- แอป sensitive ซ่อน toast/list จนเปิดรายการ
- เลือกสองมือถือและสองแอปแล้วได้เฉพาะผลที่ตรง intersection
- Archive ตามอายุ 30 วัน และไม่ auto delete เมื่อเลือก never

## 19. ชื่อและ repository

ร่างเดิมเสนอชื่อ NotificationBridge/PhoneNotifyBridge/WinNotifyRelay/NotifyBridge และแยก repo ตามแพลตฟอร์ม ข้อสรุปปัจจุบันใช้ **EasyNoty ใน monorepo เดียว**

## 20. ลำดับพัฒนาเดิม

สร้างโครง Windows/Android และ contract → mock event → SQLite/Inbox → toast → Android listener → QR → queue/ACK → manifest/missing asset → compression → sensitive → retention ใช้ milestone ในแผนหลักแทนลำดับร่างเมื่อมีข้อขัดแย้ง
