# Inventory-first workflows

This update adds inventory management and purchasing to the existing Arabic/English Windows app. Start from **Inventory management** or **Purchasing** in the navigation.

## Locations and devices

Existing stock belongs to Main stock. A location selector appears only when multiple active locations exist. Add locations with Locations; Transfer stock moves quantities and selected serials immediately without changing total company valuation. Transfers are not shipments in transit.

Configure tracking sets an optional product barcode and enables serial tracking. For existing stock, bring units to Main stock and register exactly one serial per whole unit. Tracking cannot subsequently be disabled. Each item has its own serial-number namespace; barcodes are unique across products. Scanners must work as keyboard input. Enter product barcodes followed by Enter; enter serials one per line. Serialized receipts, sales, returns and opening stock require actual unit identities.

Devices & warranties shows serial history and optional supplier/customer warranty end dates. This is inventory information, not a repair management system. Printed inventory documents include serial details.

## Reservations

Inventory shows on-hand, reserved and available quantities by location. Open Reservations to hold stock for a customer or accepted quote, optionally including specific serials. Holds expire or can be released with a reason; sell through the hold to consume it. See STOCK-RESERVATIONS.md for the complete workflow.

## Stock counts

Start a count for selected items at one location. Movements for those items/location are blocked until completion or cancellation. Save progress to resume later; locks survive restart. Enter zero explicitly for absent items. Serialized items require the physical serial list. Review expected and actual quantities, enter a discrepancy reason, and review a positive cost for gains before posting. The adjustment posts inventory differences and preserves the count. Later same-day movements are allowed; earlier-dated changes affecting a completed count require reversing that count first.

## Purchasing

Create and issue a purchase order before receiving. Details & receive supports partial deliveries and serial entry. Amend ordered quantity explicitly before overreceiving. Cancel outstanding leaves previously received stock intact.

Goods receipts post inventory against account 2195 (goods received not invoiced). Bill receipts can combine receipts from the same supplier and bill only remaining quantities. The invoice records the supplier payable, tax and price differences without receiving stock again. Pay and allocate using the existing payment workflow. Direct purchase invoices remain available for simultaneous delivery/invoicing. Invoice-first purchasing is not supported.

Return unbilled goods through the receipt; return billed goods through the linked supplier invoice. A partially billed receipt still permits returns up to its unbilled remainder. Reverse linked returns, invoices and landed-cost allocations before cancelling their receipt. Linked supplier invoices are corrected by reversing and rebilling, not by using the generic invoice editor.

## Landed costs

Select stock receipt lines and allocate a charge by receipt value, quantity, or manual amounts. Preview allocations before posting. Amounts must sum exactly, using JOD precision. Recoverable tax is not part of the entered capitalizable cost. Charges can be a payable or an immediate cash/bank payment. To reuse a previously posted payment/journal, it must debit account 2196 (acquisition cost clearing) for the exact amount and must not have an active allocation. Reverse an incorrect allocation before reallocating that same charge; its history remains available. This prevents duplicate recognition of that charge.

Receipt cost changes replay company-wide weighted-average valuation, including later sales. Late charges or invoice changes affecting closed periods are blocked; an administrator must explicitly reopen affected periods before correction.

## Replenishment

Set minimum and target quantities by item/location. Suggestions use on-hand minus active reservations plus outstanding issued orders. Draft and cancelled orders are excluded. Expected arrival and overdue status remain visible. Create draft order pre-fills the selected item, location and suggested quantity; a person selects the supplier, cost and confirms the order.

## Compatibility

Schema 4 introduced locations, serial identities, counts and procurement records; version 0.7.0 introduced schema 5 for quotations, and version 0.8.0 uses schema 6 for stock reservations. Older app versions reject the upgraded database. Keep a portable backup before upgrading. Existing documents and accounting balances are preserved. This change retains local single-process operation and company-wide weighted-average costing; it does not add branch accounting or multi-computer synchronization.

## Validation commands

- `dotnet build source/Hisab.csproj -c Release`
- `dotnet source/bin/Release/net10.0-windows/Hisab.dll --self-test <isolated-directory>`
- `dotnet source/bin/Release/net10.0-windows/Hisab.dll --inventory-test <isolated-directory>`
- `dotnet source/bin/Release/net10.0-windows/Hisab.dll --preview --preview-demo --preview-inventory <isolated-directory>`

Use isolated directories for all tests and previews. Physical scanners, printers, and other Windows hardware still require acceptance testing.
