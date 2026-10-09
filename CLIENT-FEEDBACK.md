# Account groups, stock flow and cheque receipts

This change implements client feedback in the existing Arabic and English Windows app.

- **Account hierarchy:** create Expenses with type Expense, then choose it as the parent of Water or Transportation. Any account can have nested children of the same type. The accounts screen shows the account's own balance and its balance including descendants. Group totals overlap and should not be summed. Statements can include all descendants or just the selected account. Posting remains on the selected account; grouping never creates additional journal entries.
- **Items and stock:** In and Out replace average cost in the items screen and inventory report. They are quantities across all history, including opening stock, returns and reversal movements. In minus Out equals available quantity. The movement report shows separate In and Out columns for its selected date range. Inventory costing remains unchanged internally.
- **Receiving multiple cheques:** open Receive money → Receive multiple cheques, or use the same action in the cheque register. Enter the receipt total, add each cheque's number, bank, amount and due date (تاريخ الاستحقاق), then review and save. The total must match. The whole batch saves atomically. Each cheque has its own document and can clear, bounce or cancel independently. Optional invoice allocations are distributed in entry order up to the invoice's outstanding balance; excess remains on account.
- **Text and tables:** Settings offers 14–28 point text with a live sample. Navigation, headings, tables and dialogs scale with the preference. Existing users keep their saved size. The welcome screen includes smaller presets. Tables have consistent spacing, full-row selection, aligned numeric columns, numeric sorting and horizontal scrolling when columns need more room.

Existing databases and restored backups receive a nullable parent column automatically; all existing accounts initially remain at the top level. No sample transactions are added to real ledgers. Preview data lives only in the explicit preview directory.

## Validation

Run `dotnet build source/Hisab.csproj -c Release` and `Hisab.exe --self-test <temporary-directory>`. The suite includes migration and reopening, cycle/type rejection, recursive totals, reparenting, stock reversal reconciliation, cheque totals and duplicates, atomic rollback, independent cheque settlement and allocations.

Run `Hisab.exe --preview --preview-demo <temporary-directory>` to render Arabic and English screens. Preview checks exercise dropdown/calendar popups and grouped statements, with screenshots at 14-point text and 24-point text in a smaller window. Review on the client's display and printer before distributing a new release.
