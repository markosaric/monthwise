# Requirements: Key figure labels as icon tooltips

- **Branch:** `feat/tile-label-tooltips`
- **Status:** Approved (2026-11-17)
- **Date:** 2026-11-17

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-tile-label-tooltips.md`) is written after these
> requirements are approved.

## 1. Goal

The key figure tiles at the top of the overview screens repeat a grey label under every number
(e.g. "Outstanding", "Overdue", "Received this month", "Participants" on the Shared costs screen).
Once the user knows the screen, the icon and the number are enough, and the labels only add noise.

This feature removes the label from the tile and shows it as a **tooltip on the tile's icon**,
the same way the Dashboard quick actions already show their labels
([design-refresh.md](design-refresh.md) FR-14).

## 2. Terms

| Term | Meaning |
|---|---|
| Key figure tile | A stat card in the key figures row at the top of a screen: coloured icon, large number, label, and optionally a change pill and a detail ([design-refresh.md](design-refresh.md) FR-2). |
| Label | The short name of the figure, e.g. "Outstanding". |
| Detail | Extra information shown after the label, e.g. "1.200,00 € received" or "2 overdue". |

## 3. Functional requirements

**FR-1 Label hidden on the tile**
- A key figure tile shows its icon, its number and, where it has them, its change pill and its
  detail. It no longer shows its label.
- The detail stays visible under the number, on its own, without the label in front of it.
- Nothing else on the tile changes: icon, colours, red for problems, the change pill, and opening
  the screen behind the tile when it is selected.

**FR-2 Label as a tooltip on the icon**
- When the pointer rests on the tile's icon, the label appears as a tooltip after about one second
  and disappears when the pointer leaves, like the quick actions.
- When the tile (or, for a tile that opens nothing, its icon) gets keyboard focus, the label
  appears as well, and disappears when the focus leaves.
- On touch screens, where there is no hover, a long press on the icon shows the label. A short tap
  on a tile that opens a screen still opens it.
- Screen readers always read the label together with the number and the detail, e.g.
  "Outstanding, 427,69 €".

**FR-3 Tiles this applies to**

Every key figure tile in the app (all current uses of the stat card):

| Screen | Tiles |
|---|---|
| Dashboard | Inputs, Outputs, Month result, Still to pay, Overall balance |
| Month | Inputs, Outputs, Month result, Still to pay, Overall balance |
| Cash flow (year) | Inputs, Outputs, Result, Average per month |
| Cash flow (monthly report) | Inputs, Outputs, Month result |
| Cards | Current balance, Expected at end of month, Card payments this month, Installments left, Revolving debt (when shown) |
| Loans | Active loans, Monthly payments, Still to repay, Next payment |
| Shared costs | Outstanding, Overdue, Received this month, Participants |

Key figure tiles added later follow the same rule.

**FR-4 Tiles this does not apply to**
- Tiles whose text is their content, not a label of a number, keep their text: the section links
  on the Dashboard, the Settings tiles, the loan cards on the Loans screen, the Savings "planned"
  notice and the export reminder.
- Section titles next to an icon (e.g. "Bills", "Expenses" on the Month screen) stay visible.
- Chart legends and axis labels stay visible.

**FR-5 User manual**
- The user manual says in one sentence that the name of a key figure appears when pointing at, or
  long-pressing, its icon.

## 4. Non-functional requirements

- **NFR-1 Accessible:** the label is reachable with the keyboard, on touch screens and by screen
  readers (FR-2); the tooltip has enough contrast against the page and does not cover the number
  of the tile it belongs to.
- **NFR-2 Responsive:** works on phone, tablet and desktop; tiles keep the same height within a
  row whether or not they have a detail.

## 5. Out of scope

- Changing which key figures a screen shows, their order, values, icons or colours.
- Tooltips on other icons of the app.
- A setting to show the labels again.

## 6. Acceptance criteria

1. On every screen in FR-3, the key figure tiles show no label; details and change pills are still
   shown.
2. Pointing at a tile's icon for about one second shows its label; moving away hides it.
3. Moving to a tile with the Tab key shows its label.
4. On a phone, a long press on a tile's icon shows its label; a tap on a tile that opens a screen
   opens it.
5. A screen reader reads each tile's label with its number.
6. The tiles in FR-4 look as before.

## 7. Changes to earlier requirements

These requirements change requirements already merged to `main`. They are not rewritten; each gets
an appendix in this branch:

- [design-refresh.md](design-refresh.md) Appendix 3 (FR-2: the short label under the number
  becomes a tooltip on the icon)
- [overall-balance-tile.md](overall-balance-tile.md) Appendix 1 (FR-1 and FR-3: the label
  "Overall balance" is no longer shown under the number; the detail line shows only its details)
