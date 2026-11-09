# حساب 0.9.0 — سجل الصنف

- شاشة موحدة لحركات الصنف والفواتير والمرتجعات والتحويلات والجرد والحجوزات.
- بحث بالمستند أو الرقم التسلسلي أو الملاحظات، مع فلاتر التاريخ والموقع ونوع النشاط.
- فتح المستند أو الحجز الأصلي من السجل، وعرض تفاصيل العميل أو المورد والأجهزة.
- تمييز الحركات الملغاة وعدم احتساب الفوترة اللاحقة للاستلام كمخزون وارد جديد.

## English

Open Item history from Items & stock or Inventory management. Review purchases, sales, returns, transfers, stock counts, serial movements, cost activity and reservations together. Search by reference, serial or notes; filter by dates, location and activity type; open the source record from any row.

Current on-hand, reserved and available quantities remain visible. Cancelled documents and reversals are labeled, and billing previously received goods does not double-count incoming stock. Reservations show creation date and current status without changing physical quantities.

## Compatibility and validation

Schema remains 6. This is a read-only history view; see ITEM-HISTORY.md for details. The release workflow requires 302 automated checks on x64 and x86 and cross-builds ARM64. Arabic/English history, detail, empty-state and search previews cover 14/24-point text.
