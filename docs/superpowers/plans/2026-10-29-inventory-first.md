# Inventory-first Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver location-aware inventory, device tracking, stock counts, procurement, landed costs, and replenishment in the existing desktop app.
**Architecture:** Extend Store and AccountingService partial classes. Keep the existing document/stock replay engine authoritative for valuation; add location and serial validation to every rebuild. Use dedicated bilingual WPF screens with existing styling.
**Tech Stack:** C#/.NET 10, WPF, SQLite.
**Spec:** ../specs/2026-10-29-inventory-design.md

## Global Constraints
- Default Main stock; single-location controls hidden.
- Company-wide weighted-average costing; JOD integer thousandths.
- Preserve period locks, transactions, audit evidence, encrypted backup/restore.
- No cloud, repair management, invoice-first procurement, or separate branch accounts.

## Review Focus
- Serial and location checks cannot be bypassed through legacy opening, returns, amendments, order transfers or reversals (Task 1).
- Migration and replay must not duplicate stock or lose serial histories (Task 1).
- Count locks must survive restart and reject historical conflicting transactions (Task 2).
- Partial receipt/billing/return and cancellation must not double inventory or payables (Task 3).
- Late charges must reconcile sold and unsold values and reject locked periods (Task 4).

## Task 1: Location and serial foundation
Files: create InventoryStore.cs, InventoryService.cs, InventoryTests.cs; modify Store.cs, StoreV2.cs, AccountingService.cs, AccountingV2.cs, SelfTest.cs.
Interfaces: Store.Locations(), SaveLocation(long?,string,bool), LocationQuantity(long,long); AccountingService.ConfigureInventoryItem(long,string,bool,IReadOnlyList<string>), TransferInventory(DateTime,long,long,long,decimal,string,string). Extend InvoiceLine with Location=1 and Serials="". Extend ReturnLine with Serials="".
- [ ] Test migration to Main stock, duplicate serial rollback, transfers and sale/return serial lifecycle, location oversell, reverse dependencies, negative serialized stock.
- [ ] Build and run self-test; confirm new checks fail before implementation.
- [ ] Implement schema 4, location replay and serial validation, all legacy entry points, and historical dependencies.
- [ ] Run full self-test and commit.

## Task 2: Stock counts
Files: create InventoryCounts.cs; extend InventoryTests.cs.
Interfaces: StartStockCount(long,IReadOnlyList<long>), RecordStockCount(long,long,decimal,string,decimal), CompleteStockCount(long,string), CancelStockCount(long).
- [ ] Test movement lock, restart persistence, gains/losses, serialized discrepancies, cancel and reversal.
- [ ] Run tests to failure, implement count snapshots and atomic adjustment documents with costing replay.
- [ ] Full self-test green, commit.

## Task 3: Procurement
Files: create Purchasing.cs; extend AccountingV2.cs and InventoryTests.cs.
Interfaces: PurchaseOrderLine(long Item,decimal Quantity,decimal UnitCost); CreatePurchaseOrder(long,long,DateTime,IReadOnlyList<PurchaseOrderLine>); IssuePurchaseOrder(long); CancelPurchaseOrder(long); ReceivePurchaseOrder(long,DateTime,IReadOnlyList<ReceiptInput>); BillReceipts(long,DateTime,IReadOnlyList<BillInput>,decimal).
- [ ] Test draft order non-posting, partial receiving, overreceipt rejection, partial/multi-receipt billing, invoice price variance, supplier validation, returns and reversal dependencies.
- [ ] Run tests to failure, implement receipt accrual, purchase invoices without second receipt, and safe correction paths.
- [ ] Full suite green, commit.

## Task 4: Landed cost and replenishment
Files: create LandedCosts.cs, Replenishment.cs; extend InventoryTests.cs.
Interfaces: AddLandedCost(DateTime,long,long?,decimal,IReadOnlyList<CostAllocation>,string), SetReorderRule(long,long,decimal,decimal), ReorderSuggestions().
- [ ] Test exact rounding, value/quantity/manual allocation, posted source reuse protection, sold/unsold replay, closed periods; test minimum/target/on-order/overdue semantics and user-created draft order.
- [ ] Run tests to failure, implement costs and suggestions with no automatic orders.
- [ ] Full suite green, commit.

## Task 5: Desktop workflows and delivery
Files: create InventoryScreens.cs, PurchasingScreens.cs; modify MainWindow.cs, ExtendedScreens.cs, ReportsAndPrint.cs; documentation and preview checks.
- [ ] Add UI checks for routes and single-location selectors; run to failure.
- [ ] Add inventory workspace, serial history/configuration, count sessions, locations/transfers, purchasing, receiving/billing, costs and reorder actions.
- [ ] Integrate barcode/location/serial input with existing invoice, opening and return forms; document exceptions and expose all operations through UI.
- [ ] Build, run complete tests, generate bilingual previews and inspect layouts. Commit.
- [ ] Fresh whole-branch review, fix material findings with regression tests, export source patch and Windows package with validation notes.
