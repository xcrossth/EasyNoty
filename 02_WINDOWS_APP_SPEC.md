# สเปกแอป Windows

[English](02_WINDOWS_APP_SPEC.en.md) | [สารบัญ](README.md)

รุ่นร่าง 0.1 MVP · 3 กรกฎาคม 2026 · Windows 10/11 / C# / WinUI 3

เอกสารนี้ระบุเป้าหมาย ไม่ใช่สถานะเสร็จแล้ว [แผนหลัก](03_EASYNOTY_IMPLEMENTATION_PLAN.md) แทนที่ตัวเลือกเดิม เช่น toast ต้องคงตัวกรองและ storage migration ต้องเก็บ backup ใช้ [Protocol v1](protocol/README.md) สำหรับ wire format และดู [สถานะงาน](logs/ACCEPTANCE_STATUS.md)

## 1. จุดประสงค์

รับแจ้งเตือน Android และเป็น Inbox/archive หลัก จับคู่หลายมือถือ รับ LAN WebSocket เก็บ raw/parsed ขอ assets ที่ขาด แสดง native toast มี read/unread, archive, soft delete, sensitive และ filters เลือก storage และ retention ได้

## 2. เทคโนโลยี

C#, WinUI 3, Windows App SDK, Windows 10/11, user-level tray app, native app notifications, SQLite, local asset store, WebSocket server, Zstd→zlib และ SHA-256 ต้องไม่ยกสิทธิ์ desktop app และทดสอบ native toast บน Windows 10

## 3. หน้าที่หลัก

ทำงานเบื้องหลัง/tray, host receiver, QR pairing, จัดการ devices, validate/persist notifications, native toast, ขอและเก็บ assets, Inbox/filters, sensitive policy และ retention jobs

## 4. ส่วนประกอบ

AppHost/Startup, Tray Manager, WebSocket Server, Pairing/Device Manager, Receiver/Validator, SQLite Repository, Asset/Compression Service, Parser Engine, Toast, Inbox, Filter State, Sensitive Manager, Retention และ Settings

## 5. โหมดการทำงาน

แอปสิทธิ์ผู้ใช้ปกติ เริ่มพร้อม Windows ได้ มี tray เปิด Inbox ได้ และรับ WSS/แสดง toast ขณะ user session ทำงาน ไม่ทำ Windows Service ใน MVP

## 6. Storage

เลือกข้างแอปหรือ LocalAppData เริ่ม `<AppFolder>/data` หากเขียนได้ มิฉะนั้น `%LocalAppData%/EasyNoty` พร้อม notice โครงสร้างมี DB/assets/logs และ thumbs ในอนาคต

ร่างเดิมยอมให้เปลี่ยน path แล้ว restart และเลื่อน migration ไปอนาคต แต่แผนหลักกำหนด stop receiver → backup/copy/verify DB/assets/keys → restart → เก็บต้นทางเป็น backup

## 7. Model ฐานข้อมูลแนวคิด

| ตาราง | ข้อมูล |
|---|---|
| devices | identity, display name/type/icon, key/secret ref, paired/last seen, pause/revoke, timestamps |
| apps | package/name/icon, sensitive, first/last seen, timestamps |
| notifications | identity/device/package/key, posted/captured/received, current parser, title/body/preview/sender/conversation/group/confidence, asset refs, read/archive/delete state และเวลา |
| notification_parsed_results | notification ID, parser/version, parsed JSON/fields, confidence, parsed time, current flag |
| assets | hash/MIME/size/dimensions/path/source/originality, first/last seen |
| notification_assets | notification↔asset mapping และ role |
| device_app_stats | unread/total count และ last notification คำนวณสดหรือ cache ได้ |
| settings | key/value JSON และเวลาแก้ไข |

Raw ต้อง immutable แผนภายหลังแยก raw revisions เป็นตารางเฉพาะ Delete เป็น soft delete ซ่อนจาก UI ดู entities และ EF migrations สำหรับ schema จริง

## 8. WebSocket server

