HISAB / حساب — Windows desktop accounting, version 0.3

START
Extract the complete ZIP. Double-click Start-Hisab.cmd; it selects x64, ARM64 or x86 automatically. On ordinary 64-bit Intel/AMD PCs, app/Hisab.exe also runs directly. The app includes its .NET runtime and SQLite; no database server or Python installation is required. Target: supported Windows 10/11 configurations. ARM64 is built but has not been run on ARM hardware.
First launch asks only for an optional business name and large text size. Personal use requires no username or password. Optional unlock code: 4+ characters. Shared named accounts are optional and use 8+ character passwords. Portable backup passwords still require 10+ characters because they protect a transferable encrypted file. Arabic is the default; English is available. The refreshed interface uses a short navigation menu, four everyday actions, modern controls, large text and visible keyboard focus. See Setup-Guide-Arabic.pdf for an illustrated three-page setup sheet.
Your original 75 account names and 17 item names are imported. Ambiguous accounts require classification. There are no demonstration transactions in normal use.

FIRST-TIME SETUP
Accounts: classify customers and suppliers. The bilingual editor accepts Arabic/English names and addresses; the original name is used when a translation is empty.
Items: set prices and mark services. Enter opening stock before stock movements for that item. Opening balances use a balanced journal against account 3201; inventory account 1301 is controlled by invoices/opening stock/cost adjustments.
Settings: enter Arabic and English company/payment details, tax rate, due days, logo and seal. The editable 16% default came from your document; it is not a determination of the tax rate applicable to every item.
Optional shared users: create a named Admin under More tools > Shared users before enabling shared sign-in. Admin controls users, closing and restore; Accountant posts and edits master data; Viewer reads and prints. Sign out switches users. Five failed logins lock an account temporarily.

WHAT WAS ADDED
- Cheque register: incoming/outgoing, due dates, clearing, bouncing and cancellation; accounting entries and allocation reversals follow each transition.
- Attachments: files up to 20 MB per file, stored inside the encrypted database; select a document to add or export them.
- Financial periods: close income/expenses to retained earnings and lock posting on/before the last closing date. Admin can reopen with a reason.
- Aging: receivables and payables as at the chosen end date, with not-due, 1–30, 31–60, 61–90 and over-90-day buckets and credits.
- Sales and purchase returns: quantities reference original invoice lines; over-returns are rejected and stock, tax and balances update.
- Receipts/payments: optional invoice allocation during entry, or allocate across invoices later. Historical invoice balances use allocation dates. Printed invoices show paid at issue and paid/returned/outstanding through today.
- Stock: backdated purchases/sales replay the stock timeline and moving weighted-average costs. Negative stock is an explicit administrator option, with provisional cost reconciled on receipt. Default remains to block insufficient stock. Audited cost adjustments are available for positive stock.
- Amend documents: one correction action preserves original/reversal/replacement and reason; allocations transfer within available balances. Invoice, receipt, payment and journal corrections are supported. Related live returns must be cancelled before amending their source invoice. Closed periods must be reopened before correction. Cheques are corrected through their register.
- Historical tax worksheet: invoice/return/reversal events in the selected effective-date range, output/input bases and tax, totals and net tax. Export CSV or print/PDF. Later-dated cancellations do not remove earlier tax events. Backdated corrections restate their effective periods; closing locks those periods.
- Printing: bilingual customer addresses, descriptions, company details, logos and seals are supported. Billing details/images are saved with the invoice. Translations are editable; there is no automatic translation of business names or free text.
- Encrypted local storage, encrypted portable backups, architecture-selecting launcher, printer/machine diagnostics and certificate-signing script.

EVERYDAY USE
Use invoices for sales/purchases, receipts for collections and payments for money paid. Do not post the same invoice again as a manual journal. Invoice prices support before-tax and tax-inclusive modes; line discounts apply before tax. Amounts and quantities support three decimals.
Invoices screen: open/print, amend, return, allocate, attach files or enter bilingual descriptions. Cancel preserves the original and posts a reversing entry. F1 help, Ctrl+N sales invoice, Ctrl+B backup, Esc close dialog.
Reports include trial balance, ledger, daybook, current stock, stock movements, profit/loss, aging and the Jordan sales-tax worksheet. Current stock ignores the range; aging uses its end date; trial balance includes all entries through its end date. Profit/loss excludes closing transfers.
Print/PDF opens the Windows printer dialog. Settings > Machine & printer check can list printers, export diagnostics and print a bilingual test page. No print job is sent until you choose a printer and confirm.

BACKUP, TRANSFER AND UPDATE
Local data: %LOCALAPPDATA%\Hisab\accounting.hdb. Its random encryption key is protected by Windows DPAPI for the current Windows user, in accounting.hdb.key. Keep both for same-profile recovery. Automatic daily local backups use the same protected key.
Use Backups > Save backup for a password-protected .hisab file on USB/another disk. This portable backup works under another Windows user's encryption key. Keep its password separately; forgetting it prevents recovery.
On another PC: extract the app, complete the simple welcome screen, Restore backup, enter the backup password, then use the opening mode saved in the restored backup (direct, unlock code or shared sign-in). A local safety backup is made before replacement. Do not move a live database onto a shared network folder.
Updating executable files does not overwrite user data. Existing v0.1 accounting.db and app-owned .db backups are migrated to encrypted files on startup. Previously exported copies outside the app's data folder are not changed. Encryption does not erase copies or old disk remnants.
SQLite operates in memory; encrypted snapshots are written atomically at commit. Disk-write failure rolls back the in-memory transaction. Large databases/attachments therefore require corresponding RAM and snapshot-write time.

EXTERNAL ITEMS STILL REQUIRED
Actual trusted code signing requires your code-signing certificate; source/sign-release.ps1 signs and verifies with SHA-256 using Windows SDK SignTool. The delivered executables are unsigned.
Physical printing requires a real printer test. This PC exposes Microsoft Print to PDF and OneNote only. Testing on a separate PC and native ARM hardware remains outstanding; machine diagnostics and the test page are included.
Exact pixel matching requires the original invoice image/PDF. The supplied document contains textual invoice fields, so this template reproduces those fields with branding rather than claiming an exact visual copy.
The Jordan export is an accounting worksheet for review and transfer to the applicable declaration, not an ISTD-filed return or automatic electronic-invoicing integration. It does not determine input-tax deductibility or special/exempt/import classifications. Official declarations: https://istd.gov.jo/En/List/Tax_Returns

SOURCE AND VERIFICATION
C# / .NET 10 / WPF; SQLite plus AES-GCM encrypted snapshots. Source.zip contains build/signing scripts and tests. source/build.ps1 accepts -Runtime win-x64, win-arm64 or win-x86. See ARCHITECTURE.txt, UI-DESIGN.txt and VALIDATION.txt.
