# حساب 0.8.0 — حجوزات المخزون

- حجز كميات لعميل مع موقع وتاريخ صلاحية، وأرقام أجهزة محددة اختياريًا.
- عرض المخزون الفعلي والمحجوز والمتاح، وإدخال الطلب المحجوز في اقتراحات إعادة الطلب.
- منع بيع أو نقل أو إرجاع مخزون يغطي حجزًا نشطًا؛ تحرير الحجز مع سبب موثق أو انتهاء صلاحيته تلقائيًا.
- إنشاء الحجز من عرض سعر مقبول، ثم تنفيذ البيع في فاتورة مرتبطة واحدة دون خصم المخزون مرتين.
- تحديث شاشات الحجز والعرض والبيع بالعربية والإنجليزية.

## English

Reserve stock quantities by customer and location, with an expiry date and optional fixed device serials. Inventory shows on hand, reserved and available quantities; replenishment accounts for held demand.

Active holds protect quantities and fixed serials across sales, transfers, supplier returns, reversals and stock-count adjustments. Reservations do not post accounting entries or change physical stock. Release with an audit reason or allow the hold to expire after its validity date.

Accepted quotations can reserve their stock items. Fulfillment creates one linked invoice atomically, preserving the hold if validation fails. This version supports full fulfillment; invoice reversal does not reactivate an old reservation.

## Upgrade and validation

Back up before upgrading to schema 6; older versions cannot reopen the database. See STOCK-RESERVATIONS.md for operation and expiry rules.

The release pipeline requires 281 automated checks on x64 and x86. ARM64 is cross-built. Arabic/English reservation workflows and availability screens are checked at 14/24-point text. Physical hardware acceptance testing remains necessary.
