# ความปลอดภัยและข้อมูลส่วนตัว

[English](SECURITY.en.md) | [สารบัญ](README.md)

EasyNoty ประมวลผลข้อมูลแจ้งเตือนที่อาจเป็นส่วนตัว Repository นี้เป็น public จึงเก็บเฉพาะซอร์ส เอกสาร assets สำหรับออกแบบ และข้อมูลทดสอบสมมติ

## สิ่งที่ห้าม commit

- token, password, API key, private key, certificate ที่มี private key และคีย์ signing Android
- `.env`, `local.properties`, credential settings, `mock-android-state.json`
- `data/`, SQLite/WAL, runtime logs, QR pairing จริง, notification export และข้อมูลสำรอง
- EXE/APK และผล build ที่อาจรวมข้อมูลเฉพาะเครื่อง

`.gitignore` ช่วยกันการเพิ่มไฟล์ตามชื่อ แต่ไม่สามารถตรวจ secret ที่ฝังในซอร์สได้ ต้องตรวจ staged diff และใช้ secret scanner ก่อน push ด้วย

## ข้อมูลทดสอบที่เผยแพร่ได้

`protocol/fixtures/`, `protocol/vectors/hmac-sha256.json` และค่าคงที่ใน tests เป็นข้อมูลสมมติสำหรับตรวจ contract/HMAC เท่านั้น ไม่ใช่ credential ที่ใช้งานจริง แอปจริงสร้าง secret แบบสุ่ม ห้ามนำ test key ไปใช้กับอุปกรณ์จริง

`.gitleaks.toml` ใช้กฎมาตรฐานและยกเว้นเฉพาะ token ตัวอักษร hex ซ้ำกับ HMAC key ที่สร้างจาก bytes 0–31 ในไฟล์ทดสอบที่ระบุ ทั้ง path และค่าต้องตรงพร้อมกัน ไม่ได้ยกเว้นโฟลเดอร์ tests/fixtures ทั้งหมด

## ข้อจำกัดผลิตภัณฑ์

มีโค้ด WSS certificate pinning, HMAC, DPAPI และ Android Keystore แต่ยังไม่ผ่านการตรวจรับ end-to-end ครบ การเทียบรหัส pairing และบางหน้าจอ privacy ยังมีงานค้าง ไม่มี encryption at rest สำหรับประวัติแจ้งเตือนใน MVP และ soft delete ไม่ลบข้อมูลหรือ assets ออกจากดิสก์

## รายงานช่องโหว่

อย่าเปิด public issue พร้อม secret, ข้อมูลผู้ใช้ หรือรายละเอียด exploit ที่ยังไม่แก้ หากหน้า Security ของ GitHub มี **Report a vulnerability** ให้ใช้ช่องทางนั้น หากยังไม่มี ให้ติดต่อผู้ดูแลเพื่อขอช่องทางส่วนตัวก่อนส่งรายละเอียด ขณะนี้ไม่ได้ประกาศอีเมลส่วนตัวหรือรับรองว่าระบบ private reporting เปิดอยู่

ถ้า credential หลุดต้อง revoke/rotate ที่ต้นทางก่อน การลบไฟล์จาก commit ล่าสุดอย่างเดียวไม่ลบออกจากประวัติ Git
