# حساب 0.12.0 — سلامة الحسابات واكتمال التقارير

- إصلاح التخصيصات التاريخية وعكس الشيكات وحدود الإقفال وتقريب تكلفة المرتجعات.
- أرصدة افتتاحية تفصيلية للعملاء والموردين مع استيراد ومعاينة، دون تكرار أرصدة الأستاذ.
- معالجة فروق فواتير الاستلام بعد الإقفال بتوزيع صريح على المخزون وتكلفة المبيعات في الفترة الحالية.
- قائمة دخل مقارنة، مركز مالي، تدفقات نقدية، وتقييم مخزون وتسوية استلامات غير مفوترة حتى تاريخ.
- إفصاحات غير نقدية مرتبطة بالقيود، ونسخ تقارير ثابتة، وتطابق الترتيب والقيم بين الشاشة والطباعة والتصدير.

## English

This release strengthens accounting correctness and adds a coherent bilingual reporting set:

- Historical payment allocation, cheque reversal, closing-period and return-cost rounding fixes.
- Detailed customer/supplier opening balances, including Excel preview and attachment to existing opening journals without duplicate posting.
- Explicit current-period allocation of late receipt price differences while preserving closed-period costs.
- Comparative income statement, balance sheet, direct cash-flow statement, historical inventory valuation, and GRNI reconciliation.
- Cash-flow split classifications, noncash journal-linked disclosures, and immutable report-content snapshots with integrity checks.
- Arabic/English screen, snapshot, print and CSV value/order consistency. CSV reports now include metadata before the report-table header.

## Upgrade and scope

Schema 8. Back up before upgrading; older versions cannot open upgraded databases. Noncash disclosures and business cash-flow categories require explicit classification and review. Report snapshots preserve saved content; they are not full historical database snapshots or external digital signatures.

Local bank reconciliation, managed fixed-asset/accrual/prepayment schedules and full business-month acceptance remain roadmap work. No external accounting or tax-system integration is included.

## Validation

640 automated synthetic checks; 37 bilingual reporting/UI/export checks passed locally. The Windows release workflow runs the full 640-check suite against the packaged x64 and x86 executables before publishing. ARM64 is built but not executed natively. Physical printing and target-device DPI remain manual checks.
