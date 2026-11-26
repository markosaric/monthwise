# Plan: Money lending

- **Branch:** `feat/money-lending`
- **Status:** Draft
- **Date:** 2026-11-26
- **Requirements:** [money-lending.md](../feature-requirements/money-lending.md) (approved)

## Goal
The user records money lent to a borrower (*posudba*): the amount leaves an account on the lend
date, the borrower returns it in one or more repayments to any account, and until it is returned
the outstanding amount is an expected input on its expected return date. A new **Lending** screen
lists lendings and the totals per borrower. The Month screen, Cash flow chart, monthly report, account balances
and the Dashboard show lending, in English and Croatian.

## Scope
- In scope:
  - Domain: lendings with their repayments (borrowers are payers), the outstanding amount and the state (Open,
    Overdue, Returned).
  - Database: one migration adding `lending` and `lending_repayment`.
  - API: borrower totals, lendings and repayments, the month's lending list,
    and lending in balances, the chart, the report and the Dashboard.
  - Front-end: the Lending screen, lending form, repayment dialog, the Month screen's Lending
    section, the chart and report groups, the Dashboard card, all in English and Croatian.
  - User manual (English and Croatian), and tests at every layer.
- Out of scope:
  - Everything in section 6 of the requirements: interest or repaying more than was lent,
    borrowing from a person, a repayment schedule, lending in cash or with a credit card,
    writing off, reminders, currencies.
  - **Access control** (NFR-3): there is still no authentication, as for the earlier features.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| L1, FR-6 | A borrower is a `Payer` (name 1 to 100, unique ignoring upper/lower case). The form sends `borrowerName`; the handler finds the payer with that name the same way inputs do (`InputDetailsResolver`: restoring a removed payer, otherwise creating one) and stores `payer_id` on the lending. Suggestions use the existing `GET /api/payers?search=`. Renaming and removing stay in Settings, Payers; a removed payer keeps its lendings and is shown on them, as for inputs. |
| L2, R1 | Amounts are `Money` (integer cents), greater than zero. The sum of a lending's repayments is never more than its amount: adding or changing a repayment above the outstanding amount gets a field error on `amountCents`. |
| L3, L5, R3 | Lend date ≤ expected return date. Every repayment's received date ≥ the lend date. Changing the lend date to after an existing repayment is refused with a field error on `lendDate`. |
| L4, R2 | The account must be an account (not a Credit, Charge or revolving card) that `CanBeUsedOn(today)` and is not archived, checked when the account is chosen or changed. A lending or repayment whose saved account later becomes blocked, expired or archived keeps it. R2 is prefilled in the front-end with the lending's *From account*. |
| FR-3 | Editing the amount below the sum of repayments: field error on `amountCents`. Deleting a lending deletes its repayments (cascade); the confirmation names borrower, amount, lend date and the number of repayments. |
| FR-4, FR-5 | `Outstanding = Amount − Σ repayments`. State: **Returned** when outstanding is 0, **Overdue** when outstanding > 0 and expected return date < today (household time zone), otherwise **Open**. Calculated on read, never stored. |
| FR-7 | The lent amount is an output of *From account* dated the lend date, always "paid" (like an expense). It is not an expense, bill or cash withdrawal and has no expense category. |
| FR-8 | Each repayment is a received input of *To account* on its received date. When outstanding > 0, one **expected input** of the outstanding amount on *From account*, dated the expected return date. Like other overdue items, an overdue expected return is counted in the expected end-of-month balance, since expected movements due by a date include overdue ones. Repayments are not `cash_flow_input` rows and have no input category; their payer is the lending's borrower. |
| FR-9 | `GET /api/lendings/month?month=yyyy-MM`: lent in the month (by lend date), repayments received in the month (by received date), expected returns due in the month (by expected return date, overdue ones flagged), with totals lent, returned, expected back. |
| FR-10 | Chart and report get a **Lending** group: lent amounts as paid outputs, repayments as received inputs, expected returns as expected inputs. The month result already is inputs − outputs, so lending is in it. A new `borrowerId` chart filter (a payer id); the existing payer filter applies to inputs only, as today; when it is set, the other groups are left out (as the existing filters do for each other). |
| FR-11 | Dashboard `lending` block (null when nothing is outstanding): outstanding total, overdue total, count of open lendings, `hasOverdue`. |
| FR-12 | The export already takes every household table (ADR 0007), so the new tables are exported and imported without extra code. The import summary gets a **Lendings** count. |

