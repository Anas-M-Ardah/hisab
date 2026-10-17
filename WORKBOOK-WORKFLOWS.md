# Workbook workflows

The reference workbook supplies example forms, not transactions to seed into a ledger.

1. Create main accounts and children of the same type in Accounts. Account fields offer a main-account filter when eligible children exist. Clearing it restores all eligible accounts, including existing directly posted parents.
2. In Receive money or Pay money, choose Split amount across accounts. Add each account, amount and description. Editing reverses and replaces the voucher atomically. Invoice allocation continues to use single-account vouchers.
3. Journal entries retain per-line descriptions. Statements and printed vouchers show them; printed vouchers also show each immediate parent account.
4. Reports offers Main-account and Child-account trial balances. Select a row to view its children or statement. Movements use the selected period; balances include history through its end. Direct postings to a parent appear separately without double counting.
5. Saving multiple cheques opens a combined receipt with each number, bank, due date, amount and current status. Open any batch member's issue document to print the full schedule. Historical cheques are not grouped without reliable batch data.
6. For order costing, create an Asset main account (Orders) and a non-system leaf child (Order 1). Record costs to that child through vouchers/journals. In Items & stock, choose Transfer order to stock and enter each received item's quantity and total cost. The journal credits the order and debits stock. This receives new inventory; it does not reclassify goods already received through a purchase invoice. Transfer cost cannot exceed the order's balance. Resolve negative inventory first. Cancellation restores order cost and quantities, subject to chronological stock checks.

Invoice terms and line totals, inventory in/out columns and date-range statements already existed and remain available. Workbook inconsistencies are not imported as authoritative mappings. This update does not add automatic Excel import, procurement order management, or mixed cash-and-cheque receipts.

Schema 3 adds line descriptions and cheque batch references. Older versions reject upgraded databases instead of rebuilding stock without order transfers. Keep a pre-upgrade backup when testing the preview.

Verification covers split postings/amendments, period versus closing totals, direct parent postings, stock transfer/sale/reversal reconciliation, rollback, cheque batches, persistence and permissions. Arabic/English preview checks exercise account filtering and reports and capture normal/large-font forms. Physical mouse, printer and target-machine DPI testing remain manual.
