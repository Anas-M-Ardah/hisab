# Opening balances and late supplier price differences

Date: 2026-11-23, Asia/Amman. Base checkout remains `a2899dd`; these changes build on the uncommitted audit-remediation work. Database schema advances from 7 to 8. Production databases were not opened or changed during this work.

## Delivered workflows

### Opening receivables and payables

More tools → Opening balances provides original invoice reference/date, original due date, outstanding amount, and system opening date. OC/OS documents participate in aging, positions, ordinary receipt/payment allocation, cheque allocation, and customer/supplier reconciliation. They create no sales, tax or stock movement.

Two explicit modes prevent duplicate opening entries:

- **New posting:** debit customer / credit opening equity, or debit opening equity / credit supplier.
- **Existing journal details:** link to a qualifying active JV containing only the selected party and opening equity. No new GL entries are posted. The total linked detail cannot exceed that journal's amount at any historical interval. An explicitly selected attach mode cannot silently become a new posting if its journal number is blank.

Preview includes separate customer/supplier totals for new postings and details-only records. Preview saves no documents, entries, audit rows or numbering changes. Saving revalidates the entire batch in one transaction. Duplicate references and shared-source over-allocation are rejected, including historical overlap after reversal. Linked journals cannot be reversed or amended before their detail reversal dates.

Excel import includes a downloadable `.xlsx` template, sheet selection, preview, and atomic posting. Columns are AccountCode, Reference, OriginalDate, DueDate, Amount and ExistingJournal. Identifiers and ISO dates are text to preserve leading zeros and avoid date-locale ambiguity; formula/error cells are rejected. Blank ExistingJournal means a new posting, explicitly explained in the import form and preview totals. The opening document printout includes original reference/date, source journal or posting mode, allocated payments, and outstanding balance.

Scope: positive outstanding customer/supplier invoices. Credit opening balances remain visible through the reconciliation report/manual journals; this is not a new general credit-note migration wizard. Existing journals must have the documented party/opening-equity structure.

### Supplier bills after receipt closing

Closed-receipt price differences can be recorded on an open invoice date. The user explicitly supplies the signed amount attributable to remaining inventory; the rest goes to current-period cost of sales. The UI calculates the line difference and explains the allocation. The bill printout records the total difference, inventory share and cost-of-sales share.

The service checks precision, sign, total allocation limits, positive remaining inventory for capitalization, and nonnegative resulting stock value. Fully consumed stock can use zero inventory share. The allocation is a user accounting judgment; the software does not certify that an arbitrary selected split is appropriate.

GRNI clears at recorded receipt basis; AP/tax use the invoice amount. Current-period adjustments are excluded from retrospective receipt repricing. Later sales use the adjusted weighted-average value. Reversing an open adjustment bill removes its adjustment and recalculates affected open-period costs. Closed bill reversal remains subject to the existing period guard.

Inventory replay now seeds closed history from its recorded stock movements and preserves its journal entries, movement records and return costs. Only open activity is recalculated. Tests cover closing an adjustment bill, a subsequent sale, repeated replay, reopening and replay. Open-receipt repricing retains its previous behavior; a mixed bill containing closed receipts and retrospective open-receipt price adjustments still requires separate invoices.

## Migration and verification

Schema 8 adds `opening_balances` and default-zero current-variance/inventory-adjustment columns on receipt links. Existing records keep their prior behavior. The schema guard prevents older executables from opening this database version.

- Full isolated suite: **556 PASS, zero FAIL, exit code 0**, including the original 390 checks and 166 targeted checks. See `full-suite.txt`.
- Migration fixture removes both new receipt-link columns and the opening table before setting schema 7, then verifies upgrade to 8. Opening links also survive reopen and backup/restore with valid foreign keys.
- New Excel template passes Open XML structural validation and reads without sample balances.
- Independent opening review identified and verified fixes for blank attach mode and historical source reuse/reversal. Late-billing review found no blockers; requested partial billing, frozen adjustment, reopen/replay and genuine old-table migration cases were added.
- Synthetic UI runs cover Arabic/English at fonts 14 and 24: reconciliation, opening list/form/preview/allocation/document, and late-bill form. Large-font opening tables were then adjusted to show full dates/references with wrapping and horizontal scrolling. Final UI evidence is in `ui-checks.txt` and `previews/`.
- The final source hashes identify the precise uncommitted files. All execution used the explicit `Hisab.Audit.Program` entrypoint and new temporary fixture directories, never normal app startup.

No commit, push or release was performed. Financial statements, broader month-end schedules, bank reconciliation, full business-month acceptance and professional policy sign-off remain roadmap work. These results do not establish complete market or statutory certification.
