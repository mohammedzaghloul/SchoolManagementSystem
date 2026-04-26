# 📋 قائمة التحقق النهائية قبل الرفع (Production Checklist)

هذا التقرير يلخص الخطوات النهائية لضمان عمل نظام "المدرسة الذكية" بكفاءة وأمان على Railway.

## 1. إعدادات الـ Backend (Railway Variables)
يجب تعيين المتغيرات التالية في لوحة تحكم Railway لخدمة الـ Backend:
- [ ] `ASPNETCORE_ENVIRONMENT`: ضبطه على `Production`.
- [ ] `Jwt__Secret`: توليد سلسلة نصية طويلة وعشوائية (لا تستخدم القيمة الافتراضية).
- [ ] `ConnectionStrings__DefaultConnection`: رابط قاعدة بيانات SQL Server.
- [ ] `ConnectionStrings__Redis`: رابط خدمة Redis (يتم ربطه تلقائياً إذا أضفت خدمة Redis في نفس المشروع).
- [ ] `Resend__ApiKey`: مفتاح API الخاص بـ Resend لإرسال الإيميلات.
- [ ] `Resend__From`: الإيميل الرسمي للمرسل (مثلاً `auth@mail.mohammedzaghloul.publicvm.com`).

## 2. إعدادات الـ Frontend (Railway Variables)
يجب تعيين المتغيرات التالية لخدمة الـ Frontend:
- [ ] `API_URL`: رابط الـ Backend بعد الرفع (مثلاً `https://backend-production.up.railway.app`).
- [ ] `SIGNALR_URL`: نفس الرابط مضافاً إليه `/chathub`.

## 3. البيانات والملفات (Volumes)
بما أن السيرفر في Railway يمسح الملفات عند إعادة التشغيل:
- [ ] **SQL Server:** اربط Volume بمسار `/var/opt/mssql`.
- [ ] **Uploads:** اربط Volume بمسار `/app/wwwroot/uploads` (لحفظ صور المستخدمين).
- [ ] **Face Data:** اربط Volume بمسار `/app/data` في خدمة التعرف على الوجوه.

## 4. التحقق من الدومين (DNS)
- [ ] تأكد من أن حالة الدومين في Resend هي `Verified` (DKIM/SPF) لضمان وصول الإيميلات لـ Gmail.

## 5. التعديلات التي تمت مؤخراً
- [x] **OTP Workflow:** تم فصل صفحة استعادة كلمة المرور لزيادة الأمان وتحسين تجربة المستخدم.
- [x] **Security Hardening:** إزالة `devOtp` وتفعيل مفتاح API لخدمة التعرف على الوجوه.
- [x] **Rate Limiting:** تفعيل حدود الطلبات على نقاط النهاية الحساسة لحماية السيرفر.
- [x] **Direct Login:** تبسيط الدخول ليكون بكلمة المرور مباشرة بناءً على تدقيق الـ UX.
- [x] **Routing Cleanup:** توحيد مسارات المعلم وإضافة مسار مخصص للإشعارات.
