# บันทึกการพัฒนาหลัก

[English](MAIN_WORKLOG.en.md) | [สารบัญ](../README.md)

บันทึกนี้เป็นประวัติ ตรวจสถานะล่าสุดจาก [การตรวจรับ](ACCEPTANCE_STATUS.md) ข้อความเดิมที่สรุปว่าทำครบและเหลือแค่มือถือจริงถูกแก้สถานะในการตรวจ 3 ตุลาคม 2026

## 5–10 กรกฎาคม 2026

- ยอมรับและบันทึกแผนหลัก EasyNoty เริ่ม monorepo และงาน protocol/Windows/Android พร้อมแผนย่อยและบันทึก
- สภาพแวดล้อมเดิม Windows 10 22H2 x64, .NET SDK 10.0.300, Visual Studio 18 Insiders, Android SDK ผ่าน path ในเครื่อง
- Initialize Git และเพิ่ม integration plan; งานยังไม่ได้ commit ในช่วงนั้น
- เพิ่ม root verify.ps1 ตรวจ protocol, Windows และ Android
- Protocol ผ่าน 9 schemas/9 valid/9 invalid/1 HMAC vector
- รวมโค้ดหลัง workstreams ติด usage limit และตรวจ Android ด้วย JDK 21 ผ่าน unit/build APK
- แก้ root verification ให้รัน Gradle จากโฟลเดอร์ Android
- เพิ่ม QR dialog, reveal/read behavior, toast routing, hide-to-tray และ Open/Exit; Windows build/tests ผ่าน
- เพิ่ม CameraX/ML Kit scanner และ Windows DNS-SD/mDNS พร้อม endpoint fallback
- รอบรวมเดิม protocol ผ่าน, Windows tests 11/11, Android unit/build ผ่าน และ ADB ไม่พบอุปกรณ์
- รายงานเดิมเรียกสิ่งเหล่านี้ว่า “ครบ” แต่ต่อมาพบ UI/flow และ acceptance ที่ค้าง จึงไม่ถือข้อความนั้นเป็นหลักฐานตรวจรับ

## 11 กรกฎาคม 2026

- แก้ปุ่ม Refresh/Pair device ไม่ให้ชน caption buttons
- บันทึกขนาด/ตำแหน่งหน้าต่าง และทำแถบสถานะด้านล่างให้ปิดไม่ได้
- ออกแบบไอคอนจนได้ N ริบบิ้นบนพื้นน้ำเงินมืด dark-v3; แบบ keyed/transparent เก็บเป็นประวัติ
- สร้าง ICO หลายขนาด ฝัง EXE และใช้กับ AppWindow/taskbar/tray; build ผ่าน การยืนยันภาพทุกพื้นผิวต้องทดสอบแยก

## 3 ตุลาคม 2026

- ตรวจโค้ดเทียบแผน พบ Windows UI ที่ยังไม่เชื่อม, pairing-code flow ไม่ครบ, toast ล้าง filters และ setup ไม่ติดตั้ง Runtime อัตโนมัติ
- ตรวจ Git พบว่ายังไม่มี commit และยังไม่มี remote
- เตรียมเผยแพร่ monorepo ไป xcrossth/EasyNoty ตามคำขอเจ้าของ ตั้ง author/committer เป็น GitHub noreply เฉพาะ repo
- จัดเอกสารภาษาไทยเป็นหน้าหลักและฉบับอังกฤษแยกทุกหน้า พร้อมสารบัญและข้อจำกัดตามจริง
- เพิ่ม .gitignore สำหรับ runtime secrets/DB/mock state/keys/build/local settings และไม่รวม archive สเปกซ้ำ
- ปรับ SDK roll-forward ภายใน .NET 10 และเลือก JDK ที่มี javac และเวอร์ชันอย่างน้อย 17 แทน path เฉพาะเครื่อง
- Protocol ผ่าน, Windows build ผ่านไม่มี warning/error และ tests 11/11; Android unit/build สำเร็จจาก up-to-date cache (รายงานเดิม 14/14)
- ไม่มีการทดสอบมือถือจริงหรือแก้ฟีเจอร์ที่ค้างในรอบเตรียม repository นี้
