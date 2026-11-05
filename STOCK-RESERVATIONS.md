# Stock reservations

Open **Reservations / حجوزات المخزون**, or choose Reservations from Inventory management.

## Create a hold

Choose a customer and a validity date. Add stock items, their locations and the quantities to hold. For serialized items, optionally enter some or all specific device serials, one per line. Remaining serials can be chosen at sale. Services and quantities beyond available stock cannot be reserved.

A reservation is active through the end of its validity date, according to the computer's date. It then appears as Expired and stops reducing availability automatically, even if the app was closed. Use Refresh to update an already-open list. Expired records remain in history.

Inventory shows **On hand / Reserved / Available**. Reservations do not change physical stock, inventory valuation, journals or customer balances. Availability equals on-hand stock minus active holds. Replenishment suggestions subtract reserved demand before including open purchase orders.

## Sell held stock

Choose **Sell reserved stock** from a reservation. The sales invoice opens with the customer, quantities, locations and any fixed serials filled in. Enter prices for a standalone hold, review payment and due date, and use Edit selected line to supply remaining device serials.

Fulfillment must include every held quantity at its reserved location, including each specifically held device. Extra unreserved items may be added to standalone reservation sales. The invoice is posted once and linked to the reservation in one transaction; a validation failure leaves the hold and stock unchanged.

This version supports full fulfillment into one invoice. For a partial collection, release the original hold and create separate holds for the quantities being collected and retained before selling.

Reversing a fulfillment invoice restores stock according to the normal reversal rules, but does not reactivate an old hold. Create a new reservation if the customer still needs one.

## Accepted quotes

Mark a valid quotation Accepted, then choose **Reserve stock**. All stock quantities are prefilled. Choose one location per quoted item and optionally its serials. The reservation must cover exactly the quote's stock quantities and cannot outlast the quote. Non-stock service lines remain on the quotation but are not reserved.

Converting the quote automatically uses its active hold. Quote edits and status changes require releasing the hold first, preventing a reservation from silently referring to different quantities or a different customer.

## Release and protection

Choose Release reservation and enter a reason when a customer cancels or the held stock is no longer available. Release preserves the record and audit trail without posting a stock movement.

Other sales, transfers, supplier returns, receipt reversals and stock-count adjustments cannot consume active held quantities or specifically held devices. Resolve or release a conflicting hold first. This also applies when negative stock is otherwise allowed. Complete an open stock count before creating a new hold for its items/location.

## Upgrade

Version 0.8.0 uses schema 6. Keep a portable backup before upgrading; older versions cannot open the upgraded database. Existing quotes, stock and invoices are preserved. Reservations and invoice links are included in backups.
