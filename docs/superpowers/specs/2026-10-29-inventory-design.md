# Hisab: inventory-first design

Date: 2026-10-29
Status: approved in conversation; implemented in version 0.6.0.

## Intent and agreed scope

Hisab is an inventory and sales management app for a small hardware seller. Strengthen purchasing and inventory before extending sales workflows. The company currently holds stock in one location but wants optional multiple locations.

The agreed direction is inventory first: serial numbers and barcodes, stocktaking, purchase orders with partial receiving, landed costs, and replenishment. Support a default Main stock location, per-location quantities and serials, transfers, location-specific receiving/counts/reorder rules, and combined visibility. Hide unnecessary location choices when only one location is active. Retain company-wide weighted-average costing.

Success means staff can identify available devices, reconcile physical stock, see incoming purchases, and make informed reorder decisions without duplicate inventory or accounting entries.

## Approaches considered

1. Inventory first — chosen by the user. Establish reliable quantities, device identities, and costs before expanding sales.
2. Sales first — improve quotations, reservations, pricing, and collections sooner, leaving inventory gaps unresolved.
3. One complete purchase-to-sale workflow — deliver a connected vertical slice, but change more business areas simultaneously.

## Constraints and exclusions

Build on the existing Windows C#/.NET/WPF application, Arabic and English support, encrypted local storage, stock replay, accounting transactions, permissions, returns, and audit history. Repository review was static; runtime behavior must be verified during implementation.

Exclude repair tickets, supplier service cases, branch accounting, separate companies, network synchronization, and a cloud rewrite. Sales orders, stock reservations, advanced prices, flexible tendering, and expanded sales analytics remain later phases. Do not make the current one-location workflow require location selection. Do not import illustrative transactions into live accounts.

The detailed rules below are proposed design decisions, not additional requirements stated by the user.

## 1. Locations and inventory foundation

Create Main stock and assign existing inventory and historical location-less movements to it during migration. Keep global quantity and value totals reconciled with the existing inventory ledger. Add locations without adding separate companies or cost pools.

Internal transfers record source, destination, date, items, quantities, serials where applicable, and operator. They are immediate, atomic transfers between company locations, with no revenue or change in company inventory value. In-transit shipments are outside this phase. Prevent transfers exceeding available source stock. Deactivate a location only when its stock and open operations have been resolved; preserve its history.

All new stock-affecting operations enforce existing period locks, permission checks, reversal rules, and audit recording. Backdated movements must leave historical location quantities and serial ownership valid, not just current totals. Corrections preserve original evidence.

## 2. Serials and barcodes

Each item can be quantity-only or serial-tracked. A product barcode identifies an item; a serial identifies a physical unit. Start with keyboard-emulating scanners and manual entry. Camera scanning and custom label printing are outside this phase.

For serial-tracked items, require whole-unit quantities and one serial per unit received, sold, returned, transferred, or adjusted. Require item barcode uniqueness and serial uniqueness within each item, allowing different manufacturers/items to share serial text. Preserve entered text; implement consistent normalization and lookup. Track each unit's movement history and links to supplier, purchase, customer, sale, and return. Returned units reuse their identity rather than creating duplicates.

Add optional supplier and customer warranty dates as metadata only; no repair workflow or automatic warranty adjudication. Prevent selling or transferring a serial not available at the selected location. Serialized stock cannot become negative even if the legacy optional negative-stock setting is enabled for quantity-only items.

Migration does not invent serials. Existing items remain quantity-only until a user performs a controlled registration of current physical units. Registration must reconcile serial count to current quantity and preserve historical documents. Before enabling tracking, resolve negative/fractional stock and discrepancies. Historical returns without a known serial require manual identification and an audited exception. Once serial movements exist, do not casually toggle tracking off.

## 3. Stocktaking

Start a count for a location and selected items or all items. Record expected quantities and serials, accept scanned/manual counts, identify duplicates, and show missing/extra units. A draft count does not change stock.

Proposed first-version concurrency rule: lock stock movements for the counted items at that location while the count is active. Other stock remains usable. This avoids reconciling counts against moving balances. Provide explicit cancellation to release the lock; resume open sessions after restart. Prevent competing overlapping sessions and block backdated movements affecting a finalized count unless the count is corrected through the established reversal process.

An authorized user reviews differences and reasons before posting one atomic adjustment. Quantity losses use existing costing. Gains require a defensible proposed cost, visibly reviewed by the user; use existing average cost when available, otherwise require a cost entry. Post corresponding inventory gain/loss entries using configured adjustment accounts. Exact account mappings are builder decisions consistent with the existing chart. Preserve expected count, actual count, reasons, and adjustment references.

## 4. Purchase orders and goods receipts

