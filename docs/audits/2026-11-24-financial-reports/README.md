# Financial reporting implementation — 2026-11-24

Implemented locally, unreleased:

- Comparative income statement: revenue, cost of sales, gross/operating/net profit, other income/expense and income-tax expense. Closing transfers are excluded.
- Balance sheet with prior-year comparison, unclosed earnings, customer credits and supplier advances shown on the appropriate side, and an explicit equation difference.
- Presentation-only account mappings. Unmapped balances remain visible and warn that classification is incomplete; saved mappings apply to both comparison periods.
- Historical inventory by item/location, reconciled to inventory GL 1301. Company-wide weighted-average value is allocated across locations. At zero company quantity, opposing warehouse quantities remain visible with no allocated value; residual value is shown separately for review.
- Unbilled receipt reconciliation to GRNI 2195, including partial bills, returns and effective reversal dates. Values use original receipt basis, excluding invoice tax and price variance.
- Arabic/English report labels, dated comparative CSV columns, account-ledger navigation, and wrapped large-font report text.

Validation uses Hisab.Audit with fresh synthetic databases only. No live database was opened for this batch. Independent source review found and prompted correction of the zero-company-quantity omission; follow-up review reported no blocking findings. Residual-value-with-zero-quantity is implemented but lacks a dedicated nonzero-residual fixture.

This is partial milestone 3. Cash-flow reporting, local bank reconciliation, fixed-asset/prepayment schedules, and the complete linked-month acceptance exercise remain outstanding. No release, commit or push has been performed.

Final verification: build passed with zero warnings/errors; full synthetic suite 590 PASS, 0 FAIL, exit 0; report harness 24 PASS, exit 0. Representative Arabic/English large-font and printed previews were visually inspected. See full-suite.txt, ui-checks.txt, previews/ and source-hashes.json.
