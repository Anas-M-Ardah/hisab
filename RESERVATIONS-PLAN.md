# Stock reservations

Approved: quantity reservations with optional specific serial numbers, customer and optional accepted quote, location, expiry, release, and invoice fulfillment. Reservations affect available stock only.

- Active through expiry date; expired and released holds stop consuming availability immediately, with retained history.
- Full fulfillment into one invoice. Physical quantities and journals change only through the invoice. Failure rolls back both invoice and reservation. Reversals do not silently reactivate old promises.
- All stock-changing replay paths protect active quantities and fixed serials. Releasing a hold requires a reason. Quote edits/status changes require releasing its active hold.
- Inventory shows on hand / reserved / available. Replenishment subtracts reserved demand. Sales show availability while selecting items.
- Regression tests for overselling, serial protection, transfer/count/reversal paths, atomic fulfillment, expiry, quote links, permissions and backup. Arabic/English previews and packaged executable validation.

## Completed validation

- 281 automated checks pass, including 40 reservation checks, in the self-contained Windows x64 package.
- Arabic and English previews pass at 14 and 24-point text for create, list, details, release, standalone sale, quote sale and availability.
- An actual schema-5 database migrates and replays without changing stock, documents or ledger value; future schemas are rejected.
- Reviewed quantity protection, fixed serials, atomic failure rollback, quote links, permissions, expiry and portable restore.
- Full fulfillment into one invoice is supported; partial fulfillment remains outside this version.
- Local package validation covers x64. The release workflow additionally requires x86 tests and cross-builds ARM64.