### Lending ledger
- New `Monthwise.Application/Lending/LendingLedger.cs`, like `ExpenseLedger`: loads lendings with
  their repayments once and returns:
  - `Outputs()`: one `LendingOutput(Date, Amount, AccountId, LendingId, PayerId)` per lending;
  - `Repayments()`: one `LendingInput(Date, Amount, AccountId, LendingId, PayerId, IsReceived: true)` per repayment;
  - `ExpectedReturns(today)`: one `LendingInput(…, IsReceived: false)` per lending with outstanding > 0;
  - `…Between(from, to)` variants for the chart, report and Month screen.
- `AccountBalanceQuery.CalculatorsAsync` adds outputs (negative) and repayments (positive) up to
  the date; `ExpectedMovementsAsync` adds expected returns due by the date (positive, on *From
  account*). `OverallBalanceQuery` therefore includes lending without its own change.
- `GetCashFlowChartHandler` and `GetMonthlyReportHandler` add the ledger's entries to their inputs
  and outputs; the report adds a `lending` block (lent, returned, expected back, by borrower).
- `GetDashboardHandler` gets the inputs, outputs and month result from the report as today, so they
  include lending; `OutputsByKind` gets `LendingCents`, and the new `lending` small card.

### Domain / data model changes
- `Monthwise.Domain/Lending`:
  - `Lending`: `Create`, `Update`, `AddRepayment`, `UpdateRepayment`, `RemoveRepayment`,
    `Outstanding`, `StateOn(today)`; owns its `LendingRepayment` list so the rules above are checked
    in one place.
  - `LendingRepayment` (amount, to account, received date, note).
  - `LendingState` enum (`Open`, `Overdue`, `Returned`) and `LendingErrors` with field keys.

### Database migrations
- Migration `AddLending` (snake_case):
  - `lending` (`id`, `payer_id` → `payer` restrict, `amount_cents`, `lend_date`,
    `from_account_id` → `card` restrict, `expected_return_date`, `description`, `created_at`,
    `updated_at`); checks `amount_cents > 0`, `expected_return_date >= lend_date`.
  - `lending_repayment` (`id`, `lending_id` → `lending` cascade, `amount_cents`, `to_account_id` →
    `card` restrict, `received_date`, `note`, `created_at`, `updated_at`); check `amount_cents > 0`.
  - The "sum of repayments ≤ amount" and "received date ≥ lend date" rules are checked in the
    domain (they span rows); the handlers save a lending and its repayments in one transaction.

### API changes (endpoints, DTOs)
New `Api/Endpoints/LendingEndpoints.cs` and `Monthwise.Application/Lending` handlers, with the same
error mapping as the other endpoints.

