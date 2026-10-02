# สเปกแอป Android

[English](01_ANDROID_APP_SPEC.en.md) | [สารบัญ](README.md)

รุ่นร่าง 0.1 MVP · 3 กรกฎาคม 2026 · Android 11+ / Kotlin

นี่คือข้อกำหนดเป้าหมายเดิม ไม่ใช่สถานะความครบถ้วน [แผนหลัก](03_EASYNOTY_IMPLEMENTATION_PLAN.md) และ [Protocol v1](protocol/README.md) แทนที่ตัวเลือกเดิม เช่น asset จำกัด 25 MiB, QR ใช้ endpoint/fingerprint และ transport v1 ใช้ JSON base64 ดู [งานค้าง](logs/ACCEPTANCE_STATUS.md)

## 1. จุดประสงค์

จับแจ้งเตือนจากแอปที่เลือกและส่งไป Windows ที่จับคู่ ขอ Notification Access ให้เลือก allowlist จับ raw/assets ทันที เก็บคิว/cache ส่ง event พร้อม hash manifest อัปโหลดเฉพาะ asset ที่ขาด และรองรับ Balanced/Realtime/Battery Saver Android ไม่ใช่ archive หลัก

## 2. เทคโนโลยี

Kotlin, Android 11/API 30+, Jetpack Compose, NotificationListenerService, Room/SQLite, app-specific storage, WebSocket client, CameraX/ML Kit สำหรับ QR, Zstd JNI และ zlib/Deflater fallback, SHA-256, WebP lossless เริ่มต้น โดยเลือก PNG/WebP ได้

## 3. สิทธิ์และตั้งค่า Android

อธิบายเหตุผลการอ่านแจ้งเตือน มีปุ่มเปิด Notification Access settings ตรวจสถานะ listener และแสดงหน้าแรก แนะนำ unrestricted battery/background, background data และ OEM auto-start เมื่อต้องการ realtime แต่ไม่บังคับ ผู้ใช้เลือก foreground service เอง

## 4. หน้าจอ

| หน้า | ข้อมูลและการควบคุมที่ต้องมี |
|---|---|
| Home/Status | Notification Access, PC ที่จับคู่, sync mode, connection, pending events/assets, last sync |
| Allowlist | icon, app name, package และ toggle ไม่มีแอปได้รับอนุญาตโดยไม่เลือก |
| Pair Windows | สแกน QR → ตรวจ payload → เชื่อมต่อ → handshake → เพิ่ม PC |
| Paired PCs | ชื่อ, connection, last seen, pause และ revoke |
| Sync mode | Balanced เริ่มต้น, Realtime, Battery Saver |
| Asset/compression | PNG/WebP/WebP lossless, Zstd→zlib, level 6/9/12 เริ่ม 9, threshold 5/10/15/20% เริ่ม 10%, Sync images, Wi-Fi only |

ร่างเดิมเปิดทางให้ asset limit ไม่จำกัด/ปรับได้ แต่แผนที่ตกลงล็อกไว้ 25 MiB ต่อไฟล์

## 5. ส่วนประกอบหลัก

Listener รับ `onNotificationPosted` ตรวจ allowlist จับ raw/assets บันทึก DB/cache เข้าคิวแล้วกระตุ้น connection manager ห้ามรอให้แจ้งเตือนถูกลบหรือสมมติว่าจะอ่านคืนได้ภายหลัง

Connection manager ดูแล socket/reconnect, sync mode, event/assets, ACK, missing-asset requests และ backpressure Queue manager แยก event/asset/retry ติดตาม status, retry count, last attempt/error และ PC ปลายทาง

Asset store เก็บ canonical bytes/metadata/hash อ่านตาม hash ให้ Windows ได้ และล้าง Android cache หลังพ้น retention อย่างปลอดภัย

## 6. การจับแจ้งเตือน

```text
onNotificationPosted → allowlist → raw → parsed v1 → assets
                     → local persistence → queue → send if connected
```

Raw ต้องเป็น JSON-friendly ห้าม serialize Notification/StatusBarNotification โดยตรง ฟิลด์ที่ต้องพิจารณา: capture schema/Android version, device/package/app, notification key/id/tag, group, post time/when, category/channel/importance/flags, ongoing/clearable, extras, people/actions/style summary และ asset refs

จับ extras ทั่วไป เช่น `android.title`, `android.text`, `android.subText`, `android.summaryText`, `android.bigText`, `android.textLines`, `android.conversationTitle`, `android.messages`, historic messages และ people/channel data ค่าไม่รู้จักให้เก็บ primitive/string ที่ปลอดภัย หรือ type name/debug summary หาก serialize ไม่ได้

Parsed v1 ประกอบด้วย app/title/body, sender/conversation เมื่อทราบ, preview, asset refs และ confidence 0–1 Windows ยัง re-parse ได้

