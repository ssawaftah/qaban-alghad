# خريطة الملفات والأكواد

هذه الخريطة تحدد مكان التعديل وتمنع إضافة كل ميزة إلى ملف واحد. المسارات في الجدول نسبية إلى `src/QabanAlGhad/` ما لم يذكر خلاف ذلك.

| الملف | الفئات/الدوال الأساسية | مسؤوليته ومتى يتغير |
| --- | --- | --- |
| `Program.cs` | `Program.Main` | بدء WinForms وقفل النسخة الواحدة؛ لا يفسر الوزن |
| `Properties/AssemblyInfo.cs` | AssemblyVersion/FileVersion | رقم الملف وبيانات المنتج؛ يحدث مع `VERSION` |
| `Configuration/BridgeSettings.cs` | `Settings` | الحقول الافتراضية وعقد JSON؛ يلزم ترحيل عند تغيير الاسم أو المعنى |
| `Domain/Weighing/ScaleProtocol.cs` | `Protocol.Parse`, `Payload` | صيغ ST/GS/NT، السالب والصفر، وجسم إرسال النظام الأساسي |
| `Domain/Weighing/SerialFrameBuffer.cs` | `Frames.Push` | الأجزاء المتقطعة وCR/LF وحد 16384؛ لا يرسل HTTP |
| `Domain/Weighing/ReadingPolicy.cs` | `IsFresh`, `Deadline` | عمر العينة والمهلة المتبقية |
| `Domain/Delivery/DeliverySchedule.cs` | `CanSend`, `PeriodicStart`, `PeriodicFailure` | جدولة محاولات الوجهة دون إنشاء طلبات متوازية لها |
| `Domain/Delivery/TransferFeedback.cs` | `Begin`, `BodyWritten`, `Reply`, `NoReply` | فصل اكتمال الكتابة عن إقرار الخادم |
| `Domain/Delivery/ResponseRules.cs` | `KnownSuccess`, `Matches`, `ExplicitFailure`, `IsHtml` | تفسير JSON والردود الفعلية دون مطابقة نصية مضللة |
| `Application/Delivery/OutputChannel.cs` | `Configure`, `Pump`, `Send`, `Stop` | دورة الطلب وعزل الوجهات وصياغة جسم الوجهة الثانية |
| `Infrastructure/Http/ApiTransport.cs` | `ApiTransport.SendAsync`, `ApiReply` | HTTP/TLS/Bearer/التحويلات/حدود الجسم/Retry-After |
| `Infrastructure/Security/LocalSecrets.cs` | `Protect`, `Unprotect` | DPAPI/CurrentUser؛ لا تغير entropy دون ترحيل |
| `Infrastructure/Configuration/Bootstrap.cs` | `Bootstrap.Import`, `ReturnsSetup` | استيراد ملف خاص، تشفير الرمز، حفظه، حذف ملف الاستيراد |
| `UI/MainForm.cs` | الحقول وconstructor | تركيب الخدمات، ربط الأزرار، مؤقت100ms، إغلاق النافذة |
| `UI/MainForm.Layout.cs` | `BuildInterface`, `BuildOverview`, `BuildSettings`, `RefreshInterface` | شكل الواجهة وتبويباتها ومحاذاتها؛ لا تغيّر عقود API هنا |
| `UI/MainForm.Configuration.cs` | `LoadSettings`, `Apply`, `ReadSettings` | تحميل/عرض/التحقق من الإعدادات والقيم المسموحة |
| `UI/MainForm.Weighing.cs` | `StartReading`, `StopReading`, `CloseSerial`, `Tick` | فتح المنفذ، القراءة، آخر وزن، ثم ضخ القناتين |
| `UI/MainForm.Diagnostics.cs` | `WriteLog`, `WriteBackgroundLog`, `SaveReply` | السجلات والملفات التشخيصية مع تدوير محدود |
| `UI/Controls/SurfacePanel.cs` | `SurfacePanel.OnPaint` | رسم البطاقة فقط |
| `Assets/` | ICO/PNG/SVG | الأيقونة ومصدرها؛ لا تضع إعدادات هنا |
| `App.config` | supportedRuntime | تشغيل CLR4 باستهداف .NET Framework 4.5.1 |