| Method and path | Purpose | Notes |
|---|---|---|
| `GET /api/lendings/borrowers` | Per borrower: lent, returned, outstanding (FR-6) | |
| `GET /api/lendings?state=open\|returned` | Lending screen (FR-1) | rows with outstanding and state; totals outstanding and overdue; default order of FR-1 |
| `GET /api/lendings/{id}` | One lending with its repayments | |
| `POST /api/lendings`, `PUT /api/lendings/{id}`, `DELETE /api/lendings/{id}` | Add, edit, delete (FR-2, FR-3) | `LendingRequest`: `borrowerName`, `amountCents`, `lendDate`, `fromAccountId`, `expectedReturnDate`, `description` |
| `POST /api/lendings/{id}/repayments`, `PUT …/repayments/{repaymentId}`, `DELETE …/repayments/{repaymentId}` | Record, edit, delete a repayment (FR-4) | `RepaymentRequest`: `amountCents`, `toAccountId`, `receivedDate`, `note` |
| `GET /api/lendings/month?month=yyyy-MM` | Month screen Lending list (FR-9) | three groups and totals |
| `GET /api/cash-flow/chart` (extended) | Lending in the chart (FR-10) | `borrowerId` filter (a payer id) |
| `GET /api/cash-flow/report` (extended) | Lending group in the report (FR-10) | `lending` block |
| `GET /api/dashboard` (extended) | Lending card and outputs by kind (FR-11) | `lending`, `outputsByKind.lendingCents` |
| Export and import summary (extended `DataSummaryResponse`) | Lendings count (FR-12) | |

- Server texts (validation messages) in English and Croatian in `ServerTexts`.
- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (ADR 0003).

### Front-end changes (pages, components)
- Navigation: a **Lending** (*Posudbe*) item after Shared costs, with a hand-coins icon and a new
  section colour `--section-lending` in light and dark themes.
- Routes: `/lending` (list with Open/Returned filter, totals, per-borrower totals),
  `/lending/new`, `/lending/:id/edit`.
- `src/api/lending.ts`: queries and mutations; mutations invalidate lendings, the month list,
  balances, overall balance, chart, report and Dashboard.
- `src/components/lending/`:
  - `LendingForm` (borrower with the payer suggestions, amount with the euro amount input, dates with the date
    input, account select, description) and `lendingFormModel.ts`;
  - `RepaymentDialog` (amount prefilled with outstanding, account prefilled with *From account*,
    received date prefilled with today);
  - `LendingRow` with state chip (Overdue highlighted), `LendingSection` for the Month screen,
    `LendingCard` for the Dashboard;
  - `texts.ts` and `texts.hr.ts`.
- Cash flow chart: a borrower filter; report: a Lending group.
- User manual: a short Lending section in `docs/user-manual/index.html` and
  `docs/user-manual/hr/index.html`.

## Steps
1. Domain: `Lending`, `LendingRepayment`, state and errors, with domain tests.
2. Infrastructure: EF configurations, repositories and the `AddLending` migration.
3. Application and API: lending handlers, borrower totals and endpoints, the month list.
4. `LendingLedger`, and lending in balances, chart, report, Dashboard and the data summary.
5. Regenerate OpenAPI and the front-end client.
6. Front-end: navigation, Lending screen, form, repayment dialog, Month section, chart filter,
   report group, Dashboard card, English and Croatian texts.
7. User manual in English and Croatian.
8. Run all back-end and front-end tests, lint and type checks; leave the changes staged for review.

## Tests
- Unit tests (domain): amount and date rules; outstanding after one and several repayments;
  repayment above outstanding refused; lend date after a repayment refused; amount below repaid
  refused; state Open / Overdue / Returned around the expected return date.
- API / integration tests (PostgreSQL, `MONTHWISE_TEST_DB`): create, edit, delete with repayments;
  validation errors with field keys; credit card or blocked account refused; borrower reuses an existing
  payer ignoring case and restores a removed one; renaming a payer shows on its lendings; account balance after lending and repayments; expected end-of-month balance with
  an expected return in the current month and an overdue one; chart and report groups and the
  borrower filter; month result includes lending; Dashboard card null when nothing outstanding;
  month list groups; export and import round trip brings back lendings and repayments;
  Croatian error messages.
- Front-end tests: form validation and prefills, repayment prefilled with outstanding, Returned
  filter, overdue highlight, Croatian texts complete (existing completeness check).
- Manual checks: phone and desktop layout of the Lending screen, form and Month section; Croatian
  texts not cut off.

## Open questions
- None.
