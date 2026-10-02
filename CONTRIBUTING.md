# ร่วมพัฒนา EasyNoty

[English](CONTRIBUTING.en.md) | [สารบัญ](README.md)

## โครงสร้างและขอบเขต

ทำงานใน monorepo เดียว อ่าน [แผนหลัก](03_EASYNOTY_IMPLEMENTATION_PLAN.md) และ [สถานะปัจจุบัน](logs/ACCEPTANCE_STATUS.md) ก่อนเปลี่ยนพฤติกรรม อย่าตีความข้อกำหนดในสเปกว่าเป็นฟีเจอร์ที่ใช้งานได้แล้ว

เมื่อเปลี่ยน wire contract ให้แก้ schema, fixtures, C# และ Kotlin ร่วมกัน พร้อมทดสอบทั้งสองฝั่ง ห้ามเปลี่ยนความหมายของ Protocol v1 เงียบ ๆ

## Git และอีเมล

ตั้งชื่อและ GitHub noreply ของบัญชีตัวเองแบบ repository-local ก่อน commit ดูค่าจริงจาก GitHub Settings → Emails ไม่ใช้ Gmail ส่วนตัว

```powershell
git config --local user.name "YOUR_GITHUB_LOGIN"
git config --local user.email "YOUR_GITHUB_NOREPLY_EMAIL"
git config --local user.useConfigOnly true
git var GIT_AUTHOR_IDENT
git var GIT_COMMITTER_IDENT
```

ตรวจทั้ง author และ committer รวมถึง environment variables ที่อาจ override config ไม่แนบ token ลง remote URL ใช้ `gh auth login`/credential manager ตามปกติ

## เอกสารสองภาษา

ไฟล์ `NAME.md` เป็นภาษาไทย ส่วน `NAME.en.md` เป็นภาษาอังกฤษ ต้องคงความหมายตรงกัน เพิ่มลิงก์สลับภาษาและอัปเดตสารบัญทั้งสองหน้าเมื่อเพิ่มคู่มือ สเปกเก่าคงไว้เป็นประวัติและต้องมีคำอธิบายว่าถูกแผน/contract ใหม่แทนที่ตรงไหน

## ก่อนส่งงาน

```powershell
python scripts/check_docs.py
.\scripts\verify.ps1
git diff --check
git diff --cached --stat
git diff --cached
```

หากมี Gitleaks ให้สแกนก่อน push ด้วย `gitleaks git --redact` หลัง commit และตรวจ staged files ก่อน commit ตามคู่มือเครื่องมือ ห้ามแสดง secret ลง log การอนุญาต false positive ต้องจำกัดเฉพาะค่า/ไฟล์ทดสอบที่ตรวจแล้ว

อย่า commit build, runtime data, credential หรือไฟล์เฉพาะเครื่องตาม [นโยบายความปลอดภัย](SECURITY.md) ผลทดสอบที่ต้องใช้มือถือหรือ LAN ให้บันทึกว่า “ยังไม่ทดสอบ” หากไม่ได้ทำจริง

ยังไม่มีสัญญาอนุญาตซอร์สที่เลือกไว้ จึงไม่ควรเพิ่ม LICENSE หรือประกาศสิทธิ์แทนเจ้าของโดยไม่ได้ตกลงกัน
