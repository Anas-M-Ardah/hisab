# Reporting completeness — 2026-11-25

Local, unreleased implementation. Schema 8 remains the unpublished migration.

## Delivered contract

- Noncash investing/financing disclosure register linked to original journal debit/credit accounts. Explicit categories for asset acquisition, debt-to-equity conversion and other noncash investing/financing. The company must identify and explain relevant transactions: this is not automatic completeness certification.
- Mixed cash/noncash journals can disclose the noncash portion; Cash-kind accounts cannot be allocated. Shared account capacities prevent double allocation across categories. Corrections require explanations and audit records. Reversals appear negatively on their own dates. Source amount changes flag stale disclosures. These records never post journals or add to cash flows.
- Saved report snapshots freeze the displayed table, headers, title, company, language, filter metadata and warning notes. Author and UTC save time are recorded. Database triggers prevent update/delete through ordinary SQL; SHA-256 verifies payload integrity. This is local integrity protection, not a cryptographic signature or protection from a database administrator rewriting the database.
- Save, print and CSV use the current visible row/column order, including sorting/reordering. Saved-snapshot viewing disables sorting/reordering to retain its frozen presentation. Captured reports do not recompute when source accounts, company names or balances change.
- Snapshot saving uses the master-data permission; viewers can read/print/export saved reports. Corrections create another snapshot. Backups contain both snapshots and disclosures; restoring an older backup restores that older snapshot collection.
- Report CSV now includes descriptive metadata before the table, including notes and filters. Consumers expecting a header on the first line must locate the report-table header after the metadata block. Formula-like text is escaped; negative JOD numbers remain numeric text.
- Arabic/English and 14/24 font preview checks compare numeric cells across languages, snapshot roundtrip, CSV and print-table content. Screenshots supplement those assertions.

## Independent review

Read-only review found a P2 mismatch between sorted screen rows and pre-sort snapshot/print/export data. The capture path now uses the displayed grid order and has a UI-harness assertion for sorted rows and reordered columns. No other high-confidence defects were reported in the reviewed scope.

## Limits

Snapshots preserve report content, not a full historical database or fixed printer/font geometry. Existing snapshots remain as saved even when a later report mapping or disclosure is corrected. Comprehensive bank reconciliation, managed schedules, company policy sign-off and the complete linked-month acceptance exercise remain separate roadmap work.

Only the isolated Hisab.Audit entry point with fresh synthetic databases is used for this validation. No production database, commit, push or release is part of this batch.

Verification: full synthetic suite 640 PASS, 0 FAIL, exit 0 (22 added reporting-completeness checks). Final accounting code was unchanged by subsequent displayed-order and dropdown-label UI fixes. Displayed-order fix received independent read-only confirmation. Final UI evidence is stored separately.

Final build: zero warnings/errors. Final UI harness: 37 PASS, exit 0, including sorted/reordered capture and Arabic/English numeric parity across snapshots, CSV and print tables. New disclosure and saved-snapshot dialogs rendered in both languages; representative large-font and print images were inspected. Evidence: full-suite.txt, ui-checks.txt, source-hashes.json and previews/.
