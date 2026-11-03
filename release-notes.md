# حساب 0.7.0 — عروض الأسعار وتحسين الواجهة

- عروض أسعار مستقلة مع العميل، الأصناف، الخصومات، الضريبة وتاريخ الصلاحية.
- حالات مسودة، مرسل، مقبول، مرفوض، منتهي الصلاحية ومحّول إلى فاتورة.
- طباعة العرض أو حفظه PDF وتحويله إلى فاتورة مبيعات بعد مراجعة الدفع والموقع والأرقام التسلسلية.
- العرض لا يحجز المخزون ولا يؤثر على الحسابات. يحتفظ النظام بمراجعات العرض ويمنع تحويله مرتين.
- بحث داخل القوائم المنسدلة مع رسائل عربية وإنجليزية للقوائم الفارغة والبحث دون نتائج.
- أخطاء واضحة داخل النماذج، وتنسيق تمرير ومسافات متسق.

## English

Quotations include customer details, priced lines, discounts, tax, validity dates and tracked status. Print or save to PDF, then convert once into a reviewed sales invoice with stock, serial and payment validation. Quotes do not reserve stock or post accounting entries. Saved revisions and conversion links are retained.

Dropdown search preserves original options and selection, with keyboard support and localized empty/no-match feedback. Form validation messages stay visible above the fields; shared scrolling and quotation layouts are refined for Arabic/English and different text sizes.

## Upgrade

Back up before upgrading. Schema 5 preserves existing stock and documents but cannot be reopened by 0.6.x. See QUOTATIONS.md for the workflow and conversion rules.

The release pipeline requires 241 accounting/storage checks on x64 and x86. ARM64 is cross-built; hardware scanners and printers still require acceptance testing.
