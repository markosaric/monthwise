# Plan: Design refresh

- **Branch:** `feat/design_refresh`
- **Status:** Done
- **Date:** 2026-10-18
- **Requirements:** [design-refresh.md](../feature-requirements/design-refresh.md) (approved), with
  the mock-up [design-refresh-mockup.html](../feature-requirements/design-refresh-mockup.html), and
  [credit-card-settings.md, Appendix 3](../feature-requirements/credit-card-settings.md#appendix-3-cards-and-accounts-move-from-the-dashboard-to-the-cards-screen),
  [input-cash-flow.md, Appendix 2](../feature-requirements/input-cash-flow.md#appendix-2-account-balances-move-from-the-dashboard-to-the-cards-screen),
  [loans-and-leases.md, Appendix 2](../feature-requirements/loans-and-leases.md#appendix-2-loans-and-balances-on-the-redesigned-dashboard),
  [credit-card-installments.md, Appendix 2](../feature-requirements/credit-card-installments.md#appendix-2-expected-balance-on-the-cards-screen)

## Goal
The app gets a colourful, figure-first look: a collapsible menu with coloured icons and system
status only on a problem, a Dashboard that shows how the current month is going (with icon quick
actions and no balances), account balances on the Cards screen, and redesigned Month, Cash flow,
Loans, Shared costs and Settings screens. Nothing the app stores or calculates changes.

## Requirement changes
- [credit-card-settings.md, Appendix 3](../feature-requirements/credit-card-settings.md#appendix-3-cards-and-accounts-move-from-the-dashboard-to-the-cards-screen)
  (2026-10-18, changes FR-9, FR-10, FR-11, Appendix 1): the Dashboard **Cards** widget moves to a new
  **Accounts** part at the top of the Cards screen with the same rules (which cards, order, entry
  content, empty states, opening details); statuses are highlighted there and in Settings.
- [input-cash-flow.md, Appendix 2](../feature-requirements/input-cash-flow.md#appendix-2-account-balances-move-from-the-dashboard-to-the-cards-screen)
  (2026-10-18, changes FR-11, Appendix 1): current balance, expected end-of-month balance and the
  starting balance hint are shown on the Cards screen instead of the Dashboard; the calculation is
  unchanged.
- [loans-and-leases.md, Appendix 2](../feature-requirements/loans-and-leases.md#appendix-2-loans-and-balances-on-the-redesigned-dashboard)
  (2026-10-18, changes FR-3, FR-13): a blocked or expired account with a standing order stays in the
  Cards screen's Accounts part; the Loans widget becomes the Dashboard **Loans** small card (monthly
  total, still to repay, red on overdue, hidden without active loans).
- [credit-card-installments.md, Appendix 2](../feature-requirements/credit-card-installments.md#appendix-2-expected-balance-on-the-cards-screen)
  (2026-10-18, changes FR-9): the expected balance lowered by card payments is shown on the Cards
  screen.

## Scope
- In scope:
  - Theme: colour tokens (section, meaning and accent colours), display font, card style (FR-1,
    FR-4, FR-5).
  - Shared UI building blocks: stat card, change pill, status chip, progress bar, section card,
    ring and bar chart wrappers, quick action icon button with tooltip.
  - Left menu (FR-6 to FR-8), bottom navigation colours and the phone system-problem bar.
  - Dashboard (FR-9 to FR-11, FR-14) with one new read-only API endpoint.
  - Cards screen Accounts part (FR-12).
  - Month, Cash flow and monthly report, Loans and loan details, Shared costs, Settings redesigns
    (FR-15 to FR-20); the settings sub-screens and forms get the new style only (FR-5).
  - Tests at every layer touched.
- Out of scope:
  - Everything in section 5 of the requirements: dark mode, new data or screens, changing menu
    sections, colour settings, more system status detail, quick actions outside the Dashboard,
    another month on the Dashboard.
  - The Savings screen (still a placeholder).
  - Access control, as for earlier features.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| FR-1, FR-7 | Section colours as CSS tokens: Dashboard indigo, Month blue, Cash flow teal, Cards violet, Loans dark orange, Shared costs pink, Savings green, Settings grey. Meaning tokens: input green, output orange, expense dark yellow, expected = a lighter tint of its colour, problem red. Accent (highlight card) dark teal so white text stays readable; amounts written in green or orange use darker text shades (NFR-1). |
| FR-2 | Overview screens start with at most 4 `StatCard`s; the change pill compares with the previous month only where the API returns it. |
| FR-4 | Display font **Fraunces** (variable, `@fontsource-variable/fraunces`) for `h1`–`h3`, key figures and card titles; body stays **Geist**; key figures use `tabular-nums`. |
| FR-6 | Menu state `expanded`/`collapsed` in `localStorage` key `monthwise.menu`, default expanded; read and write wrapped in try/catch. Toggle is a `button` with `aria-expanded` and `aria-controls`. Collapsed width 72 px, labels as tooltips. |
| FR-8 | System status from the existing `useHealth` (polls every 30 s). Shown only when the query has an answer that is not healthy, or the API is unreachable; nothing while pending. Desktop: bottom of the menu; phones: red bar above the bottom navigation. `HealthStatus` card removed from the Dashboard. |
| FR-10 | Dashboard always uses today's month (server clock). Change pills: inputs green when higher, outputs green when lower, result green when better. Highlight card order: overdue (red) → next due → all paid. Coming up: next 7 days from today, at most 5, may span into next month. |
| FR-12 | Accounts part reuses the current `useDashboardCards` data and `CardSummary` rules; totals are the sums of the listed balances. |
| FR-14 | Five icon-only buttons (Add income, Add bill, Add expense, Cash withdrawal, Add purchase); label in a tooltip after 1 s hover, immediately on keyboard focus, always as `aria-label`; long press on touch. Add purchase only when a card can take a purchase (same check as the Cards screen). Forms get `?returnTo=/` and go back there after save or cancel. |
| FR-15 | Month keeps add buttons in their section headers, styled like FR-14 but with visible text. Fully paid or received sections start collapsed; collapsed state is not stored. |
| FR-16 | Period stays visible; the six other filters move into a popover behind **Filters** with removable chips. |
| NFR-4 | No calculation changes: every figure comes from an existing calculation; new values are only sums and differences of existing ones. |

### Domain / data model changes
- None.

### API changes (endpoints, DTOs)
- New `GET /api/dashboard` (tag `Dashboard`), handler `GetDashboardHandler` in
  `Monthwise.Application/Dashboard/`, using `TimeProvider` for today and reusing existing handlers
  and ledgers (no new calculations):

  | Field | Source |
  |---|---|
  | `month` | today's `yyyy-MM` |
  | `inputs`: total, received, previous month total | `GetMonthlyReportHandler` for this and the previous month |
  | `outputs`: total, paid, previous month total | same report (`Outputs.Totals`, `PreviousMonthOutputsCents`) |
  | `monthResultCents`, `previousMonthResultCents` | same report |
  | `stillToPay`: total, count, overdue total, overdue count | unpaid bills, loan payments and card payments of the month (existing month lists) |
  | `nextDue`: name, date, amount, kind | earliest unpaid output due today or later |
  | `comingUp`: up to 5 items (kind, name, date, amount, isInput) | unpaid outputs and expected inputs due in today … today + 6 days, this and next month |
  | `outputsByKind`: bills, expenses, loan payments, card payments | the month's outputs grouped by kind |
  | `lastSixMonths`: month, inputs, outputs | `GetCashFlowChartHandler`, monthly buckets |
  | `loans`: hasActive, monthly total, remaining, hasOverdue | `GetLoansDashboardHandler` |
  | `cardPayments`: month total, number of card payments, cards with installments | month card payments and installment cards |
  | `sharedCosts`: outstanding total, participants owing, hasOverdue | `GetSharedCostsHandler` |
  | `recurringBills`: active count, bills not issued this month | recurring bill list, month bills (`NotIssuedCount`) |

- `GET /api/cards/dashboard` stays as it is and is now used by the Cards screen (its name is kept so
  the API does not break; renaming can be a later refactor).
- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts`.

### Front-end changes (pages, components)
- **Theme** (`src/index.css`): section, meaning and accent tokens; `--font-heading` set to
  Fraunces; light grey page background, white cards with a soft shadow.
- **New building blocks** in `src/components/design/`: `StatCard`, `ChangePill`, `StatusChip`,
  `ProgressBar`, `SectionCard` (collapsible, coloured left edge and icon), `RingChart` and
  `BarChart` (Recharts wrappers with legend and exact-amount tooltip), `QuickActionButton` (icon,
  colour, Radix tooltip with 1 s delay), `sectionColors.ts` (one map used by menu, pages and charts).
- **Layout** (`components/layout/`): `AppLayout` with collapsible sidebar, `useMenuState` hook,
  coloured icons, `SystemStatusAlert` (menu and phone bar) replacing `HealthStatus`; `navigation.ts`
  gets a colour per item.
- **Dashboard** (`pages/DashboardPage.tsx`): quick actions, key figures, highlight card, Coming up,
  month result overview with 6-month chart, outputs-by-kind ring, small cards; `CardsWidget`,
  `LoansWidget` and `HealthStatus` removed from it; `useDashboard` hook in `src/api/dashboard.ts`;
  empty state (FR-11).
- **Cards** (`pages/CardsPage.tsx`): Accounts part from `CardsWidget`/`CardSummary` (moved, not
  rewritten), four key figures, current vs expected bar chart, then the existing installment list.
- **Month** (`MonthPage` and the five sections): header with month arrows, key figures, progress
  bars, `SectionCard`s with totals and "x of y paid", add buttons in section headers, short rows with
  `StatusChip` and one-tap check, details on expand, outputs ring.
- **Cash flow** (`CashFlowPage`, `CashFlowChart`, `MonthlyReportPage`): key figures, period buttons,
  filters popover with chips, stacked received/expected chart with result line, months list; report
  with change pills, two ring charts and collapsible groups.
- **Loans** (`LoansPage`, `LoanDetailsPage`): key figures, loan cards with progress, horizontal paid
  vs remaining chart; payment list with status chips.
- **Shared costs** (`SharedCostsPage`): key figures, participant cards, outstanding list with one-tap
  received, per-participant chart, participants management at the end.
- **Settings** (`SettingsPage`): grouped tiles with coloured icons and counts (from existing list
  hooks), new **Recurring bills** tile.
- **Forms**: Input, Bill, Expense (incl. cash withdrawal) and Purchase form pages honour
  `returnTo` (only same-app paths starting with `/` are accepted).
- **Dependency**: add `@fontsource-variable/fraunces`; `radix-ui` (already installed) provides the
  tooltip and popover.

### Database migrations
- None.

## Steps
1. Commit the approved requirements (done) and this plan.
2. Theme tokens, display font and the shared building blocks, with component tests.
3. Left menu: collapsible sidebar, coloured icons, system status alert (desktop and phone); remove
   the Dashboard status card.
4. Application and API: `GetDashboardHandler`, `GET /api/dashboard`, API tests, regenerated OpenAPI
   document and front-end types.
5. Dashboard page and quick actions with `returnTo` in the four form pages.
6. Cards screen Accounts part, key figures and chart.
7. Month screen.
8. Cash flow screen and monthly report.
9. Loans screen and loan details.
10. Shared costs screen.
11. Settings screen and the style pass over settings sub-screens and forms.
12. Documentation: `docs/INIT.md` (Dashboard endpoint, screen overview).
13. Manual check on desktop and phone width against the mock-up; leave all changes staged for
    review, then set this plan's status to `Done`.

## Tests
- Unit tests (Domain): none, the domain does not change.
- API / integration tests (real PostgreSQL in CI), which also cover `GetDashboardHandler` (the
  repository has no separate application test project):
  - `GET /api/dashboard` with an empty database: all zero, no next due, nothing coming up, six
    months, no section blocks.
  - With inputs, bills, an expense and a loan: totals, previous-month figures and the month result
    equal the monthly report's; outputs by kind add up to the outputs total; still to pay, next due
    and Coming up (bill before input on the same day); the 6-month chart; the Loans block.
  - Recurring bills block: active recurring bills and bills not issued this month.
- Front-end (Vitest + Testing Library):
  - Menu: toggle collapses and expands, `aria-expanded` changes, state survives a re-render from
    storage, storage errors are ignored, tooltips have the labels.
  - System status: nothing while pending or healthy; "API unreachable" on error; "Database
    unhealthy" when the database is not healthy; disappears when healthy again.
  - Dashboard: no balance texts and no cards list; four key figures with pills; highlight card in
    its three cases; Coming up empty state; small cards hidden without data; quick actions have
    accessible names, Add purchase hidden when no card can take a purchase, each links to its form
    with `returnTo=/`.
  - Forms return to `returnTo` after save and cancel, and ignore a `returnTo` that is not an app path.
  - Cards: accounts with current, expected, negative and missing starting balance, same filtering
    as the old widget; totals are sums.
  - Month: add buttons inside their sections; fully paid section starts collapsed; check button
    marks paid; details only when expanded.
  - Cash flow: filter chips reflect and clear filters; months list links to reports.
  - Loans, Shared costs, Settings: key figures, progress and counts from fixture data.
  - Existing tests updated where markup changed; every amount in fixtures still shown with the same
    value (NFR-4).
- Manual checks:
  - Desktop and phone width for every screen, collapsed and expanded menu, keyboard use of toggle
    and quick actions, contrast of text on coloured backgrounds (NFR-1), API stopped and database
    stopped for the status alert.

## Implementation notes

Changes to the approach found while building, before the requirements were merged:

- **Recurring bills small card (FR-10):** recurring bills have no amount of their own, so the card
  shows the active recurring bills and how many of this month's bills from them are not issued yet,
  instead of a monthly total. The requirement was changed on this branch to match.
- **Outputs by kind:** `ExpenseBook.Outputs()` is split into `BillOutputs()` and
  `ExpenseOutputs()`, so the Dashboard counts bills and expenses exactly as the monthly report does.
- **Coming up** reads this month's lists, and next month's when the 7 days reach into it.
- **Month progress (FR-15):** the "x of y" next to each bar is in amounts (e.g. "2.600,00 € of
  2.920,00 €"); the section headers show "x of y paid" as counts.
- **Cash flow (FR-16):** the period still opens on This month (input cash flow FR-12); the months
  list replaces the "Open monthly report" button.
- **Card status badges:** Active is green and Expiring soon amber, so the Cards screen and Settings
  follow FR-1.
- A Shared costs test typed a fixed due date that is now before today's default issue date; it uses
  a date far in the future instead.

## Open questions
- None.
