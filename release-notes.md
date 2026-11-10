# حساب 0.10.0 — استيراد Excel مع المعاينة

- استيراد الأصناف والأرصدة الافتتاحية الاختيارية من ملفات xlsx، مع اختيار الورقة ومطابقة الأعمدة.
- معاينة الجديد والمتخطى والأخطاء قبل الحفظ، مع تفاصيل الصفوف والبحث عن الأخطاء.
- حماية الأكواد والباركود النصية والأرقام التسلسلية، ومنع تكرار الرصيد الافتتاحي.
- حفظ الدفعة كاملة في معاملة واحدة، أو التراجع عنها كاملة عند الخطأ.
- يتضمن سجل الصنف الموحد، والفلاتر وروابط المستندات الأصلية.

## English

Import Excel workbooks from Items & stock. Choose a worksheet, match columns and review every product and optional opening balance before saving. The preview distinguishes new items, existing-item opening balances, skipped duplicates and validation errors.

Existing codes are skipped by default; an explicit opening-only option preserves existing item details. Serial numbers, barcodes, quantities, costs, stock-count conflicts and closed periods are checked. The entire reviewed batch commits atomically and is revalidated before saving.

Includes unified Item history, date/location/activity filters, serial search and links to source records.

## Compatibility and validation

Schema remains 6. Supports standard .xlsx files; see EXCEL-IMPORT.md for supported columns, formats and limits. Microsoft Open XML SDK licenses are included.

The release workflow requires 342 automated checks on x64 and x86 and cross-builds ARM64. Arabic/English import and history previews cover normal and large text sizes.
