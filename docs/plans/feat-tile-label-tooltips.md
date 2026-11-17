# Plan: Key figure labels as icon tooltips

- **Branch:** `feat/tile-label-tooltips`
- **Status:** In progress
- **Date:** 2026-11-17
- **Requirements:** [tile-label-tooltips.md](../feature-requirements/tile-label-tooltips.md) (approved),
  [design-refresh.md Appendix 3](../feature-requirements/design-refresh.md#appendix-3-key-figure-labels-as-icon-tooltips),
  [overall-balance-tile.md Appendix 1](../feature-requirements/overall-balance-tile.md#appendix-1-label-as-an-icon-tooltip)

## Goal
Key figure tiles show only their icon, number, change pill and detail. The figure's name appears
as a tooltip on the icon (hover, keyboard focus, long press) and is always read by screen readers.

## Requirement changes
[Appendix 3 of design-refresh.md](../feature-requirements/design-refresh.md#appendix-3-key-figure-labels-as-icon-tooltips)
changes FR-2 of the design refresh:
- FR-2: a key figure card shows the coloured icon, the amount, and where it has them a detail and a
  change pill. The short label is not shown on the card; it is a tooltip on the icon (about one
  second of pointing, keyboard focus, long press on touch) and screen readers read it with the
  amount.

[Appendix 1 of overall-balance-tile.md](../feature-requirements/overall-balance-tile.md#appendix-1-label-as-an-icon-tooltip)
changes FR-1, FR-3 and acceptance criterion 3:
- FR-1: "Overall balance" is a tooltip on the icon, not text under the number.
- FR-3: the line under the number shows only the details (e.g. "2 primary accounts"), or nothing.

## Scope
- In scope: FR-1 to FR-5, NFR-1 and NFR-2 of the requirements; every tile listed in FR-3 (all of
  them use the shared `StatCard`); the user manual sentence.
- Out of scope: section 5 of the requirements; the tiles in FR-4 (they do not use `StatCard`).

## Design
- **Domain / data model changes:** none.
- **API changes:** none.
- **Database migrations:** none.
- **Front-end changes:**
  - `components/design/StatCard.tsx` is the only place that renders key figures, so the change is
    made there and all 30 tiles follow (FR-3). The `label` prop stays; pages only change where the
    label text is also repeated in a detail.
    - The visible label line is removed. The detail, when present, is shown alone under the number
      (no leading " · "). When there is no detail, nothing is shown under the number (FR-1).
    - The label is rendered first in the card as screen-reader-only text, so a screen reader reads
      "Outstanding, 427,69 €, …" (FR-2). Existing tests that find tiles by their label text keep
      working.
    - The `IconTile` becomes the tooltip trigger, using the same Radix tooltip and look as
      `QuickActionButton` (dark bubble, `side="bottom"`), so it does not cover the number (NFR-1).
    - **Tile that opens a screen** (`to`): the whole card stays one link (no second focusable
      element inside it). The tooltip is controlled: it opens when the pointer rests on the icon,
      and when the link gets keyboard focus (`:focus-visible` only, not on a mouse click); it closes
      on blur and pointer leave.
    - **Tile that opens nothing:** the icon gets `tabIndex={0}` and is the trigger, so focus on it
      shows the tooltip (FR-2). Its accessible name is the label and the separate screen-reader
      text is not added, so the label is not read twice.
    - **Long press** (touch): pressing the icon for 500 ms opens the tooltip and swallows the
      following click, so a short tap still opens the screen, as in `QuickActionButton`. The
      long-press handling is moved out of `QuickActionButton` into a small shared hook
      (`components/design/useLongPress.ts`) used by both; the quick action behaviour is unchanged.
  - `StatGrid` wraps its tiles in a `Tooltip.Provider` with the quick action delay
    (`QUICK_ACTION_LABEL_DELAY_MS`, about one second), overriding the menu's 300 ms provider.
  - `components/dashboard/overallBalance.ts`: `overallBalanceDetail` already returns only the
    details; its doc comment is updated (Appendix 1, FR-3). No text changes elsewhere: the other
    details ("… received", "… paid", "2 overdue", next payment date) already read well alone.
  - Tiles keep the same height within a row: the grid already stretches its items (NFR-2).
  - `docs/user-manual/index.html`, section 1 Getting started: one sentence that the name of a
    tile's figure appears when pointing at its icon, focusing it, or long-pressing it on a phone
    (FR-5).

## Steps
1. Extract the long-press logic from `QuickActionButton` into `useLongPress`; quick action tests
   still pass.
2. Change `StatCard` and `StatGrid` as above.
3. Update the `overallBalanceDetail` comment; check every `StatCard` use still reads well without
   its label.
4. Add the tests below and update any test that expects the "Label · detail" text.
5. Update the user manual.
6. Run front-end lint, type check and tests; check the screens in the browser on desktop and phone
   width.

## Tests
- Unit tests (Vitest + Testing Library):
  - `StatCard` (new `components/design/statCard.test.tsx`): no visible label line; the label is in
    the card's accessible text before the number; the detail is shown without " · "; focusing a
    linked tile shows a tooltip with the label; focusing the icon of a tile without link shows it;
    a long press (touch pointer, 500 ms with fake timers) on the icon shows it and the following
    click does not navigate; a short tap navigates.
  - Dashboard: the Overall balance tile shows "2 primary accounts" without "Overall balance" in
    front (Appendix 1, criterion 3); existing quick action tests unchanged.
  - Shared costs: the four tiles are found by their labels (accessible text) and show their amounts.
- API / integration tests: none (no back-end change).
- Manual checks: acceptance criteria 1 to 6 on Dashboard, Month, Cash flow (year and monthly
  report), Cards (with and without a revolving card), Loans and Shared costs, at desktop and phone
  width; VoiceOver reads each tile's label with its number.

## Open questions
- None.
