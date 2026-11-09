# Item history / سجل الصنف

Select an item in **Items & stock** or **Inventory management**, then choose **Item history / سجل الصنف**.

- Search by document reference, serial number or notes. Search ignores letter case.
- Filter by inclusive dates, stock location (including inactive locations), and activity type. Blank dates include all dates. Use Apply filters; Clear filters restores the full history.
- Current stock, reserved and available totals apply to the selected location, independent of the date/activity/search filters. They are current totals, not historical balances.
- Each row represents one document at one location. Transfers have separate outgoing and incoming rows. Repeated invoice lines for the same item/location are grouped.
- Activity details shows the customer or supplier, document quantity, notes and serial numbers. Open source opens the document or reservation. Double-click a row for details.
- In/Out columns show physical movement. Supplier billing for previously received goods and landed costs appear without receiving the stock again. Services have document quantities but no physical movement.
- Cancelled documents and reversal records remain visible and labeled. Both are excluded from current stock. Reversals use the original location and serial information.
- Reservations show their creation date and current status, held quantity and optional serials in details. They do not represent physical movements or a dated log of each status change.
- Includes opening stock, sales and purchases, returns, transfers, posted stock counts, serial registration, cost adjustments, allocated landed costs and reservations. Unreceived purchase orders and unreserved quotations are not stock activity.

This read-only feature retains schema 6 and does not alter stock or accounting records. Arabic and English layouts respect saved text size. Use Refresh to reload activity after changing a source record.

Validation: 302 automated checks, including 21 item-history checks; Arabic/English previews at 14 and 24-point text. Physical screen-reader and high-DPI acceptance remain manual checks.