## التحديثات

راجع ملفات Infrastructure/Updates وUI/MainForm.Updates.cs ومشروع QabanAlGhad.Updater في [دليل التحديثات](ONLINE_UPDATES.md).

## مشروع الاختبارات

| الملف | نطاقه |
| --- | --- |
| `tests/QabanAlGhad.Tests/Program.cs` | نقطة تشغيل الاختبارات، 13 فحص بروتوكول وتأطير، وتوزيع أوضاع CLI |
| `CompatibilityTests.cs` | 97 فحص توافق وجدولة وردود وIL، و56 تحقق HTTP loopback |
| `DualDeliveryTests.cs` | 29 تحقق استقلال الوجهات والرموز والتهيئة |

الاختبارات تشير إلى ملف التطبيق المبني وتفحص الكود نفسه، ولا تعيد نسخ مصدر المحلل داخلها. لا تدخل ملفات الاختبار في مشروع التطبيق أو حزمة المستخدم.

## أين تضيف التغيير؟

| التغيير المطلوب | ابدأ هنا | التحقق اللازم |
| --- | --- | --- |
| ميزان بصيغة مختلفة | ScaleProtocol + SerialFrameBuffer | رسائل حقيقية منزوعة الحساسية، وأجزاء متقطعة وإطارات تالفة |
| عنوان/رمز وجهة ثانية | Bootstrap + OutputChannel | عدم تغيير الوجهة الأساسية، وعدم اختلاط الرموز |
| إخفاء/تحريك عنصر | MainForm.Layout | فحص UI على Windows، والمحاذاة ومقاسات النافذة |
| تغيير فاصل الإرسال | OutputChannel + DeliverySchedule | ثبات الوزن، بطء الخادم، عدم تداخل الطلبات |
| إضافة تحديثات | مكونات جديدة وفق ONLINE_UPDATES | فشل التنزيل، التوقيع، الرجوع، وإيقاف القراءة قبل التثبيت |
| معالجة حفظ وزن المرتجعات | مشروع الموقع المستقل | ليس تعديلًا في هذا المستودع |

## أسلوب التغيير

حافظ على commit يشرح المشكلة والسلوك الناتج والتحقق. لا تغيّر التحليل والتشفير والواجهة وآلية التحديث في تعديل واحد يصعب مراجعته. أسماء بعض الفئات قصيرة لأنها محفوظة من النسخة العاملة؛ يمكن تحسينها في تغييرات منفصلة مع اختبارات وبيان الترحيل.

## إضافات0.11

SettingsGate.cs يتحقق من مشتق كلمة مرور الإعدادات؛ SettingsUnlockDialog.cs يعرض نافذة مقنعة مع حد المحاولات. OperationStatus.cs يفصل الحالة المحلية عن إقرار الخادم. MainForm.Diagnostics.cs يأخذ عينة سجل كل ثانية ويحفظ قراءات وأحداث منفصلة؛ InterfaceTests.cs يتحقق من اللون والقفل والمواضع والسجل على Windows.

## مكونات الاعتمادية

- `Infrastructure/Serial/SerialSession.cs`: جلسة القراءة والاسترداد وصندوق أحدث عينة؛ واجهة ISerialConnection لاختبارات محلية.
- `Infrastructure/Logging/BackgroundLogWriter.cs`: طابور كتابة محدود وتفريغ السجل عند الإغلاق.
- `Domain/Weighing/ReadingDescription.cs`: تصنيف أسباب رفض الإطار.
- `tests/QabanAlGhad.Tests/RegressionTests.cs`: اختبارات فساد الإعدادات وحماية الرموز والتشخيص وإغلاق السجل وجلسة القراءة.
- `scripts/verify-package.ps1`: فحص محتويات الحزمة وبصماتها، و`scripts/sign.ps1` للتوقيع الاختياري بشهادة متاحة مسبقًا.
