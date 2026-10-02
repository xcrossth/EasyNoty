# EasyNoty สำหรับ Windows

[English](README.en.md) | [สารบัญ](../README.md)

แอป x64 แบบ unpackaged ใช้ .NET 10, WinUI 3 และ Windows App SDK 2.2 เก็บประวัติใน SQLite และรับ Protocol v1 ผ่าน WSS ด้วยสิทธิ์ผู้ใช้ปกติ นี่คือ build ทดลอง อ่าน [งานค้าง](../logs/ACCEPTANCE_STATUS.md) ก่อนใช้งาน

## เครื่องมือที่ต้องมี

- Windows 10 build 19041 ขึ้นไปแบบ x64
- .NET 10 SDK ตาม `global.json` ซึ่งกำหนดขั้นต่ำ 10.0.300 และยอมรับ feature band ใหม่กว่าใน .NET 10
- Windows SDK และเครื่องมือ build WinUI ตามที่ Visual Studio/Windows App SDK ต้องการ
- Windows App Runtime 2.2 สำหรับเปิดแอป และ .NET 10 Desktop/ASP.NET Core runtimes ตาม dependency ของแอป; เครื่องพัฒนาที่มี SDK มี runtime หลักอยู่แล้ว

## Build และทดสอบ

รันจาก root ของ repository:

```powershell
.\windows\scripts\test.ps1
.\windows\scripts\build.ps1
```

หรือใช้ `.\scripts\verify.ps1 -SkipAndroid` เพื่อตรวจ protocol และ Windows พร้อมกัน สคริปต์ build จะ restore/build solution และ publish setup helper

ผล desktop อยู่ใต้ `windows/src/EasyNoty.Desktop/bin/Release/net10.0-windows10.0.19041.0/` (อาจมีโฟลเดอร์ platform เพิ่มเมื่อเปลี่ยนการตั้งค่า build) ให้เปิด `EasyNoty.Desktop.exe` พร้อมไฟล์ประกอบทั้งโฟลเดอร์ ไม่ใช่คัดลอก EXE เพียงไฟล์เดียว

Setup อยู่ใต้ `windows/src/EasyNoty.Setup/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/` เป็น self-contained ส่วน desktop ยังเป็น framework-dependent

## การติดตั้งและ firewall

เปิด desktop ด้วยสิทธิ์ปกติ ถ้าขาด Windows App Runtime ให้ติดตั้ง runtime 2.2 แบบ x64 ตามข้อความของ Windows ก่อน

Setup helper ต้องใช้ admin เฉพาะตอนเพิ่มกฎ inbound TCP `38492` สำหรับ Private network ปัจจุบัน helper ยังไม่ตรวจ/ติดตั้ง Runtime อัตโนมัติ รองรับ `--port <number>` แต่การเปลี่ยนพอร์ต helper ไม่ได้เปลี่ยนค่าพอร์ต receiver ให้ด้วย

อย่าเปิด receiver ออกอินเทอร์เน็ตหรือเพิ่มกฎ Public network โดยไม่จำเป็น ดู [ความปลอดภัย](../SECURITY.md)

## ใช้งานหน้าต่าง

- เลือก Inbox / Archived / All และค้นหาข้อความ
- เลือกอุปกรณ์และแอปเป็นตัวกรองแบบหลายรายการได้
- เปิดรายการเพื่ออ่าน ใช้ Mark read, Archive และ Delete; Delete เป็น soft delete
- ปิดหน้าต่างแล้วซ่อนลง tray; เมนู tray มี Open และ Exit
- จำขนาด/ตำแหน่งหน้าต่างใน `window-placement.json`; แถบสถานะด้านล่างปิดไม่ได้
- ใช้ไอคอนเดียวกันสำหรับ EXE, หน้าต่าง/taskbar และ tray

บางส่วนของสเปกยังไม่มีหน้าจอ เช่น rename/pause/revoke, ตั้ง sensitive, raw viewer/re-parse และ storage migration การคลิก toast ยังล้างตัวกรอง และหน้าจอเทียบรหัส pairing ยังไม่ครบ

## ข้อมูลในเครื่อง

ใช้ `<AppFolder>/data` หากเขียนได้ มิฉะนั้นใช้ `%LocalAppData%/EasyNoty` หน้าจอแสดงตำแหน่งจริง เก็บฐานข้อมูล, assets, log และคีย์ไว้ที่นี่ ห้าม commit โฟลเดอร์นี้ การลบแจ้งเตือนไม่ได้ลบไฟล์ asset จริง

## Mock Android

มี CLI ใน `src/EasyNoty.MockAndroid/` รองรับ:

```text
pair <wss://host:port> <pairing-token> <pc-uuid> <sha256:fingerprint> [android-uuid]
send normal|ongoing|group|large|duplicate|malformed
asset <file> [mime]
offline
```

คำสั่ง pair ใช้ข้อมูลจาก pairing session และรอ Accept บน Windows ปัจจุบันหน้าจอแสดง QR แต่ยังไม่มีปุ่มคัดลอก JSON จึงยังไม่ใช่เส้นทางทดสอบที่สะดวกครบถ้วน ห้ามแนบ token ลง issue หรือเก็บใน shell script ที่ commit

Mock เก็บ secret ใน `mock-android-state.json` ของ working directory ไฟล์นี้ถูก ignore และใช้เฉพาะการพัฒนา ไม่ใช่ตัวอย่างวิธีเก็บ credential สำหรับแอปจริง โหมด offline เพียงไม่เชื่อมต่อ ไม่ใช่หลักฐานว่าทดสอบ retry ผ่านแล้ว
