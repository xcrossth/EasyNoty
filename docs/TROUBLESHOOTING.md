# แก้ปัญหาเบื้องต้น

[English](TROUBLESHOOTING.en.md) | [สารบัญ](../README.md)

| อาการ | ตรวจอะไร |
|---|---|
| เปิด EXE แล้วแจ้ง Windows App Runtime หาย | ติดตั้ง runtime 2.2 แบบ x64; setup helper ยังไม่ติดตั้งให้อัตโนมัติ และเก็บไฟล์ประกอบ EXE ให้ครบ |
| หา .NET SDK 10.0.300 ไม่พบ | ใช้ .NET 10 SDK feature band 10.0.300 ขึ้นไป; `windows/global.json` ยอม roll forward ภายใน major/minor เดียวกัน ไม่ใช้ runtime แทน SDK |
| Gradle หา Java/SDK ไม่พบ | ตรวจ `JAVA_HOME` ว่ามี `bin/javac.exe` และตั้ง SDK ผ่าน environment หรือ ignored `android/local.properties` |
| `adb devices` ว่าง/unauthorized | เปิด USB debugging ตรวจ driver/สาย และกดยอมรับบนมือถือ |
| QR หมดอายุ | สร้าง pairing session ใหม่ อายุ 2 นาที ห้ามเผยแพร่ QR หรือ token |
| หา PC ไม่เจอ | ใช้ LAN เดียวกัน ตรวจ Wi-Fi client isolation, Private firewall TCP 38492 และ mDNS; เครือข่ายอาจบล็อก multicast |
| ไม่มีแจ้งเตือนจาก Android | ตรวจ Notification Access, allowlist, pause, คิว และ background policy; เปิดการแจ้งเตือน Windows ด้วย |
| ไอคอนที่ pin ยังเป็นรูปเก่า | ปิดแอปแล้วเปิด build ใหม่ หากเป็นเฉพาะ pinned shortcut ให้ unpin แล้ว pin ใหม่ |
| ปิดหน้าต่างแล้ว process ยังอยู่ | เป็นพฤติกรรม hide-to-tray; เลือก Exit ที่ tray เพื่อปิดจริง |
| ขนาดหน้าต่างไม่คืนค่า | ตรวจสิทธิ์เขียน storage; ไฟล์ placement ที่เสีย/ตำแหน่งอยู่นอกจอจะถูกข้าม |
| ไม่พบเมนูจัดการอุปกรณ์หรือ raw viewer | ยังเป็นงานค้าง ไม่ใช่การตั้งค่าเครื่องผิด ดูสถานะโครงการ |

การรายงานปัญหาให้ระบุ OS, ขั้นตอนทำซ้ำ, commit/build และ error ที่ตัดข้อมูลส่วนตัวแล้ว อย่าแนบโฟลเดอร์ `data/`, ฐานข้อมูล, credential, raw notification หรือภาพหน้าจอที่มีข้อความส่วนตัว ดู [ความปลอดภัย](../SECURITY.md)
