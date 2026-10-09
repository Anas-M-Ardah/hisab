# Hisab placement and interaction system

Hisab targets Windows WPF, with Arabic RTL and English LTR. Preserve the original Hisab palette. Improve element placement, hierarchy, grouping and interactions.

## Accounts screen

**Purpose:** find, create, edit and arrange accounting groups while keeping balances visible.

**Navigation shell:** retain the original dark blue sidebar, white navigation text and blue active item. Navigation and everyday task colors remain those of v0.4.0.

**Primary content:** an outline-style table. Put account names, indentation and disclosure controls together in the first column; follow with code, type and balances. Emphasize parent labels. Keep columns resizable and use the existing alternating backgrounds. Flat sorting stays disabled to prevent separating children from parents.

**Supporting patterns:** search and account creation share the upper control region. On narrow windows, creation actions move below search. Selected-account editing and arrangement actions sit above the table, with Undo nearby. The top-level drop target sits immediately before the outline. Instructions and total interpretation sit below the content.

**States:** edit, move, child creation and names actions enable when a row is selected, respecting permissions. Disclosure controls reveal children; expansion choices survive navigation within the session. Drag targets provide feedback; Move account remains a click and keyboard alternative. Undo restores the latest move.

**Accessibility:** preserve visible labels, meaningful disclosure-button names, keyboard focus, 14–28 saved text preferences and high-contrast resources. Reading order mirrors in Arabic. Layout dimensions below are Hisab decisions, not Apple HIG measurements.

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

Render actual Arabic and English WPF screens at small and large text settings. Check collapse/expand, expansion retention across navigation, move/undo, grouped statements and native popups. Run the 133 existing storage/accounting checks. Verify original brush colors against the hierarchy baseline. Target-device screen-reader, DPI and high-contrast usability remain to be reviewed.