เริ่มพร้อมแอป ฟังพอร์ตใน LAN แสดงสถานะและ restart เมื่อเปลี่ยนพอร์ต Pairing endpoint ใช้ระหว่าง session อายุจำกัด ตรวจ token/version ก่อนบันทึกคู่ Device endpoint ตรวจ identity/signature ปฏิเสธ revoked และจัดการ paused ตาม policy

## 9. Pairing UI

เปิด Pair New Device → สร้าง token/QR → Android เชื่อม → แสดง confirmation → ผู้ใช้ยอมรับ → เพิ่มอุปกรณ์ QR ต้องใช้ endpoint/fingerprint ตาม schema แทนตัวอย่าง public-key/IP เดี่ยวในร่างเดิม

เป้าหมาย UI อุปกรณ์แสดง icon/name/last seen/connection/unread และรองรับ pause/resume, rename, revoke

## 10. รับ notification

Validate envelope/device → persist raw/revision และข้อมูล logical/parsed → commit → event ACK → ขอ asset ที่ขาด → toast โดย toast ใช้ placeholder ได้และต้องซ่อนเนื้อหา sensitive ห้าม ACK สำเร็จก่อนบันทึก DB

## 11. รับ asset

ตรวจ manifest/hash ที่มีอยู่ → ขอรายการที่ขาด → รับ transfer → decompress → ตรวจ SHA-256 canonical bytes → เก็บไฟล์เดิม/metadata → asset ACK จัดไฟล์ตาม hash prefix และใช้ MIME เลือกนามสกุลได้ hash เป็นตัวอ้างอิงหลัก

การล้างไฟล์ที่ไม่ถูกอ้างอิงและแสดงพื้นที่คืนได้เป็นงานอนาคต

## 12. Compression

รองรับ none/zstd/zlib อ่าน encoding, decompress, ตรวจ hash แล้วเก็บ canonical bytes ต้นฉบับ หากเลือกเก็บแบบบีบอัดภายหลังต้องมี metadata ให้กู้และตรวจเดิมได้ Level 6/9/12 เริ่ม 9 Android เป็นฝั่งบีบอัดก่อนอัปโหลด

## 13. Native toast

เมื่อ event มาถึงให้แสดง toast คลิกแล้วเปิด Inbox/focus รายการ แอปปกติแสดง app, title/sender, preview, icon/avatar ถ้ามี และชื่ออุปกรณ์เมื่อจำเป็น แอป sensitive แสดงเพียง app กับ Content hidden ห้าม body/sender preview

Asset ยังไม่มาถึงให้ใช้ app/generic icon และอัปเดต Inbox ภายหลัง ไม่จำเป็นต้องอัปเดต toast ตามรูปใน MVP ไม่ทำ custom toast

## 14. Inbox UI

เป้าหมายมีอุปกรณ์ด้านซ้าย แอปด้านบน toolbar/search/view และรายการพร้อม detail ตัวกรองแต่ละรายการมี icon/name/unread รองรับ multi-select และ All หมายถึงไม่กรอง Counts ควรสัมพันธ์กับตัวกรองอุปกรณ์

แถวปกติแสดง app/device/time/title/preview แถว sensitive ซ่อนข้อความ แถว unread ต้องต่างจาก read Archived ไม่อยู่ Inbox แต่แสดง Archived/All ส่วน deleted ไม่อยู่ UI ปกติ

คลิก toast เปิดหน้าต่าง/focus/scroll และ mark read เมื่อเปิด detail แผนหลักกำหนดห้ามเปลี่ยนตัวกรองเดิม Sensitive ต้องมีการเปิด/reveal รายการชัดแจ้ง

## 15. Views

Inbox, Archived และ All ไม่ต้องมี Deleted view ใน MVP อาจเพิ่ม developer view ภายหลัง

## 16. Read/unread

สถานะเฉพาะ Windows รายการใหม่ unread เปิด detail แล้วอ่านได้ และผู้ใช้ mark read/unread เองได้ ไม่ sync กลับมือถือและไม่ใช้สถานะอ่าน/ลบของมือถือ

## 17. Archive/delete

