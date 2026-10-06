# Plan: Input cash flow

- **Branch:** `feat/input_cash_flow`
- **Status:** Draft
- **Date:** 2026-10-06
- **Requirements:** [input-cash-flow.md](../feature-requirements/input-cash-flow.md) (approved)

## Goal
The user records incoming payments (inputs) on the **Month** screen: payer, category, account,
amount, issue date, expected date and, once the money arrives, the received date. Regular inputs
are recorded once as recurring. Each account can get a starting balance and balance corrections.
The Dashboard shows the current and the expected end-of-month balance per account, a new
**Cash flow** dashboard charts inputs over time, and a monthly cash flow report sums them up.

## Scope
- In scope:
  - Domain: inputs, recurring inputs and their occurrences, payers, input categories, balance
    entries, and the balance calculation.
  - Database tables and one migration.
  - API endpoints for inputs, recurring inputs, payers, categories, balance entries, account
    balances, the chart and the report.
  - Month screen: month navigation, the Income part with list, totals and the add/edit form.
  - Settings: **Payers** and **Input categories** sections; balance entries on the account details.
  - Dashboard: balances in the Cards widget.
  - **Cash flow** dashboard with the chart, and the monthly report.
  - Tests at every layer.
- Out of scope:
  - Outputs, transfers between accounts, imports, closing a month (requirements, section 6).
  - **Access control** (NFR-3): there is still no authentication; the new endpoints go behind auth
    together with the card endpoints when authentication lands.
  - **Ongoing transactions:** inputs are not ongoing transactions in the sense of the card
    requirements, so `ICardActivityQuery` stays as it is and blocked or expired accounts do not
    come back on the Dashboard because of inputs.
  - Households: one implicit household, as for cards.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| A1, FR-5 | An input stores a `PayerId`. Saving an input with a typed name reuses the payer with the same name (case-insensitive) or creates it. Renaming a payer shows the new name on all its inputs. Removing a payer hides it from suggestions and Settings (`removed_on`); its inputs keep showing the name, and typing the name again brings it back. |
| A2, FR-4 | The account is a `Card`. A new input (or a changed account) needs an account whose status is not Blocked, Expired or Archived; any card type is allowed. Existing inputs keep their account when it is blocked or expires later. The form prefills the account only when exactly one non-archived card is primary. |
| A3, FR-6 | Categories are a table seeded with Salary, Salary add-ons, Agreed contributions, Other income. Names unique case-insensitive; rename always allowed; delete only when unused, otherwise **hide** (`hidden_on`). |
| A4 | Amount > 0, stored as `Money` (cents). |
| A6, A8 | Expected date and received date are each on or after the issue date. |
| A7, FR-3 | *Received* is not stored separately: an input is received when `ReceivedDate` is set. Ticking sets it to today; unticking clears it. Issue and expected dates never change on tick or untick. |
| Section 3 | The month of an input is the month of `ReceivedDate ?? ExpectedDate`. |
| FR-7 | Frequencies `Weekly`, `Monthly`, `Quarterly`, `Yearly`, optional end date (see "Recurring inputs"). |
| FR-9, FR-10 | Starting balance and correction are both **balance checkpoints** (see "Balance"). |
| FR-12 | Default period: current month by day; longer periods by month. |

### Recurring inputs (FR-7)
A recurring input is stored once as a template. Its occurrences are **calculated**, not stored, so
"occurrences appear in every month the user views" works without an end date and without a
background job.
- **Template** (`RecurringInput`): payer, category, account, amount, description, frequency, the
  issue and expected dates of the first occurrence (the anchors), optional `EndDate`.
- **Schedule:** occurrence *n* (from 0) has issue date = issue anchor + *n* steps and expected
  date = expected anchor + *n* steps. A step is 7 days, 1, 3 or 12 months. Months are always
  added to the anchor (not to the previous occurrence) and clamped to the last day of the month,
  so an anchor on the 31st gives 28/29 Feb and then 31 Mar again. The occurrence is identified by
  its scheduled expected date (`OccurrenceDate`). Occurrences after `EndDate` do not exist.
- **Stored occurrences:** an occurrence becomes a normal `Input` row (with `RecurringInputId` and
  `OccurrenceDate`, unique together) the first time the user changes it, ticks it as received, or
  creates the template already received. Deleting a calculated occurrence stores a
  `SkippedOccurrence` (`recurring_input_id`, `occurrence_date`). Deleting a stored one removes the
  row and stores the skip, so it is not calculated again.
