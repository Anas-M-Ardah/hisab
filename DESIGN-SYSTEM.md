# Hisab interface direction

Hisab is an Arabic-first Windows accounting application used by both older users and users who prefer compact layouts. The design should feel quiet, precise and dependable. Transactions, names and balances are the primary content.

## Visual system

- Neutral light canvas and sidebar; white content surfaces; blue reserved for the current location and primary actions.
- Semantic brushes in `Theme.xaml`, with Windows high-contrast overrides in `UiTheme.cs`.
- Consistent outline icons, restrained borders, 8-DIP control corners and 12-DIP content corners.
- Segoe UI for locally available English and Arabic rendering, with no network font dependency.
- Page title 28 DIP at the standard text setting, field labels 16, table text 18 and navigation 17. Small settings keep table text and field labels at least 14 DIP.
- Text preferences remain 14–28. Control height and table rows scale with the saved text size; no forced change to existing preferences.
- Selected navigation uses weight and background as well as color. Table selection retains readable text and keyboard focus.

## Interaction

Keep familiar native WPF keyboard, dropdown and calendar behavior. Dragging accounts has a Move account alternative and Undo. Preserve visible field labels and keep the everyday tasks reachable from Home. Account navigation must describe the full chart of accounts, including expenses.

## Sources reviewed

- [UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill): applied specific WPF DPI/layout and list virtualization guidance, and UX guidance for drag alternatives, text scaling and interaction states. Reviewed a local checkout; no global skill installation or new runtime dependency.
- [Apple Design Skill](https://github.com/NutshellEngineering/apple-design-skill): reviewed as an unofficial HIG reference candidate. It targets Apple platforms and is not an implementation recipe for WPF.

The automatic design-system generator returned marketing-page compositions on both queries. Those compositions, font choices and effect prescriptions were not applied. The native accounting layout above is a product-specific design decision, supported by the relevant targeted guidance.

## Verification

Render real WPF screens in Arabic and English, including 14-point table views and 24-point text at 1024×768. Check native popups and account collapse/expand, move and undo. Run the existing accounting and storage self-tests. Screen-reader, physical high-DPI and Windows high-contrast usability still require target-device review; semantic high-contrast colors are preserved in code.
