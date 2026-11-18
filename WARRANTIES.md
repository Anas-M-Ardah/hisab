# الضمان والاستبدال / Warranties & exchanges

## الاستخدام

1. افتح **الضمان والاستبدال** ثم **تسجيل / استلام ضمان**. ابحث بالتسلسلي أو العميل أو الفاتورة، واختر الجهاز المباع.
2. أدخل آخر يوم للضمان وشروطه، واحفظ أو اطبع شهادة الضمان. التاريخ الفارغ يعني أن مدة الضمان غير مسجلة. يظهر تاريخ العميل القديم كمرجع أولي فقط؛ سجل الضمان المرتبط بالفاتورة هو المعتمد لهذه المطالبات.
3. عند الاستلام، سجّل العطل والحالة الظاهرة والملحقات والتاريخ. اطبع إيصال المطالبة للعميل. لا تضاف الأجهزة التالفة إلى المخزون القابل للبيع.
4. يمكن إرسال الجهاز إلى المورد برقم متابعة، ثم استلامه. هذه متابعة عهدة محلية، بدون تكامل خارجي أو قيد دائن من المورد.
5. للاستبدال، يجب أن يكون الجهاز الأصلي في المتجر. اختر صنفًا بديلًا وموقعًا وتسلسليًا متاحًا ثم أكد الاستبدال المجاني. لا توجد فروقات أسعار أو رسوم على العميل في هذا الإصدار.
6. يغلق الجهاز الذي أعيد للعميل دون استبدال بنتيجة فحص. بعد الاستبدال، يبقى الجهاز التالف في العهدة ويمكن إرساله للمورد أو تسجيل إتلافه مع السبب.

## Business rules

- Version 1 covers serialized hardware sold through a posted sales invoice. Register each device separately. Nonserialized accessories continue to use the existing sales-return workflow.
- Coverage is evaluated on the intake date, inclusive of the last covered day. Terms and expiry lock when the first claim is received. An expired or unrecorded warranty requires an administrator's explicit exception reason for a free replacement.
- A replacement inherits the original expiry and terms, including any expired/unrecorded state. It can have a subsequent claim linked back to the original sale. A goodwill exception does not silently extend coverage.
- Replacement document `WX` removes one available serial at company weighted-average cost, debits warranty expense (5196), and credits inventory (1301). Backdated stock costs recalculate the replacement cost. Customer balances, invoice revenue and tax are unchanged.
- Faulty devices are tracked in claim custody, outside sellable stock and inventory valuation. Stock counts, purchases and customer returns cannot add an open-claim or already-replaced serial back to stock. Refurbishment/reintroduction and supplier reimbursements are outside this version.
- Existing stock reservations, location availability, stock-count locks and closed accounting periods still apply.
- A claim with its original device in store can be closed by recording its return to the customer and the inspection outcome. A replaced device stays in custody until supplier handling or disposal is recorded.
- **Undo replacement** is for a physical exchange correction: both devices must be back in store. It restores the replacement to available stock, reverses the expense and reopens the claim. It is blocked if the replacement has subsequent claim history. Generic document reversal cannot bypass this workflow.
- Users with posting permissions can handle claims. Registration needs master-data permission; only administrators can authorize uncovered replacements. Viewers cannot change records.
- Use the list search for customer, original/replacement serial, invoice, claim ID or supplier reference. Turn off the active/custody filter to see closed claims and disposed devices. The claim detail includes its event history, original invoice and a printable receipt.

## Upgrade

Database schema is **7**. Back up before upgrading. Older Hisab versions cannot open upgraded data. All warranty records and custody history are included in local encrypted/portable backups. There are no external-system integrations.