## 7. การจับ assets

Source kind ครอบคลุม encoded_bytes, content_uri, bitmap_object, icon_bitmap/data/uri/resource, drawable_bitmap/rendered และ unknown_rendered_snapshot

Originality level ได้แก่ original_encoded_bytes, lossless_snapshot_from_bitmap, rendered_snapshot_from_drawable และ unknown_snapshot

ถ้าได้ encoded bytes ต้นฉบับให้เก็บเดิม หากได้เพียง Bitmap/Icon/Drawable ให้ extract/render และ encode ตาม snapshot format เริ่ม WebP lossless คำนวณ hash จาก bytes ที่เก็บ ห้าม resize/crop/lossy-convert ข้อห้าม base64 ในร่างเดิมหมายถึงไม่ฝังรูปใน notification manifest; v1 transfer ใช้ base64 ตาม contract

Manifest มี role, hash, MIME, byte size, width/height, source kind และ originality level โดยใช้ชื่อ field จริงจาก schema

## 8. Compression

คำนวณ hash ต้นฉบับก่อน ลอง Zstd (level 6/9/12 เริ่ม 9) หากล้มเหลวใช้ zlib ใช้ผลบีบอัดเมื่อ `compressed_size <= original_size * (1 - threshold)` เท่านั้น threshold 5/10/15/20% เริ่ม 10%

ทำงานใน background queue ไม่ block capture จัดลำดับข้อความ/event → app icon/avatar → preview → big image → อื่น ๆ

## 9. WebSocket sync

Android เป็น client, Windows เป็น server Balanced เชื่อมเมื่อทำได้/reconnect เมื่อ network กลับมา Realtime พยายามคง socket และอาจใช้ foreground service Battery Saver ส่งเป็นชุดห่างขึ้น

Android ส่ง event/raw/parsed/manifest Windows ตอบ event_ack และขอ asset ที่ขาด Android อัปโหลดและรอ asset_ack แยกต่อ PC ทำเครื่องหมายสำเร็จเมื่อ ACK ยืนยัน persist/verify เท่านั้น

## 10. Pairing

สแกน QR ที่ระบุ protocol, PC ID/name, endpoints/port, one-time token, fingerprint และ expiry ตาม contract หลังจับคู่เก็บ PC identity/name, endpoint, secret reference, paired time, last seen และ pause/enabled state

Windows เป็นผู้สร้าง secret 32 bytes Android รับผ่าน pinned WSS และเก็บโดย Keystore ไม่ใช้ตัวอย่าง QR ร่างเก่าที่มี public_key เป็น wire จริง

## 11. ฐานข้อมูลแนวคิด

| ตาราง | ข้อมูล |
|---|---|
| paired_windows_devices | ID, PC ID/name, endpoint, key/secret ref, paired/last-seen, paused/revoked |
| app_allowlist | package/name, allowed, last seen, icon hash |
| notification_events | event/instance/revision, notification key, package/app, posted/captured, raw/parsed, created |
| notification_event_delivery | event + PC, status, retry, last attempt/error, ACK time |
| assets | hash, MIME, size, dimensions, source/originality, path, created/last seen |
| asset_delivery | asset + PC, status, retry, last attempt/error, ACK time |

นี่คือ model แนวคิด ดู Room entities/schema สำหรับชื่อและโครงสร้างที่ใช้อยู่จริง

## 12. ข้อผิดพลาด

Raw capture ล้มเหลวให้เก็บ minimal event พร้อม error และไม่ทำ listener ล้ม Asset extraction ล้มเหลวยังเก็บ event ได้พร้อม error metadata Network ล้มเหลวคง pending และ retry/backoff แสดง connection state Asset retry แยกได้ ไม่จำเป็นต้องส่ง event ที่ ACK แล้วซ้ำ Windows ขอ asset ภายหลังได้

## 13. Privacy

แจ้งว่าข้อความอาจเป็นส่วนตัว ส่งเฉพาะ allowlist ไป PC ที่จับคู่ ผู้ใช้ pause/revoke ได้ ไม่มี cloud ใน MVP

## 14. เกณฑ์ตรวจรับ

- เปิด/ปิด Notification Access แล้วตรวจสถานะได้
- allowlist ทำงานและไม่จับแอปที่ไม่อนุญาต
- สแกน QR จับคู่และแสดง PC ได้
- event/raw/parsed ถึง Windows และรับ ACK
- missing assets ถูกขอ ส่ง และ ACK
- Windows offline แล้วยังคงคิว เมื่อกลับมาต้องส่งสำเร็จ

## 15. งานอนาคต

Cloud relay, reply/dismiss จาก Windows, allowlist แยก PC, rich remote actions, raw viewer บน Android, battery diagnostics ขั้นสูง และ diagnostic bundle export
