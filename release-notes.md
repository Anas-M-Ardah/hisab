# حساب 0.5.0 — الحسابات والسندات والتقارير المترابطة

- تصفية الحسابات حسب الحساب الرئيسي أثناء تسجيل السندات والقيود والفواتير.
- سند قبض أو صرف موزع على عدة حسابات، مع مبلغ وبيان لكل سطر، وتعديل السند بقيد عكسي موثق.
- حفظ بيان كل سطر في القيود وكشف الحساب والمستند المطبوع.
- ميزان للحسابات الرئيسية وآخر للفروع، مع حركات الفترة والأرصدة حتى نهايتها وفتح كشف الحساب.
- سند قبض مطبوع يجمع الشيكات وتواريخ استحقاقها، مع تحصيل أو إرجاع كل شيك مستقلًا.
- تحويل تكاليف الطلبية من حساب أصول فرعي إلى أصناف مخزنية بكمياتها وتكاليفها.
- إبقاء إجمالي السند وزر الحفظ ظاهرين وإصلاح ارتفاع عناوين الجداول عند تكبير الخط.
- ترقية قاعدة البيانات إلى الإصدار 3 مع منع فتحها بإصدارات أقدم.

## English

Account hierarchy now carries through entry and reports. Split receipts/payments support a description and amount per account, with reversible amendments. Journal line descriptions appear in statements and printouts. Main-account and child-account trial balances show period movements and closing balances with drill-down actions.

Cheque batches have a combined printable receipt while settlements remain independent. Purchase-order costs transfer from a leaf asset account into received stock quantities and values, with balance checks and inventory-safe reversals. Table headings grow with their text, and workflow forms keep totals and save actions visible.

Database schema 3 protects upgraded data from older app versions. The release pipeline requires 171 accounting/storage checks on x64 and x86 before publishing.
