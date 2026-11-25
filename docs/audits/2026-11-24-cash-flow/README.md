# Cash-flow reporting — 2026-11-24

Implemented locally; unreleased, schema 8 remains the current unpublished migration.

## Reporting contract

The first implementation uses the direct method from posted Cash-kind ledger accounts. This intentionally replaces the initial indirect-method proposal: current records identify actual receipts/payments reliably, while automatic indirect adjustments could misclassify noncash assets, borrowing and mixed payments. Noncash activity is excluded from this cash report; a separate noncash investing/financing disclosure schedule is not implemented.

- Current period and same dates one year earlier, with dated CSV columns.
- Gross operating, investing and financing receipts/payments; opening cash, closing cash, net change and reconciliation difference.
- All business movements require explicit classifications, with three-decimal nonnegative allocations. A payment can be split across categories. No automatic supplier/expense assumptions.
- Pure cash-account transfers are automatic. Transfer portions in mixed documents must balance within each document. Opposite errors in unrelated documents cannot cancel the incomplete-report flag.
- Unclassified receipts and payments are separately visible even when their net is zero. Details identify unbalanced transfers and stale allocations.
- Keys use document/account/direction, surviving entry-ID replacement during inventory replay. Changed cash amounts invalidate saved allocations. Reversals inherit original classifications unless explicitly overridden. Saved classifications are presentation-only, audit logged and restricted by the master-data permission.
- Cash scope comprises all accounts of kind Cash (including bank accounts configured that way). Pending cheques are excluded. Clearing is included on its posting date, and a later bounce reverses cash on the bounce date. No external integration or exchange-rate logic is introduced.
- Current classifications and account kinds apply to comparative periods. Classification edits do not post or rewrite journals, including closed periods; fixed report snapshots are not implemented.

Arabic/English report, detailed CSV/print, and classification dialog are available from Reports. The direct report is not complete for reliance until unclassified amounts and per-document transfer errors are resolved; reconciliation alone is insufficient.

## Review and remaining scope

Independent read-only source review found no actionable defects. Its suggested reversal override, same-account dual direction, and cross-document transfer-error tests were added. Validation uses only isolated Hisab.Audit synthetic databases. No live database, commit, push or release is part of this task.

Local bank reconciliation, noncash disclosures, fixed-asset/prepayment schedules and the complete linked-month acceptance exercise remain outstanding.

Full synthetic regression: 618 PASS, 0 FAIL, exit 0. Includes 28 cash-flow checks covering split allocations, gross direction, noncash exclusion, transfers, stale basis, prior periods, reversals/overrides, cheque clearing/bounce, persistence and viewer permissions. Source build passed with zero warnings/errors. Final toolbar-only UI verification is recorded separately.

Final toolbar build: zero warnings/errors. Final report harness: 32 PASS, exit 0, plus both classification dialogs rendered. Arabic large-font cash report, English print and classification layout were visually inspected. Final source hashes and representative previews are included. The final change after the full regression was toolbar layout only; financial calculations were unchanged.
