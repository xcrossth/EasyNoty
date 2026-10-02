# EasyNoty

[English](README.en.md)

![ไอคอน EasyNoty](assets/branding/easynoty-icon-dark-v3-64.png)

EasyNoty ส่งแจ้งเตือนจากแอปที่คุณเลือกบน Android มายัง Windows ผ่านเครือข่าย LAN โดย Windows เป็นกล่องข้อความและที่เก็บประวัติหลัก ไม่มี cloud relay ใน MVP

นี่คือ **monorepo** ที่รวมแอป Windows, Android, สัญญาการสื่อสาร, ชุดทดสอบ, ไอคอน และเอกสารพัฒนาไว้ด้วยกัน

## สถานะ

อยู่ระหว่างพัฒนา MVP มีซอร์สและ build ทดลอง แต่ **ยังไม่ผ่านการตรวจรับครบตามแผน** บางฟีเจอร์มีเฉพาะ service และยังไม่เชื่อมกับหน้าจอ Windows รวมถึงยังไม่มีผลทดสอบครบเส้นทางกับมือถือจริง อ่าน [สถานะและงานค้าง](logs/ACCEPTANCE_STATUS.md) ก่อนทดลอง

หน้าตาแอปยังเป็นภาษาอังกฤษ คู่มือใช้ภาษาไทยเป็นหน้าหลัก และทุกหน้า Markdown มีฉบับ `.en.md` แยกพร้อมลิงก์สลับภาษา

## เริ่มต้น

```powershell
git clone https://github.com/xcrossth/EasyNoty.git
cd EasyNoty
```

อ่าน [คู่มือเริ่มทดลอง](docs/GETTING_STARTED.md) แล้ว build ตามคู่มือ [Windows](windows/README.md) และ [Android](android/README.md) ใช้ข้อมูลแจ้งเตือนสมมติในช่วงทดสอบ

Repository เก็บซอร์ส ไม่เก็บ EXE/APK, ฐานข้อมูลแจ้งเตือน, คีย์, pairing state หรือไฟล์ตั้งค่าเฉพาะเครื่อง

## โครงสร้าง

| โฟลเดอร์ | หน้าที่ |
|---|---|
| `windows/` | C# / .NET 10 / WinUI 3: desktop app, receiver, SQLite, setup helper, mock Android และ tests |
| `android/` | Kotlin / Compose: Notification Access, allowlist, Room queue, QR pairing และ sync |
| `protocol/` | Protocol v1, JSON Schema, fixtures และ HMAC vector ที่ใช้ร่วมกัน |
| `assets/branding/` | ไอคอนต้นฉบับที่เลือกและแบบทดลอง |
| `scripts/` | ตรวจสอบโครงการและเอกสารสองภาษา |
| `docs/` | คู่มือเริ่มต้นและแก้ปัญหา |
| `plans/` | แผน integration และแผนย่อย |
| `logs/` | บันทึกพัฒนาและตรวจรับ ไม่ใช่ log แจ้งเตือนของผู้ใช้ |

Android จับแจ้งเตือน → บันทึกคิว → ส่งผ่าน WSS ที่จับคู่แล้ว → Windows ตรวจสอบและบันทึกก่อน ACK → แสดง Inbox/toast ส่วนรูปภาพส่งเมื่อ Windows ยังไม่มี hash นั้น

## สารบัญ

| หัวข้อ | ภาษาไทย | English |
|---|---|---|
| เริ่มทดลอง | [อ่าน](docs/GETTING_STARTED.md) | [Read](docs/GETTING_STARTED.en.md) |
| Windows | [อ่าน](windows/README.md) | [Read](windows/README.en.md) |
| Android | [อ่าน](android/README.md) | [Read](android/README.en.md) |
| แก้ปัญหา | [อ่าน](docs/TROUBLESHOOTING.md) | [Read](docs/TROUBLESHOOTING.en.md) |
| ร่วมพัฒนาและ Git | [อ่าน](CONTRIBUTING.md) | [Read](CONTRIBUTING.en.md) |
| ความปลอดภัย | [อ่าน](SECURITY.md) | [Read](SECURITY.en.md) |
| สถานะและงานค้าง | [อ่าน](logs/ACCEPTANCE_STATUS.md) | [Read](logs/ACCEPTANCE_STATUS.en.md) |
| ภาพรวมระบบ | [อ่าน](00_SYSTEM_OVERVIEW.md) | [Read](00_SYSTEM_OVERVIEW.en.md) |
| สเปก Android | [อ่าน](01_ANDROID_APP_SPEC.md) | [Read](01_ANDROID_APP_SPEC.en.md) |
| สเปก Windows | [อ่าน](02_WINDOWS_APP_SPEC.md) | [Read](02_WINDOWS_APP_SPEC.en.md) |
| แผนหลัก | [อ่าน](03_EASYNOTY_IMPLEMENTATION_PLAN.md) | [Read](03_EASYNOTY_IMPLEMENTATION_PLAN.en.md) |
| Protocol v1 | [อ่าน](protocol/README.md) | [Read](protocol/README.en.md) |
| แผน integration | [อ่าน](plans/00_INTEGRATION_PLAN.md) | [Read](plans/00_INTEGRATION_PLAN.en.md) |
| แผน protocol | [อ่าน](plans/01_PROTOCOL_V1_PLAN.md) | [Read](plans/01_PROTOCOL_V1_PLAN.en.md) |
| แผน Windows | [อ่าน](plans/02_WINDOWS_MVP_PLAN.md) | [Read](plans/02_WINDOWS_MVP_PLAN.en.md) |
| แผน Android | [อ่าน](plans/03_ANDROID_MVP_PLAN.md) | [Read](plans/03_ANDROID_MVP_PLAN.en.md) |
| บันทึกหลัก | [อ่าน](logs/MAIN_WORKLOG.md) | [Read](logs/MAIN_WORKLOG.en.md) |
| บันทึก protocol | [อ่าน](logs/subagents/protocol_v1.md) | [Read](logs/subagents/protocol_v1.en.md) |
| บันทึก Windows | [อ่าน](logs/subagents/windows_mvp.md) | [Read](logs/subagents/windows_mvp.en.md) |
| บันทึก Android | [อ่าน](logs/subagents/android_mvp.md) | [Read](logs/subagents/android_mvp.en.md) |
| ไฟล์ไอคอน | [อ่าน](assets/branding/README.md) | [Read](assets/branding/README.en.md) |

สเปก `00`–`02` เป็นร่างเป้าหมายเดิม แผนหลักเป็นข้อสรุปภายหลัง ส่วน field และกติกาบนสายส่งให้ยึด `protocol/schemas/` การมีข้อกำหนดในเอกสารไม่ได้หมายความว่าทำเสร็จแล้ว

## ตรวจสอบ

```powershell
python scripts/check_docs.py
.\scripts\verify.ps1
```

ต้องมี Python 3.10+, .NET 10 SDK, JDK และ Android SDK ตามคู่มือ `verify.ps1` ตรวจ protocol, restore/build/test Windows และ unit tests/build APK Android ใช้ `-SkipAndroid` หรือ `-SkipWindows` เพื่อตรวจเฉพาะส่วนได้

ยังไม่ได้เลือกสัญญาอนุญาตซอร์ส การเป็น public repository ไม่ได้ให้สิทธิ์ภายใต้ MIT หรือสัญญาอนุญาตอื่นโดยอัตโนมัติ