- **Month view:** stored inputs of the month, plus calculated occurrences of the month that are
  neither stored nor skipped. Calculated ones carry `recurringInputId` + `occurrenceDate` instead
  of an input id.
- **Changing the template** applies to calculated occurrences automatically. Stored occurrences
  that are not received and whose expected date is today or later are updated to the new amount,
  account, category and dates as well; the edit dialog says so. Received occurrences never change.
- **Stopping** sets `EndDate` to today. Stored, not received occurrences after today are deleted;
  received ones stay.
- **Changing the frequency or anchors** keeps already stored occurrences (received or individually
  changed ones before today) and recalculates the rest from the new schedule.

### Balance (FR-9, FR-10, FR-11)
- `BalanceEntry`: account, kind (`Starting` or `Correction`), date, **balance** (the actual
  balance entered by the user, may be negative), optional note. Both kinds mean "at the end of this
  date the account had this balance"; a starting balance is simply the first one.
- The **balance on a date D** = the latest checkpoint on or before D, plus received inputs with a
  received date after that checkpoint's date and on or before D. Without any checkpoint it starts
  from zero and the account is flagged `hasStartingBalance = false` (FR-11 hint).
- A correction therefore always makes the balance equal to the entered value on its date
  (acceptance criterion 9), even if older inputs are edited later. Its **difference** is shown,
  calculated as entered balance minus the balance the day before plus inputs received on that date
  (what the app would have shown without it).
- **Current balance** = balance on today. **Expected balance** = current balance + all inputs on
  that account that are not received and whose expected date is on or before the end of the
  current month, stored or calculated (overdue expected inputs included).
- Balance entries are never part of input totals, the chart or the report totals (FR-9); the
  report lists them separately with their difference.
- Outputs will later be subtracted in the same calculation; the checkpoint model does not change.

### Domain / data model changes (`Monthwise.Domain/CashFlow`)
- Value types and enums: `RecurrenceFrequency`, `BalanceEntryKind`, and a `RecurrenceSchedule`
  (anchors, frequency, end date) with `OccurrencesBetween(DateOnly from, DateOnly to)`.
- Entities (Guid v7 ids, `CreatedAt`, `UpdatedAt`):
  - `Payer`: `Name` (max 100), `RemovedOn?`; `Rename`, `Remove`, `Restore`.
  - `InputCategory`: `Name` (max 50), `IsPredefined`, `HiddenOn?`; `Rename`, `Hide`, `Show`.
  - `Input`: `PayerId`, `CategoryId`, `AccountId`, `Amount`, `IssueDate`, `ExpectedDate`,
    `ReceivedDate?`, `Description?` (max 200), `RecurringInputId?`, `OccurrenceDate?`;
    `Create`, `Update`, `MarkReceived(today)`, `MarkExpected()`, and `Month`.
  - `RecurringInput`: template fields and `Schedule`; `Create`, `Update`, `Stop(today)`,
    `Occurrence(DateOnly occurrenceDate)` (builds the calculated input values).
  - `SkippedOccurrence`.
  - `BalanceEntry`: `AccountId`, `Kind`, `Date`, `Balance` (`Money`), `Note?`.
- `AccountBalance` calculator: pure function over checkpoints and inputs that returns the balance
  on a date, the current and expected balance, and each correction's difference.
- Rules are returned as `Result` errors with field keys, like the card rules.

### Database migrations
- Migration `AddInputCashFlow` creates (snake_case):
  - `payer` (`name`, `removed_on`), unique index on `lower(name)`.
  - `input_category` (`name`, `is_predefined`, `hidden_on`), unique index on `lower(name)`,
    seeded with the four default categories.
  - `recurring_input` (`payer_id`, `category_id`, `account_id` → `card`, `amount_cents`,
    `description`, `frequency` text, `issue_anchor`, `expected_anchor`, `end_date`).
  - `cash_flow_input` (`payer_id`, `category_id`, `account_id` → `card`, `amount_cents`,
    `issue_date`, `expected_date`, `received_date`, `description`, `recurring_input_id`,
    `occurrence_date`), unique index on (`recurring_input_id`, `occurrence_date`), and indexes on
    `received_date` and `expected_date` for month queries.
  - `skipped_occurrence` (`recurring_input_id`, `occurrence_date`), primary key on both.
  - `balance_entry` (`account_id` → `card`, `kind`, `date`, `balance_cents`, `note`).
