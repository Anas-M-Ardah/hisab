# Excel import with preview / استيراد Excel مع المعاينة

Open **Items & stock → Import Excel / الأصناف والمخزون → استيراد Excel**. No Excel installation is needed to read the workbook. Use **Excel template** to save a blank `.xlsx` template with identifier columns formatted as text.

## Prepare the workbook

Use a standard `.xlsx` file with column headers and one product per row. Select the worksheet and header row (1–100), then match columns. English and Arabic header aliases are recognized; other headings can be mapped manually. After changing the header row, click Read headers & match columns again.

| Column | Meaning |
|---|---|
| Code | Required unique item code, stored as text. Codes are compared without case sensitivity. |
| Name | Required for a new item. Existing items retain their current names. |
| Type | Stock or Service (مخزني or خدمة). Blank means Stock for new items. |
| Price | Selling price; blank means zero for new items. |
| Barcode | Optional unique text barcode for new items. |
| OpeningQuantity | Optional opening quantity; blank means zero. |
| UnitCost | Positive opening unit cost when quantity is positive. Not the selling price. |
| Serialized | Yes/No, True/False, 1/0 or نعم/لا. Blank means No for new items. |
| Serials | One distinct text serial per serialized unit, separated by newlines, commas or semicolons. |

Code and Name mappings are required. Other fields can be left Not used. Existing item details are never overwritten; in opening-only mode, blank Type/Serialized inherit the existing values.

Store codes, barcodes and serials as actual text before entering them, especially values with leading zeros. Changing an already-rounded numeric identifier to text cannot recover its original digits: re-enter the original value. Formulas and error cells in mapped columns are rejected; use Paste Special → Values. Dates are selected in Hisab, not imported from the worksheet. Common Excel date formats in numeric input cells are flagged. Numeric values allow at most three decimal places; use a decimal point in text numbers, without thousands separators. Arabic digits are supported. Serialized quantities must be whole units.

Unmerge cells in the selected worksheet. Hidden rows are included, as are rows hidden by Excel filters. The reader supports up to 20 worksheets, 64 populated columns and 5,000 data rows; files must be at most 10 MB compressed / 30 MB expanded. Split larger workbooks. Save `.xls`, `.xlsm`, `.xlsb` or password-encrypted files as an unencrypted standard `.xlsx` first.

## Preview and duplicates

The preview shows New, Opening stock, Skip and Error rows, plus the total opening-stock value. Errors only narrows the view. Double-click a row or use Row details to inspect source values and validation messages.

Existing codes are skipped by default. To populate an existing unused item, explicitly select Use existing items for opening stock only. That mode preserves names, prices, barcodes and tracking settings; it requires a positive opening quantity and no previous stock movements. Items with prior movements need normal purchasing or stock-count workflows instead. Duplicate codes within the file are errors, not merged quantities. Barcode conflicts and invalid serial lists are errors.

Choose one active location and one opening date for the batch. Closed periods, future opening dates and open stock counts are checked. New serialized-item registration also requires the current date to be open.

## Save

Nothing is saved during file selection, mapping or preview. Correct errors in Excel, save and reload the workbook. The app imports the loaded preview snapshot, not later edits to the file. Import becomes available only when there are no errors and at least one New or Opening row. Review the final confirmation.

Products, opening-stock documents, serials, journal entries and the import audit entry are saved in one transaction. A failure rolls back the entire batch. Database changes that alter the reviewed result require a fresh preview. Reimporting the same file skips matching codes by default; it cannot post another opening balance for an item with stock movements. Stock receipts and cost corrections are not supported through this importer.

The app may be busy while committing a large batch; wait for the success message. Review imported stock in Item history and the opening documents in Invoices & documents.

Schema remains 6. Keep your normal backups. Validation covers 342 automated checks (40 import checks), plus Arabic/English mapping, validation, row-detail and import-button previews at 14/24-point text.

Implementation references: Microsoft Open XML SDK 3.5.1; https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/how-to-retrieve-the-values-of-cells-in-a-spreadsheet . License included in ThirdParty/OPENXML-LICENSE.txt.
