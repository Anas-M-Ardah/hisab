# حساب 0.6.1 — تحسين القوائم المنسدلة

- تحسين تنسيق محرر الحسابات: شريط تمرير أنحف، مسافات أوضح، وسهم أوضح للقائمة المنسدلة.
- عرض «لا توجد بيانات» في القوائم المنسدلة الفارغة، داخل الحقل وعند فتح القائمة.
- تختفي الرسالة تلقائيًا عند توفر الخيارات، ولا يمكن اختيارها كقيمة.
- دعم العربية والإنجليزية وأحجام النص المختلفة.

## English

- Refine the account editor with a slimmer scrollbar, more space around fields and a clearer dropdown arrow.
- Show “No data available” in empty dropdown fields and their open menus.
- Update the empty message automatically when options are added or removed; the message is never a selectable item.
- Verify Arabic/English layouts, account editing interactions and different text sizes.

## Download and upgrade

Extract Hisab-Windows.zip and run Start-Hisab.cmd. Includes Windows x64, x86 and ARM64 builds with the .NET runtime, source code and guides.

This is a UI update with no database schema change from 0.6.0. When upgrading from older versions, keep a portable backup: schema 4 cannot be reopened by pre-0.6.0 versions.

The release pipeline requires all 219 automated checks to pass on both x64 and x86. ARM64 is cross-built but not executed on the hosted x64 runner. Bilingual UI checks also verify empty-menu messages, data changes and account-editor layouts.
