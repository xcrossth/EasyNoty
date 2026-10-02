# EasyNoty สำหรับ Android

[English](README.en.md) | [สารบัญ](../README.md)

แอป Android 11+ เขียนด้วย Kotlin/Compose จับแจ้งเตือนเฉพาะแอปใน allowlist แล้วส่ง raw snapshot และ assets ไป Windows ผ่าน WSS ที่ตรวจ certificate fingerprint

## Build

ใช้ JDK 21 ซึ่งเคยทดสอบกับโครงการนี้, Android SDK Platform 36 และ Build Tools ที่ AGP 9.2 ต้องการ ซอร์สกำหนด Java target 17 และใช้ Gradle Wrapper 9.4.1

ตั้ง `JAVA_HOME` ไปยัง JDK และ `ANDROID_HOME`/SDK path ให้ถูกต้อง อย่า commit `local.properties` หรือคีย์ signing

```powershell
cd android
.\gradlew.bat testDebugUnitTest assembleDebug
# ตรวจ lint เพิ่มได้
.\gradlew.bat lintDebug
```

APK อยู่ที่ `app/build/outputs/apk/debug/app-debug.apk` เป็น debug build สำหรับ ADB/sideload ยังไม่มี Play Store/AAB หรือ release signing ที่จัดเตรียมไว้

## ติดตั้ง

จากโฟลเดอร์ `android/`:

```powershell
adb devices
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

1. เปิดแอปและให้ Notification Access
2. เลือกแอปใน **Allowed apps** ไม่มีแอปที่เปิด sync ให้อัตโนมัติ
3. เปิด **Scan QR code** เพื่อสแกน QR จาก Windows ใช้ CameraX/ML Kit ในเครื่อง หรือกรอก QR JSON ด้วยตนเอง
4. เลือก Balanced เริ่มต้น; Realtime ใช้ foreground service ส่วน Battery Saver ใช้การส่งเป็นชุดผ่าน WorkManager
5. เลือกว่าจะรับเฉพาะข้อมูลใหม่หรือ backfill cache 7 วันตอนจับคู่ PC

การสแกนไม่อัปโหลดภาพกล้อง ดู [คู่มือเริ่มต้น](../docs/GETTING_STARTED.md) สำหรับข้อจำกัด pairing ปัจจุบัน

## การเก็บและส่งข้อมูล

- ใช้ Room เก็บ raw/revisions และสถานะส่งแยกแต่ละ PC
- เก็บ secret ด้วย Android Keystore; certificate pinning และ HMAC ใช้ตาม Protocol v1
- Event/asset จะสำเร็จเมื่อได้รับ ACK ที่ถูกต้องเท่านั้น
- PC ที่ pause ยังคงมี backlog; revoke ยกเลิกภาระส่งของคู่นั้น
- Asset อ้างอิงด้วย SHA-256 จำกัด 25 MiB; GC ต้องเคารพ reference และรายการส่งค้าง
- ปิด Sync images แล้วยังต้องส่ง app icon ตาม contract
- Android เป็น cache/queue ไม่ใช่ archive หลัก

## การทดสอบ

มี local tests สำหรับ hash, compression, notification instance, pairing payload, HMAC, Room และ protocol fixtures โดยดึง resources จาก `../protocol/` จึงต้อง clone monorepo ทั้งชุด

Unit tests และการสร้าง APK ไม่ยืนยัน NotificationListenerService, กล้อง, Doze, OEM background policy หรือการส่งถึง Windows ต้องตรวจบนมือถือจริงตาม [สถานะการตรวจรับ](../logs/ACCEPTANCE_STATUS.md)