- Check constraints: `amount_cents > 0`; `expected_date >= issue_date`;
  `received_date is null or received_date >= issue_date`; `expected_anchor >= issue_anchor`;
  `(recurring_input_id is null) = (occurrence_date is null)`.
- Foreign keys restrict deletes (payers and used categories are never deleted, cards are only
  archived).
- Update `docs/INIT.md` section 3: inputs are `cash_flow_input` and `recurring_input` (instead of
  `transaction`/`recurring_item` for income), balances are `balance_entry` per card, and add
  `payer` and `input_category`.

### API changes (endpoints, DTOs)
New `Api/Endpoints/CashFlowEndpoints.cs` and `CashFlowSettingsEndpoints.cs`, one handler per use
case in `Monthwise.Application/CashFlow`, FluentValidation validators, same error mapping as cards.

| Method and path | Purpose | Notes |
|---|---|---|
| `GET /api/inputs?month=yyyy-MM` | Month list and totals (FR-1) | stored and calculated inputs; totals expected, received, all |
| `GET /api/inputs/{id}` | One stored input | |
| `POST /api/inputs` | Add (FR-2) | body includes `payerName` and optional `recurrence { frequency, endDate? }`; with a recurrence it creates the template |
| `PUT /api/inputs/{id}` | Edit (FR-8) | |
| `DELETE /api/inputs/{id}` | Delete (FR-8) | a recurring occurrence is also skipped |
| `POST /api/inputs/{id}/received`, `DELETE /api/inputs/{id}/received` | Tick / untick (FR-3) | |
| `PUT /api/recurring-inputs/{id}/occurrences/{date}` | Change one calculated occurrence (FR-7) | stores it |
| `POST /api/recurring-inputs/{id}/occurrences/{date}/received` | Tick a calculated occurrence | stores it as received |
| `DELETE /api/recurring-inputs/{id}/occurrences/{date}` | Delete one calculated occurrence | stores a skip |
| `GET /api/recurring-inputs/{id}`, `PUT /api/recurring-inputs/{id}` | View / change the template (FR-7) | |
| `POST /api/recurring-inputs/{id}/stop` | Stop (FR-7) | |
| `GET /api/payers?search=` , `PUT /api/payers/{id}`, `DELETE /api/payers/{id}` | Autocomplete, rename, remove (FR-5) | `409` on a duplicate name |
| `GET /api/input-categories?includeHidden=`, `POST`, `PUT /{id}`, `POST /{id}/hide`, `POST /{id}/show`, `DELETE /{id}` | Categories (FR-6) | delete `409` when used |
| `GET /api/cards/{id}/balance-entries`, `POST`, `PUT /{entryId}`, `DELETE /{entryId}` | Starting balance and corrections (FR-9) | response includes each correction's difference |
| `GET /api/cards/dashboard` (extended) | Balances (FR-11) | each card gets `currentBalanceCents`, `expectedBalanceCents`, `hasStartingBalance` |
| `GET /api/cash-flow/chart?from&to&granularity=Day\|Month&accountId&payerId&categoryId` | Chart (FR-12) | received and expected sums per bucket |
| `GET /api/cash-flow/report?month=yyyy-MM` | Monthly report (FR-13) | groups by category, account, payer; balance entries; previous month total and difference |

- List items (`MonthInputResponse`) carry `id?` (stored) or `recurringInputId` + `occurrenceDate`
  (calculated), plus payer, category and account names, dates, amount, `isReceived`,
  `isRecurring`.
- The input form's account list reuses `GET /api/cards` filtered on the client to usable statuses;
  the default comes from the primary flag (FR-4).
- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (ADR 0003).

### Front-end changes (pages, components)
- Routes and navigation:
  - `/month` shows the current month, `/month/:yearMonth` another one, with previous/next buttons.
  - `/month/:yearMonth/inputs/new`, `/inputs/:id/edit`, and
    `/recurring-inputs/:id/occurrences/:date/edit` and `/recurring-inputs/:id/edit` for the forms
    (pages, as for cards, so they work well on a phone).
  - New nav item **Cash flow** (`/cash-flow`) and the report at `/cash-flow/:yearMonth`.
  - Settings sections **Payers** (`/settings/payers`) and **Input categories**
    (`/settings/input-categories`).
