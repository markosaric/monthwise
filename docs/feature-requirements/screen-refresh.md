# Requirements: Screens show every saved change at once

- **Branch:** `fix/screen-refresh`
- **Status:** Approved (2026-11-12)
- **Date:** 2026-11-11

> This document describes **what** the fix must do, not **how** it is built.
> The implementation plan (`docs/plans/fix-screen-refresh.md`) is written after these
> requirements are approved.

## 1. Goal

Several requirements already say that a change shows up everywhere at once, for example
[input-cash-flow.md](input-cash-flow.md) acceptance criterion 8, [overall-balance-tile.md](overall-balance-tile.md)
FR-7 and [shared-bills.md](shared-bills.md) acceptance criterion 6. Today this holds only for the
screens each kind of change was written for. A change of one kind can leave numbers or names from
another part of the app out of date on the screen the user is looking at (for example the Month
screen, which shows inputs, bills, expenses, loan payments, card purchases and the key figures
together) until the screen is opened again or the page is reloaded. The main branch code review
lists this as finding L8.

After this fix, every change the user saves is shown on every screen at once, whatever kind of data
it changes and wherever it is made.

## 2. Terms

| Term | Meaning |
|---|---|
| Saved change | Adding, editing, deleting, stopping, paying, reconciling or reordering anything the app stores, including settings such as cards, categories, banks, card issuers, participants and payers. |
| Shown data | Everything a screen or an open form shows that comes from stored data: lists, totals, key figures (including the Overall balance), balances, charts, the monthly report, choice lists in forms and the Dashboard. |

## 3. Functional requirements

**FR-1 Every saved change updates all shown data**
- After a saved change succeeds, all shown data on the open screen, and in any form or panel open on
  it, is loaded again, not only the data of the part where the change was made.
- This holds for every kind of change in every part of the app: inputs and cash flow, bills and
  expenses, recurring bills, loans and leases, cards and accounts, installment purchases, revolving
  cards, shared costs and their participants, and the settings lists.
- Screens that are not open show the new data when they are opened, as today.

**FR-2 The Dashboard is always current**
- The Dashboard key figures, the Overall balance tile, the chart and every other part of it show
  the effect of every saved change, whether the change was made on the Dashboard (for example
  through a quick action) or on another screen.

**FR-3 Failed changes**
- A change that fails changes nothing on the screens; the error is shown as today.

**FR-4 Unchanged behaviour elsewhere**
- Import and restore keep reloading every screen (see [backup-and-restore.md](backup-and-restore.md)
  FR-7), including the reload after an import made on another device.
- The status check of the app (see [design-refresh.md](design-refresh.md)) keeps its own timing
  and is not reloaded by a saved change.
- Data that is only about the export reminder and the backup history keeps updating as it does
  today.

## 4. Non-functional requirements

- No visible change to any screen besides the numbers and names being current: no flicker, no
  lost scroll position, and form fields the user is typing in keep their values.
- After a saved change, the updated numbers appear without a delay the user notices on a normal
  home connection.
- The rule holds for screens and kinds of change added later, without each new feature having to
  list which other screens it affects.

## 5. Out of scope

- Changes made on another device or in another browser tab, other than an import (FR-4). The
  other device shows them when a screen is opened again, as today.
- Changes to the back end or to what any number means; only when shown data is refreshed changes.
- Live updates pushed from the server.

## 6. Acceptance criteria

1. On the Month screen, adding, editing or deleting an input, a bill, an expense, a loan payment, a
   card purchase or a shared cost updates every section of the screen and the key figures,
   including the Overall balance tile, without reopening the screen.
2. Adding a bill, an input or a card purchase through a Dashboard quick action updates the
   Dashboard key figures, the Overall balance tile and the chart at once.
3. Renaming a card or account updates the name everywhere it is shown, including the "paid from"
   choices in open forms and the Card purchases screen, without reopening a screen.
4. Changing a loan or lease updates its payments on the Month screen and the Dashboard at once.
5. Changing a shared cost or a participant updates the card purchases, bills, loans and expected
   contributions it affects at once.
6. Adding or hiding a category, bank, card issuer or payer updates the choice lists in open forms
   and in forms opened after the change, without reloading the page.
7. A change that fails leaves every screen as it was and shows the error.
8. Importing a file or restoring the safety copy still reloads every screen; the status check keeps
   running on its own timing.
