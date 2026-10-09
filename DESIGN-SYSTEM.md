# Hisab placement and interaction system

Hisab targets Windows WPF, with Arabic RTL and English LTR. Preserve the original Hisab palette. Improve element placement, hierarchy, grouping and interactions.

## Accounts screen

**Purpose:** find, create, edit and arrange accounting groups while keeping balances visible.

**Navigation shell:** retain the original dark blue sidebar, white navigation text and blue active item. Navigation and everyday task colors remain those of v0.4.0.

**Primary content:** an outline-style table. Put account names, indentation and disclosure controls together in the first column; follow with code, type and balances. Emphasize parent labels. Keep columns resizable and use the existing alternating backgrounds. Flat sorting stays disabled to prevent separating children from parents.

**Supporting patterns:** search and account creation share the upper control region. On narrow windows, creation actions move below search. Selected-account editing and arrangement actions sit above the table, with Undo nearby. The top-level drop target sits immediately before the outline. Instructions and total interpretation sit below the content.

**States:** edit, move, child creation and names actions enable when a row is selected, respecting permissions. Disclosure controls reveal children; expansion choices survive navigation within the session. Drag targets provide feedback; Move account remains a click and keyboard alternative. Undo restores the latest move.

**Accessibility:** preserve visible labels, meaningful disclosure-button names, keyboard focus, 14–28 saved text preferences and high-contrast resources. Reading order mirrors in Arabic. Layout dimensions below are Hisab decisions, not Apple HIG measurements.

## App-wide composition

Home separates balance summaries, everyday transaction actions and recent activity. Tables in Accounts, stock, Documents, Cheques and user administration use a bounded content area with controls above, rather than a table embedded deep in a long scrolling page. On short windows, the outer page can scroll while keeping a usable minimum table area.

Documents prioritizes Open/print, Amend and Return, with allocation, attachments, translations and cancellation in More. Commands require a selected row. Stock creation sits beside search; selected-item actions form a separate group.

Report filters group report type and date range horizontally when space permits. Account and child-account options appear only for statements. Settings groups Appearance/language and Company/printing into cards, places protection and branding options in expandable sections, and keeps Save visible below scrolling content.

Shared forms have a persistent title and Cancel action. Standalone completion actions remain visible in a footer; the invoice retains its existing totals/review footer. Form commands, validation and unsaved-change guards are retained. These are Windows adaptations of Apple layout, toolbar and scoped-task principles.

## Shared layout tokens

- Preserve every original semantic brush color from the hierarchy baseline. Additional selection aliases use existing Primary and OnPrimary colors.
- Use locally available Segoe UI for English and Arabic.
- Standard setting: page title 28 DIP, field label 16, table text 18, navigation 17. Table text and field labels remain at least 14 DIP.
- Control and row heights scale with saved text size; existing preferences are retained.
- Consistent outline icons, 8-DIP control corners and 12-DIP content corners.

## Apple design skill applied

Read [Apple Design Skill](https://github.com/NutshellEngineering/apple-design-skill), an unofficial mirror of Apple's HIG. Applied these design-intent references:

- [Layout](https://developer.apple.com/design/human-interface-guidelines/layout): group related controls, align content, respect reading order and adapt to window/text sizes.
- [Toolbars](https://developer.apple.com/design/human-interface-guidelines/toolbars): prioritize frequent actions and group commands by function.
- [Outline views](https://developer.apple.com/design/human-interface-guidelines/outline-views): express hierarchy in the first column, use disclosure controls and retain expansion choices.
- [Lists and tables](https://developer.apple.com/design/human-interface-guidelines/lists-and-tables): provide clear headings, selection feedback and resizable columns.

Outline-view guidance is macOS-specific; Hisab adapts its information organization to WPF. Apple APIs, materials and platform-only toolbar behavior are not prescribed for Windows. No global skill installation or runtime UI dependency.

## Verification

Render actual Arabic and English WPF screens at small and large text settings. Check collapse/expand, expansion retention across navigation, move/undo, grouped statements and native popups. Run the 148 storage/accounting checks. Verify original brush colors against the hierarchy baseline. Target-device screen-reader, DPI and high-contrast usability remain to be reviewed.

## Table refinement

White rows with subtle horizontal rules replace alternating stripes. Primary names and final balances use semibold text; codes, dates and own balances are secondary. Numeric headings match right-aligned tabular figures. Compact labels distinguish item types and document statuses. Custom header chrome retains sort indicators and resize thumbs; scoped table scrollbars retain native tracks and paging commands. A live footer reports visible records and selection. Actual rounded clipping keeps headers and the footer within the table surface.

Preview checks invoke the native header click handler, exercise resize-thumb events, scroll to the last record and verify selection counts in Arabic and English.

## Account organizer

Accounts uses a tree and details view. Organize accounts opens a dedicated editor with explicit drag handles, 18-DIP vector chevrons inside 44-DIP controls, a top-level drop zone, a parent chooser, inline name/code editing and Undo. Dropping commits the move immediately. New child creation preselects its parent. Invalid cycles and cross-type moves are rejected by both UI and storage. Unsaved edits are guarded when selecting, leaving, changing language or closing.

The tree loads localized names and posted balances in one query and computes descendant totals once. Selection, disclosure and drag-over use the cached snapshot. Account and item pickers load translations in bulk. Language is cached until preferences change. Tables receive finite space directly from the workspace grid, preserving row virtualization and eliminating outer scrolling around tables. Scrollbar tracks have explicit orientations and a minimum thumb size.

Buttons, fields, pickers and checkboxes use one shared control-height token (at least 44 DIP). Rows use one shared row-height token (at least 52 DIP). Both scale with the saved font size. Multiline fields remain taller. Preview checks cover Arabic/English, 14/20/24-DIP text and smaller windows, native drop events, save/undo, finite table layout, sort/resize/scroll and popups. Native mouse gestures and target-device DPI/accessibility still require manual acceptance.