- `src/api/cashFlow.ts`: TanStack Query hooks; mutations invalidate the inputs, dashboard cards,
  chart and report queries (acceptance criterion 8).
- Components in `src/components/cashflow/`:
  - `IncomeSection` on the Month page: list, totals, empty state with **Add input**; expected rows
    dimmed; a Received check box per row; recurring marker; both dates shown when received on a
    different day than expected (FR-3).
  - `InputForm`: payer autocomplete (`PayerCombobox`, free text allowed), account select with the
    "set up an account first" state, category select, amount (`parseEuroInput`), three dates,
    Received check box, description, Recurring switch with frequency and optional end date.
  - `RecurringInputForm` for the template, with the warning about not-received future occurrences.
  - `DeleteInputDialog` naming payer, amount and date.
  - `BalanceEntries` on the card details page: list with differences, add/edit/delete starting
    balance and corrections, and the hint when no starting balance exists.
  - `CashFlowChart` (Recharts, stacked received and expected bars, expected in a lighter tone),
    with period and filter controls; clicking a month bar (or "Open report" in the day view)
    opens the report.
  - `MonthlyReport`: totals, groups, balance entries, previous-month comparison, month navigation.
  - Labels and messages in `components/cashflow/texts.ts` (NFR-5).
- `CardSummary` in the Dashboard widget shows the current balance, the expected balance in a muted
  colour, a negative balance highlighted, and the starting balance hint (FR-11).
- New dependency **recharts** (the chart library chosen in `docs/INIT.md`). New shadcn component:
  `checkbox`; the payer autocomplete is a small own component on `input` with a suggestion list.

## Steps
1. Commit the approved requirements (done) and this plan.
2. Domain: entities, `RecurrenceSchedule`, `AccountBalance`, rules, with unit tests.
3. Infrastructure: EF configurations, the `AddInputCashFlow` migration with seed data, indexes and
   checks, repositories and the month/chart/report queries.
4. Application: DTOs, validators, handlers, the balance part of the dashboard handler.
5. Api: endpoints, error mapping, OpenAPI metadata; regenerate the OpenAPI document; API tests.
6. Front-end foundations: recharts, checkbox, regenerated types, `cashFlow.ts` hooks.
7. Settings: Payers and Input categories sections; balance entries on card details.
8. Month screen: navigation, Income section, input and recurring forms, dialogs.
9. Dashboard balances, Cash flow dashboard and monthly report.
10. Documentation: `docs/INIT.md` data model, README feature list if needed.
11. Manual check on desktop and phone width, then set this plan's status to `Done`.

## Tests
- Unit tests (Domain):
  - input rules: each invalid case (amount, dates, missing fields), month by received or expected
    date, tick sets only the received date, untick clears it;
  - `RecurrenceSchedule`: each frequency, the 31st and 29 February clamping, end date, occurrences
    in a month for weekly (4 or 5);
  - `AccountBalance`: no checkpoint, starting balance, later correction, inputs on the checkpoint
    date, editing an input before a correction, expected balance with overdue and future inputs,
    negative balances, correction difference;
  - payer and category rename/remove/hide rules.
- Application tests: payer reuse by name (case-insensitive) and restore after removal; account
  usability check; template change updates only not-received future stored occurrences; stop
  deletes not-received occurrences after today; delete of a calculated occurrence is skipped.
- API / integration tests (real PostgreSQL in CI):
  - add, edit, tick, untick, delete inputs and the month totals;
  - recurring: create, month view with calculated occurrences, change one occurrence, skip, change
    the template, stop;
  - categories: seeded list, duplicate name `409`, delete used category `409`, hide;
  - balance entries and the dashboard balances;
  - chart buckets by day and by month with filters; report groups, totals and previous-month
    difference;
  - database checks reject a zero amount and a received date before the issue date.
- Front-end (Vitest + Testing Library):
  - `InputForm`: account prefill with one primary, empty with several, "set up an account" state;
    payer suggestions and free text; Received check box sets the received date to today; validation
    errors from the API;
  - `IncomeSection`: dimmed expected rows, totals, empty state, ticking a calculated occurrence;
  - `CardSummary` balances and the starting balance hint;
  - `MonthlyReport` totals and `CashFlowChart` renders received and expected series.
- Manual checks: record a salary as monthly recurring, tick it as received a day late, add a
  one-off input on a second account, add a starting balance and a correction, check the Dashboard,
  the chart (current month and 12 months) and the report on desktop and phone width.

## Open questions
- None.
