# Plan: Dashboard without repeated figures, in two rows

- **Branch:** `feat/dashboard-layout`
- **Status:** Done
- **Date:** 2026-11-17
- **Requirements:** [design-refresh.md Appendix 4](../feature-requirements/design-refresh.md#appendix-4-dashboard-without-repeated-figures-in-two-rows)

## Goal
The Dashboard shows the month result only once (in the key figure), no longer shows outputs by
kind or the changes against last month twice, and fits in two rows on desktop: the key figures,
then the Cash flow chart beside the highlight card, Coming up and the small cards.

## Requirement changes
[Appendix 4 of design-refresh.md](../feature-requirements/design-refresh.md#appendix-4-dashboard-without-repeated-figures-in-two-rows)
changes FR-10 items 3 to 6 and acceptance criterion 11 of the design refresh:
- FR-10 item 4: the overview card becomes the **Cash flow card**: only the 6-month inputs versus
  outputs chart, opening the Cash flow screen. The "Month result" heading, the big month result,
  the caption "Inputs minus outputs this month" and the comparison with last month are removed;
  the key figures and their change pills already show them.
- FR-10 item 5: the "This month's outputs by kind" card is removed from the Dashboard (it stays on
  the Month screen).
- FR-10 items 3 to 6, desktop: under the key figures one row of two equal columns: the Cash flow
  card on the left, as tall as the right column; the highlight card, Coming up and the small
  cards (two per line) stacked on the right. Nothing comes after it.
- Phones: one column in the order key figures, highlight card, Coming up, Cash flow card, small
  cards.
- Acceptance criterion 11: the Dashboard shows the month result once and no outputs by kind.

## Scope
- In scope: the Dashboard page, its texts and tests, the user manual's Dashboard section.
- Out of scope: the Month screen's outputs by kind chart (unchanged); the Dashboard API. The
  `outputsByKind` field of the Dashboard response is no longer read by the Dashboard but stays in
  the API, so this branch is front-end only; removing it is a separate clean-up if wanted.

## Design
- **Domain / data model changes:** none.
- **API changes:** none.
- **Database migrations:** none.
- **Front-end changes:**
  - `pages/DashboardPage.tsx`:
    - `Overview` is renamed `CashFlowCard`. It drops the heading, the big amount, the caption and
      the comparison lines (with the `Comparison` component and the `percentChange` use). The card
      gets a header with the title "Cash flow" as a link to `/cash-flow` (replacing the small
      "Cash flow" link at the bottom), then the `PairedBarChart`, which now fills the card's width.
    - The outputs by kind `Card` with its `RingChart` is removed, and with it the unused imports.
    - The last three rows become one grid, `lg:grid-cols-2 lg:grid-rows-[auto_auto_1fr]`, with
      the children in phone order: `HighlightCard`, `ComingUp`, `CashFlowCard`, `SmallCards`. On
      desktop `CashFlowCard` is placed in column 1 spanning the three rows
      (`lg:col-start-1 lg:row-span-3 lg:row-start-1`), so the other three fill column 2 from top
      to bottom. The small cards keep their `sm:grid-cols-2` grid.
    - The chart grows with the card on desktop: `CardContent` and the chart are flex columns that
      fill the card, and the chart area is `h-56` on phones and `lg:flex-1 lg:min-h-56` on
      desktop.
  - `components/design/PairedBarChart.tsx`: an optional `className` for the element around the
    chart and its legend, so the Dashboard can let it fill the card. Other users (Cards screen)
    are unchanged.
  - `components/dashboard/texts.ts`: the `overview` texts (title, caption, comparison lines) and
    the `outputsByKind` texts are removed; `cashFlow` keeps only the title and the chart label.
  - `docs/user-manual/index.html`: the Charts bullet becomes a Cash flow bullet: the chart of
    inputs vs. outputs of the last six months, without outputs by kind.

## Steps
1. Rework `Overview` into `CashFlowCard` and remove the outputs by kind card.
2. Arrange highlight card, Coming up, Cash flow card and small cards in one two-column grid and
   let the chart fill its card.
3. Update the Dashboard texts.
4. Update the Dashboard tests.
5. Update the user manual.
6. Run lint, type check and front-end tests; check the layout in a browser at desktop and phone
   width.

## Tests
- Unit tests (`dashboard.test.tsx`):
  - The Cash flow card has no "Month result" heading or "Inputs minus outputs this month"
    caption, and the month result amount appears only in the Month result key figure.
  - The Cash flow card shows no "more/less inputs/outputs than last month" lines.
  - No "This month's outputs by kind" text on the Dashboard; the small cards test keeps checking
    that only sections with data are shown.
  - The Cash flow card links to `/cash-flow`.
- API / integration tests: none (no API change).
- Manual checks: desktop (about 1440 px wide) shows the key figures and one more row: the chart
  on the left, the highlight card, Coming up and the small cards on the right, nothing below;
  phone width shows key figures, highlight, Coming up, chart, small cards with no sideways
  scrolling; with no loans, card payments, shared costs or recurring bills the right column ends
  after Coming up and the chart is as tall as it; the Month screen still shows its ring chart.