Purchase orders record supplier, location, items, ordered quantities, agreed prices, expected delivery, and notes. Draft/issued/partially received/received/cancelled states are derived consistently from quantities and explicit cancellation. Orders do not post stock or accounting entries. Cancellation releases only unreceived quantities; receiving above ordered quantity requires an explicit order amendment first.

A goods receipt records actual quantities and serials, permits partial receipt, and updates stock once. Proposed accounting: value the receipt at agreed order cost and offset inventory against a goods-received-not-invoiced clearing account. The supplier invoice clears the receipt accrual and records the payable, taxes, and any difference between receipt and invoice cost. The supplier invoice must not receive the same units again. Price differences use the stock-replay/cost-adjustment mechanism so sold stock and remaining inventory reconcile.

This phase supports receipt-first and simultaneous receipt/invoice purchases. Invoice-first purchasing, invoiced-but-unreceived inventory, and supplier-deposit allocation are excluded; an invoice must never imply physical receipt when goods have not arrived. Support partial billing and invoices covering multiple receipts from the same supplier. Track received and billed quantities independently. Keep the existing direct purchase-invoice workflow for purchases received and billed together; it must not duplicate a linked receipt. Changes and reversals must check dependent receipts, invoices, sales, and returns and either reverse consistently or explain the dependency blocking the action. Purchase returns must retain receipt/serial links and distinguish returns before billing from supplier credits after billing.

## 5. Landed costs

Record acquisition costs such as freight once as a payable or payment, then allocate eligible amounts to one or more receipts. Prevent duplicate recognition when a charge is already recorded. Keep recoverable tax outside inventory cost under the existing tax model; this feature does not assert jurisdiction-specific tax compliance.

Default allocation is proportional to receipt line value, with quantity-based and explicit manual allocation alternatives. Show a preview; allocated amounts must exactly equal the charge after rounding. Zero-value allocation bases require another method or manual values. Allocate within Hisab's existing JOD/precision model; multi-currency purchasing is outside this phase.

Costs arriving after sales must update remaining inventory and affected cost of sales through the existing costing mechanism and retain cost history. For both late supplier-price differences and landed costs, block posting if the required replay would change a closed period. Explain the affected periods and require an administrator to reopen them through the existing audited process before applying the correction and closing again. Do not silently use a current-period adjustment to bypass the lock. A separate current-period correction mechanism is outside this phase.

## 6. Replenishment

Configure minimum and target quantities per item/location, with target at least minimum. Suggested purchase quantity is max(0, target minus projected quantity), triggered when projected quantity falls below minimum. Projected quantity is on-hand plus outstanding issued purchase-order quantities assigned to the location. Exclude cancelled quantities and draft orders. Reservations are not included until sales-order functionality exists.

Show on-hand, on-order, expected dates, overdue orders, and the calculation. Do not disguise overdue incoming stock as available today. Staff can override a suggestion, select a supplier, and create a draft purchase order. Never place orders automatically. No demand forecasting or automatic supplier ranking is required.

## Delivery sequence and acceptance

1. Locations, serial/barcode tracking, and existing sale/return integration. Migration preserves quantities, valuation, and documents; single-location users retain simple operations. A serial traces purchase through sale and return; transfers preserve company totals.
2. Stocktaking. Counts resume after restart, block conflicting movements, and produce auditable balanced adjustments with serial/quantity agreement.
3. Purchase orders and partial receipts. Receiving and invoicing in separate steps increases inventory exactly once; accruals, payables, price variances, returns, and reversals reconcile.
4. Landed costs. Allocations reconcile to the charge, update sold and unsold costs correctly, and respect period locks.
5. Replenishment. Suggestions explain their inputs, account for open orders, and create draft orders only on user action.

For each increment, verify atomic rollback, backup/restore, schema migration/version compatibility, permissions, period locks, Arabic/English use, large text, and scan/manual input. Add focused tests for meaningful stock and accounting invariants. Existing reported test results are not proof of these new features.

## Decisions left to the builder

Choose schema names, code organization, indexes, barcode normalization details, exact chart-account mappings, and screen layout consistent with the existing app. Validate performance with realistic inventory sizes. Develop the exact transaction and stock-replay plan before changing product code. Escalate any conflict that changes business behavior rather than silently expanding scope.

## Approval

This document is the proposed scope and business behavior. User approval is required before implementation planning under the selected Superpowers brainstorming workflow. The highest-impact proposed defaults are item-scoped stock-count locks, receipt accrual accounting, immediate internal transfers, receipt-first/simultaneous purchasing only, and explicit administrator reopening when late costs would change closed periods. A builder review identified the last two policies; they are now explicit proposals for approval.

