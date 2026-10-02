# เริ่มทดลอง EasyNoty

[English](GETTING_STARTED.en.md) | [สารบัญ](../README.md)

คู่มือนี้สำหรับ build ทดลอง ไม่ใช่รุ่นพร้อมใช้งานทั่วไป อ่าน [ข้อจำกัดปัจจุบัน](../logs/ACCEPTANCE_STATUS.md) ก่อน โดยเฉพาะ pairing และหน้าจอตั้งค่า

## เตรียมเครื่อง

- Windows 10 build 19041+ หรือ Windows 11 แบบ x64, .NET 10 SDK และ Windows App Runtime 2.2
- Android 11+ ที่เปิด USB debugging ได้, JDK 21 (target ของซอร์ส Java คือ 17), Android SDK Platform 36 และ Build Tools ที่ AGP ต้องการ
- Python 3.10+ และ Git
- PC และมือถืออยู่ LAN เดียวกัน ไม่มี Wi-Fi client isolation; receiver ใช้ TCP `38492` เป็นค่าเริ่มต้น

## Build และเปิด Windows

```powershell
git clone https://github.com/xcrossth/EasyNoty.git
cd EasyNoty
python scripts/check_docs.py
.\scripts\verify.ps1
```

ตั้ง `JAVA_HOME` และ `ANDROID_HOME`/`ANDROID_SDK_ROOT` ให้ตรงเครื่อง หรือระบุ Android SDK ใน `android/local.properties` ซึ่งไม่เข้า Git ดูรายละเอียดในคู่มือ [Windows](../windows/README.md) และ [Android](../android/README.md)

เปิด `EasyNoty.Desktop.exe` จาก Release โดยเก็บไฟล์ประกอบไว้ด้วย แอปรันด้วยสิทธิ์ปกติ ปิดหน้าต่างแล้วจะซ่อนลง tray เลือก **Exit** ที่ tray เพื่อออกจริง

หาก firewall ปิดกั้น ใช้ setup helper ตามคู่มือ Windows เพื่อเพิ่มกฎ Private network เฉพาะ helper ต้องใช้ admin และ helper ยังไม่ติดตั้ง Runtime ให้อัตโนมัติ

ข้อมูลอยู่ใน `data/` ข้างแอปเมื่อเขียนได้ มิฉะนั้นใช้ `%LocalAppData%/EasyNoty` ดูตำแหน่งจริงจาก Settings และอย่าอัปโหลดข้อมูลนี้

## ติดตั้งและทดลอง Android

```powershell
adb devices
adb install -r android/app/build/outputs/apk/debug/app-debug.apk
```

1. ให้ Notification Access แล้วเลือกแอปทดสอบใน allowlist ไม่มีแอปที่ถูกอนุญาตโดยอัตโนมัติ
2. เริ่มด้วย Balanced mode กด **Pair device** บน Windows แล้วสแกน QR จาก Android ภายใน 2 นาที
3. อนุญาตกล้องและกด **Accept pending device** บน Windows เมื่อมีอุปกรณ์รอ
4. ส่งแจ้งเตือนสมมติจากแอปที่อนุญาต ตรวจ Inbox, toast และคิวส่ง

ข้อจำกัด: หน้าจอเทียบรหัส 6 หลักก่อน Accept ยังไม่ครบ ทดลองเฉพาะอุปกรณ์และ LAN ที่ไว้ใจได้ และยังไม่มีผลยืนยัน flow นี้ครบเส้นทางกับมือถือจริง

ตรวจ grouped/ongoing notifications, รูปภาพ, offline retry, Wi-Fi/IP เปลี่ยน, Doze, Realtime และ privacy ตาม [รายการตรวจรับ](../logs/ACCEPTANCE_STATUS.md) รายงานผลโดยไม่แนบ QR, token หรือข้อความส่วนตัว
