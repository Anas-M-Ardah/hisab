# Quotes and consistent UI

Approved scope: UI polish and quotations, within inventory/sales management.

- Shared searchable dropdown popup, localized empty/no-match feedback, consistent form scrolling and persistent inline error messages. Preserve keyboard controls and existing selection values.
- Quotes stored separately from posted documents: customer, date, expiry, priced lines, tax, terms, status and audit history. No inventory reservation or accounting effects.
- Draft / Sent / Accepted / Declined / Converted status; expired status derived from date. Editing a nonconverted quote returns it to Draft. Conversion requires a valid nonexpired quote and opens a reviewed sales invoice with payment/location/serial input. One invoice per quote, atomic rollback on failure, retain original quote after invoice reversal.
- Customer-facing printable quotation / PDF via existing print flow.
- Service regression tests, bilingual UI previews, full automated suite. No release until implementation and checks are complete.

## Completed validation

- Release build and self-contained x64 publish succeeded.
- Full service suite: 241 passed, zero failed; packaged executable repeated all 241 checks successfully.
- Arabic/English quotation lists, forms, conversion, print layouts, inline validation and searchable dropdowns checked at 14/24-point text.
- Dropdown search tested for preserved source/selection, no-match feedback, keyboard selection and data removal.
- Existing account-editor interactions and layouts checked with the packaged executable at 14/20/24-point text.
- A populated schema-4 backup migrated without changing stock, documents or ledger values; future schemas remained blocked.
- Changes retained on codex/quotes-ui-polish; no remote publication performed.
