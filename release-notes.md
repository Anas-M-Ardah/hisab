# حساب 0.6.0 — إدارة المخزون والمشتريات

- باركود الأصناف وتتبع الأجهزة بالأرقام التسلسلية وسجل الحركة وتواريخ الضمان.
- مخزون رئيسي افتراضي مع مواقع إضافية اختيارية وتحويلات داخلية.
- جلسات جرد قابلة للاستكمال، ومنع الحركة للأصناف المشمولة، وتسويات موثقة للفروقات.
- أوامر شراء واستلام جزئي وفوترة الكميات المستلمة دون مضاعفة المخزون، مع إرجاع الكميات غير المفوترة.
- توزيع تكاليف الشحن والمصاريف الإضافية واقتراحات إعادة الطلب حسب الموقع.
- شاشات عربية وإنجليزية وتفاصيل المخزون في المستندات المطبوعة.

## الترقية

احتفظ بنسخة احتياطية قابلة للاستعادة قبل الترقية. ينتقل الملف إلى إصدار قاعدة البيانات 4، ولا تستطيع الإصدارات القديمة فتحه بعد ذلك. تنتقل الكميات الحالية إلى المخزون الرئيسي مع الحفاظ على المستندات والأرصدة.

فك ضغط Hisab-Windows.zip ثم شغّل Start-Hisab.cmd. تتضمن الحزمة إصدارات Windows x64 وx86 وARM64 وبيئة .NET، إضافة إلى دليل الاستخدام والمصدر. راجع INVENTORY-WORKFLOWS.md لمعرفة خطوات العمل الجديدة.

## English

Hisab 0.6.0 adds product barcodes, device serial tracking and warranty dates; optional stock locations and internal transfers; resumable stock counts with audited adjustments; purchase orders, partial receipts and supplier billing without duplicate inventory; landed-cost allocation; and replenishment suggestions by location. Arabic/English screens and printed inventory details are included.

Existing quantities migrate to Main stock. Keep a portable backup before upgrading: schema 4 databases cannot be reopened by older versions. Company-wide weighted-average costing is retained.

Extract Hisab-Windows.zip and run Start-Hisab.cmd. The package includes Windows x64, x86 and ARM64 builds, the .NET runtime, source code and workflow documentation.

## Validation

The release pipeline requires all 219 automated checks to pass on x64 and x86 before publishing. ARM64 is cross-built but not executed on the hosted x64 runner. Local validation also covered populated schema-3 migration and 10 bilingual UI preview checks. Physical barcode scanners and printers still require acceptance testing.

Includes fixes for legacy serial returns, partially billed receipt returns, receipt rounding, freight-allocation corrections and order limits when reversing returns.
