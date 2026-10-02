# แผนรวมระบบ EasyNoty

[English](00_INTEGRATION_PLAN.en.md) | [สารบัญ](../README.md)

## เป้าหมาย

รวม Protocol v1, Windows และ Android ใน monorepo ที่ build ซ้ำได้ และพิสูจน์เส้นทางทำงานข้ามแพลตฟอร์ม

## ลำดับงาน

1. ใช้สเปกและแผนหลักที่ root เป็นข้อกำหนด
2. แบ่งงานใน protocol/windows/android พร้อมแผนและบันทึกของแต่ละส่วน
3. ตรวจ C#/Kotlin models เทียบ schema และ golden fixtures
4. มี entrypoint ตรวจ protocol, Windows restore/build/test และ Android unit/build
5. ทดสอบ Windows receiver ด้วย Mock ก่อนทดสอบมือถือจริง
6. ต่อ ADB ติดตั้ง debug APK จับคู่ผ่าน LAN ตรวจ event persistence/assets/toast/pause/retry/privacy
7. บันทึกหลักฐาน device-only และข้อจำกัดสภาพแวดล้อมตามจริง

## เกณฑ์ผ่าน

- Restore/build ได้จาก clean checkout และ automated tests ผ่าน
- ทั้งสองฝั่งอ่าน protocol fixtures ได้
- Windows persist ก่อน ACK และ deduplicate retry
- Android เข้าคิวขณะ offline/pause แล้ว retry หลัง reconnect
- Diagnostic ปกติไม่มีข้อความหรือ secrets
- Desktop ไม่ elevated; ขอ UAC ได้เฉพาะ setup helper

## การแบ่งงานเดิม

ผู้รวมระบบดูแล root verification, integration และ acceptance; ส่วน protocol/Windows/Android ดูแลโค้ด แผน และบันทึกในขอบเขตของตน การแบ่งนี้เป็นประวัติการพัฒนา ไม่ใช่การยืนยันว่าทุกเกณฑ์ผ่านแล้ว ดู [สถานะ](../logs/ACCEPTANCE_STATUS.md)