Archive ซ่อนจาก Inbox แต่คงข้อมูลใน Archived/All ทำได้ทั้ง manual และ retention เริ่ม 30 วัน Delete ตั้ง flag/time และซ่อน ไม่ลบแถว/raw/assets จริง Auto delete เริ่ม never เลือก 30/60/90/custom/never ได้

## 18. Sensitive manager

Settings แสดงแอปที่เคยรับและสลับ normal/sensitive ใช้กับทั้งประวัติเก่าและใหม่ ซ่อน toast/list เปิดเฉพาะรายการ และซ่อนเมื่อ lock เป็นค่าเริ่มต้น แผนหลักให้สถานะอิง package ร่วมทุก device และล้าง reveal เมื่อเสีย selection/minimize/close/lock

## 19. ค้นหาและ filters

ค้นหา app/title/sender/body/preview แบบพื้นฐาน ตัด deleted เคารพ devices/apps ที่เลือก และไม่เผย sensitive preview จนเปิดรายการ Full-text ขั้นสูงเป็นงานอนาคต

## 20. Parser

รองรับ generic, style-aware, app-specific และ user rules ในอนาคต MVP มี Generic Android, MessagingStyle และ Gmail/email fallback ต้อง re-parse จาก raw ย้อนหลังได้

เป้าหมาย Advanced UI มี re-parse รายการเดียว/ทั้งแอป/ทั้งประวัติ, raw viewer และ export sample

## 21. Settings

| กลุ่ม | ตัวเลือกเป้าหมาย |
|---|---|
| General | startup, theme/system theme, storage, port, device name |
| Notifications | toast on/off, lock-screen content off, sensitive hidden, click-to-focus |
| Retention | archive เริ่ม 30 วัน, delete เริ่ม never พร้อม preset/custom |
| Devices | pair/list/rename/pause/revoke |
| Apps | list, sensitive state, unread count |
| Advanced | re-parse, raw, diagnostics, compression และงาน cleanup อนาคต |

## 22. Tray

เป้าหมายเมนู Open Inbox, Pair Device, Pause Sync, Settings, Quit และสถานะ connected/disconnected/unread เมื่อทำได้ ปัจจุบันเมนูมีเพียง Open/Exit ตามรายงานสถานะ

## 23. Retention job

รันเป็นระยะ Archive รายการเกินอายุที่ยังไม่ archive/delete และบันทึกเวลา Delete เมื่อไม่ได้ตั้ง never ให้ soft-delete ตามอายุ ไม่ลบแถวหรือไฟล์จริง แผนหลักให้นับจาก posted_at

## 24. Error handling

Protocol ส่ง structured error/log โดย server ไม่ล้ม DB failure แสดง warning และไม่ ACK ก่อน persist Asset hash ผิดให้ปฏิเสธไม่เก็บและ retry/mark failed Pairing ต้องแสดงเหตุผล token หมดอายุ, version mismatch หรือเชื่อมต่อไม่ได้

## 25. Security/privacy

ข้อมูลอยู่ในเครื่อง ไม่มี cloud คู่ต้อง revoke ได้ ซ่อน sensitive แสดง path ให้ชัด Database encryption และ Windows Hello/PIN เป็นงานอนาคต Lock screen ต้องไม่เผย preview

## 26. เกณฑ์ตรวจรับ

- เปิดเป็นผู้ใช้ปกติ มี tray และ server
- แสดง QR จับคู่และมี device entry
- event persist ก่อน ACK แสดง toast และคลิกเปิดรายการ
- unread/read/archive/delete ทำงานตามนโยบาย
- ตัวกรอง device/app แบบหลายรายการและ counts ถูกต้อง
- sensitive toast/list ซ่อน เปิดเฉพาะ item
- missing asset ถูกขอ ตรวจ hash และใช้ในรายการได้
- archive default 30 วัน, delete never และทุก delete เป็น soft delete

## 27. งานอนาคต

Custom toast, cleanup, thumbnail, full-text, DB encryption, Windows Hello/PIN, reply/dismiss, cloud relay, sync ระหว่าง Windows, backup export/import และ AI summary/grouping
