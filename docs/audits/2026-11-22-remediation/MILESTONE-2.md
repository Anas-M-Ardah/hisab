# Milestone 2 implementation batch

Base: a2899dd / schema 7, on top of the local milestone 1 changes. No schema migration, production-data edit, commit, push or release is part of this batch.

## Behavior delivered

- **INV-01:** chronological replay accumulates the quantities and costs already returned against each sales line. The last open return receives the remaining original cost. Closed return costs are retained. Tests include replay, cancellation of the final and an earlier return, and returns spanning a close. This does not retroactively repair a fully returned transaction already closed with an old rounding residual.
- **ACC-03:** the first close must include all earlier document activity. Each later close must start the day after the previous close. The existing profit-transfer model remains; no new fiscal-calendar or separate administrative-lock module is introduced. The form suggests the first activity date/next open date and an end date of yesterday. Closing with no eligible past date still fails validation.
- **INV-02, bounded scope:** an open-period invoice can settle a closed receipt at its recorded value, including partial quantities, current invoice tax, and already-sold stock. It posts GRNI/AP/tax without replaying the closed inventory entries. Cancelling that still-open invoice also avoids stock replay. If the original unit price matches, closed-receipt partial invoices use the remaining allocated receipt basis; the final fraction takes the exact remainder. A zero final remainder settles quantity without a zero journal. Open-period independent invoice rounding retains its previous behavior.
- Actual price differences against closed receipts remain rejected. Mixing a closed receipt with an open receipt's price adjustment in one bill is rejected atomically; these must be handled separately. A future current-period variance workflow still needs policy and implementation.
- **CAP-01, visibility first:** the new Customer / supplier reconciliation report exposes ledger balance, invoice outstanding, and the amount outside invoice aging at a specified cutoff. Manual opening balances, unallocated payments and other discrepancies are visible instead of disappearing from the user's view. Positive customer balances are receivable; positive supplier balances are payable. The residual is explicitly for review, not a claim that every difference is explained. It has no invented due date and posts nothing. Detailed opening-document/import/allocation workflows remain pending.
- **AR-01:** numeric input accepts valid grouped Western, Arabic and Persian numerals, including signs, while rejecting malformed grouping. Formatting and parsing round-trip; existing amount precision checks remain with transaction entry points.

## Verification and review

- `milestone-two-red.txt`: prior to these production edits, independent assertions reproduced return-cost loss, the closing gap, rejection of equal-cost closed-receipt billing, and grouped-input rejection.
- `milestone-two-full-first.txt`: retained failed full-suite run. Existing open-period fractional billing caught an overly broad rounding change. The fix was narrowed to closed receipts rather than changing the existing expected behavior.
- Final full-suite output is retained separately as `milestone-two-full-final.txt`: **493 PASS, zero FAIL, exit code 0**. It includes the 390 original checks and 103 accounting/closing/stock assertions. `milestone-two-source-hashes.json` identifies the final implementation/test files.
- The isolated build completed with zero warnings/errors. Build/run commands are in [the runner guide](../../../tests/Hisab.Audit/README.md).
- Independent read-only review found the same-price fractional edge and requested more closed-return/mixed-bill/reclose coverage. These were implemented. Subsequent review found no blocking issue, including the narrowed rounding rule and zero-value settlement. The reviewer did not execute tests.
- `ui-checks.txt` records Arabic/English rendering at 14/24 font sizes. Screenshots and print pages are retained in `report-previews/`. Visual inspection covered the Arabic 24 and English 14 screens and Arabic/English print pages: amounts and explanations were legible; the large-font table scrolls vertically. Physical printing was not performed. Existing English company-name fallback is unchanged.

## Remaining milestone work

Detailed opening invoices/imports and allocation support, price-difference settlement after close, the policy for later landed costs, and separate fiscal-year/administrative locking remain unresolved work. Reports currently disclose non-invoice balances but do not assign them aging buckets. This batch must not be described as completing the entire accounting roadmap or the planned 30-scenario business-month acceptance exercise.
