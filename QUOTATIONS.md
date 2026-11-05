# Quotations and UI improvements

Open **Quotations / عروض الأسعار** in the sidebar. Create a quote with a classified customer, item descriptions, quantities, prices, discounts, tax settings and a validity date. Save it to obtain a QT number and print it or save to PDF through the Windows print dialog.

Quotes do not reserve devices automatically, reduce stock, post journals or create a receivable. You can quote goods that are not yet in stock. Record Sent, Accepted or Declined using More actions; Sent records your own status change and does not send email. Expired status is based on the validity date. Search by quote number or customer and filter by status.

Editing an unconverted quote creates a retained revision and returns it to Draft. Renew the validity date on an expired quote before converting it. Earlier saved revisions and status changes remain in quote_versions for audit evidence.

**Convert to invoice** opens a pre-filled sales invoice for review. Prices, quantities, discounts, tax and customer must match the quote; change the quote first if these need revision. Select the stock location and actual device serials using Edit selected line, then enter payment and due date. Normal stock and closed-period checks apply when posting. A failed conversion leaves both quote and stock unchanged.

Each quote links to one invoice. Selecting Convert again opens that invoice. Reversing an invoice preserves the quote's conversion link; create a new quote for a replacement sale. Converted quotes cannot be edited.

Dropdowns now offer a labeled search field without changing their original options or current selection. Search results can be chosen by mouse or with Down/Enter; Escape closes the menu. Empty lists and unmatched searches show localized messages. Forms retain errors above their fields, and scrolling/spacing use the shared visual style.

Accepted quotes can explicitly reserve their stock through **Reserve stock**. Conversion uses the active hold automatically; release it before changing the quote. See STOCK-RESERVATIONS.md for expiry, serial selection and fulfillment rules.

## Compatibility

Version 0.8.0 uses schema 6. Back up before opening an existing database; older versions cannot reopen an upgraded database. Current inventory quantities, existing invoices and balances are preserved. Quote printing preserves customer/company names and terms captured on save; Arabic and English company/terms text follows the selected interface language.
