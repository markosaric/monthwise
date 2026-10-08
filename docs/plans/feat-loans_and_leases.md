# Plan: Loans and leases

- **Branch:** `feat/loans_and_leases`
- **Status:** Draft
- **Date:** 2026-10-08
- **Requirements:** [loans-and-leases.md](../feature-requirements/loans-and-leases.md) (approved), and
  [input-cash-flow.md, Appendix 1](../feature-requirements/input-cash-flow.md#appendix-1-loan-payments-in-the-account-balance-chart-and-report)

## Goal
The user records bank loans and leases once on the **Loans** screen, with the monthly payment, the
payment day and either the number of payments or the end month. Every payment then appears in its
month on the **Month** screen. A standing order is paid automatically at the end of its due date,
and a bill is paid when the user ticks it. Payment changes set a new amount or payment day for the
following payments. Loan payments lower the account balances and appear as outputs on the Cash
flow dashboard and in the monthly report. The Dashboard gets a **Loans** widget.

## Requirement changes
- [input-cash-flow.md, Appendix 1](../feature-requirements/input-cash-flow.md#appendix-1-loan-payments-in-the-account-balance-chart-and-report)
  (2026-10-08, changes input FR-10 to FR-13):
  - **Balance (FR-10, FR-11):** the current balance subtracts paid loan payments dated after the
    latest checkpoint. The expected balance also subtracts expected loan payments on that account
    due by the end of the month. An expected bill payment without an account is not counted.
  - **Chart (FR-12):** shows outputs (loan payments), paid and expected, and can be filtered by
    loan category.
  - **Report (FR-13):** lists the month's loan payments grouped by category, account and loan,
    with totals, and shows the month result (inputs minus outputs). The previous-month comparison
    covers outputs and the month result too.
  - Implementation: see "Balance", "Chart and report" and the API table below.

## Scope
- In scope:
  - Domain: loans, the payment schedule, payment changes, stored payments, loan categories and
    subcategories, and the balance calculation with outputs.
  - Database tables and one migration with the default categories.
  - API endpoints for loans, payments, payment changes, categories, the Loans widget, and the
    extended balances, chart and report.
  - **Loans** screen (list, details, add/edit form, payment changes, close, delete).
  - **Month** screen: a new **Expenses** part with the month's loan payments.
  - Settings: **Loan categories** section.
  - Dashboard: **Loans** widget, and loan payments in the account balances.
  - Cash flow dashboard and monthly report with outputs.
  - Tests at every layer.
- Out of scope:
  - Everything in section 6 of the requirements: other outputs, the interest rate and
    amortization, sharing, insurance, partial early repayments, foreign currencies, imports, other
    reports.
  - **Access control** (NFR-3): there is still no authentication. The new endpoints go behind auth
    together with the card and cash flow endpoints when authentication lands.
  - Households: one implicit household, as for cards and inputs.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| A1 | Name max 50, unique case-insensitive among loans that are not closed (partial unique index on `lower(name) where closed_on is null`). |
| A3, A4 | `SubcategoryId` must belong to `CategoryId`. `LenderId` is a `Bank` from the card settings; the form's **Add new…** creates a bank the same way the card form does. |
| A5, A6, FR-3 | `PaymentMethod` is `StandingOrder` or `Bill`. A standing order needs `AccountId`: a card that is primary and usable (`CanBeUsedOn(today)`) when the loan is saved. A bill has no account on the loan. |
| A7 | `StartMonth` is a `YearMonth`. |
| A8, FR-4 | `PaymentDay` is 1 to 31. The due date is the payment day clamped to the end of the month. For a **bill**, a due date on Saturday or Sunday moves to the following Monday, unless that Monday is in the next month; then it stays on its day. A **standing order** keeps the day. A payment's due date is therefore always in its own month. |
| A9 | Monthly payment > 0, stored as `Money` (cents). |
| A10, A11, FR-2 | The loan stores `StartMonth` and `PaymentCount`. `EndMonth = StartMonth + PaymentCount - 1`. The API accepts either `paymentCount` or `endMonth` and calculates the other. The form does the same calculation while the user types. |
| A12, FR-6 | `ResidualValue?` (> 0) is allowed only when the category has `AllowsResidualValue`. |
| A13 | Description max 200. |
| Section 3 | The month of a payment is the month of `PaidDate ?? DueDate`. |
| FR-5 | A standing order payment counts as paid when today is after its due date, with paid date = due date and account = the loan's account, unless the user changed it. A bill payment is paid only when the user ticks it. |
| FR-7 | A payment change has a `FromMonth` and a new monthly payment, a new payment day, or both, plus a note. `FromMonth` must be after the month of the latest paid payment. |
| FR-9 | Remaining amount = sum of the expected payments, including an unpaid residual value. |
| FR-10 | A loan is **Paid off** when all its payments, residual value included, are paid. Closing sets `ClosedOn = today`. Delete is allowed only when no payment is paid. |

### Payment schedule
As with recurring inputs, payments are **calculated** from the loan and stored only when the user
changes them. This keeps edits and payment changes simple and needs no background job.
- **Regular payment** for month *m*, from `StartMonth` to `EndMonth`:
  - the amount and payment day come from the newest payment change with `FromMonth <= m`, else
    from the loan;
  - bill date = first day of *m*, and the due date follows the rule in the table above.
- **Residual payment** (when `ResidualValue` is set): one payment in `EndMonth + 1`, with bill date
  on the 1st and due on the payment day in that month (weekend rule for bills). It is always a
  bill, whatever the loan's payment method.
- A payment is identified by (`LoanId`, `Month`, `Kind`), where `Kind` is `Regular` or `Residual`.
- **Stored payment** (`LoanPayment`): created the first time the user changes the amount or due
  date, ticks or unticks a bill, or corrects or unticks a standing order payment. It holds the
  overrides (`Amount?`, `DueDate?`) and an explicit paid state (`PaidDate?`, `PaidAccountId?`,
  `IsPaid`). The explicit state wins over the automatic standing-order rule, so an unticked
  standing order payment stays expected.
- **Month view:** for each loan, the payments whose `PaidDate ?? DueDate` falls in the month. This
  combines calculated payments with stored ones, in the same way as `InputLedger` does for inputs.
- **Edit (FR-10):** changes to amount, payment day, start month or count apply to payments that are
  not paid. Stored payments that are paid stay. Stored payments that are not paid keep only their
  user overrides.
- **Changing the payment method** applies to expected payments only. A paid standing order payment
  that was paid automatically is stored before the change, so it stays paid.
- **Close:** calculated payments stop at `ClosedOn`, and stored payments that are not paid are
  deleted.
- **Payment change and hand-changed payments (FR-7):** saving a change that covers stored payments
  with an override sends `keepChangedPayments`. The form asks the user first when the loan details
  list such payments. When the flag is false, the overrides of those payments are cleared.

### Account no longer usable (FR-3)
- A loan **needs a new account** when it is a standing order and its account is not primary or not
  usable today. This is calculated, not stored, so unblocking or re-marking the account primary
  clears it.
- Its expected payments carry `needsAccount = true`. They are still listed, and they are not
  counted in any balance until the user chooses an account or switches to bill.
- A standing-order loan with expected payments is an **ongoing transaction** of its account (FR-3).
  `ICardActivityQuery` (Infrastructure `CardActivityQuery`) also returns the accounts of such loans,
  so a blocked or expired account stays on the Dashboard Cards widget and in the default Settings
  list while the loan needs a new account. Archived accounts are never shown, as before.

### Balance (input Appendix 1)
- `AccountBalance` takes signed movements: received inputs are positive and paid loan payments
  negative. `ReceivedAmount` becomes `BalanceMovement(Date, Money)`. Checkpoints keep working the
  same way, so a correction still makes the balance equal to the entered value.
- **Expected balance** = current balance + expected inputs − expected loan payments on that
  account due by the end of the current month. Overdue payments are included, and bill payments
  without an account are not counted.
- `AccountBalanceQuery` gets the paid and expected loan payments from a new `LoanLedger` (the loan
  equivalent of `InputLedger`).

### Chart and report (FR-12, input Appendix 1)
- `ChartBucketResponse` gets `OutputPaidCents` and `OutputExpectedCents`. The chart filter gets
  `loanCategoryId`, which applies to outputs only. `payerId` and `categoryId` stay inputs only.
- `MonthlyReportResponse` gets `Outputs` (totals, `ByCategory`, `ByAccount`, `ByLoan`) and
  `MonthResultCents` (all inputs − all outputs). Its previous-month part gets the previous outputs
  and month result. Bills without an account are grouped under "No account yet".

### Domain / data model changes (`Monthwise.Domain/Loans`)
- Enums and value types: `PaymentMethod`, `LoanPaymentKind`, `LoanStatus` (`Active`, `PaidOff`,
  `Closed`), and `DueDateRule` (clamp and weekend move).
- Entities (Guid v7 ids, `CreatedAt`, `UpdatedAt`):
  - `LoanCategory`: `Name` (max 50), `AllowsResidualValue`, `IsPredefined`, `HiddenOn?`;
    `Rename`, `SetAllowsResidualValue`, `Hide`, `Show`.
  - `LoanSubcategory`: `CategoryId`, `Name` (max 50), `IsPredefined`, `HiddenOn?`.
  - `Loan`: the attributes A1 to A13, `ClosedOn?`, and the list of `PaymentChange`s; `Create`,
    `Update`, `AddChange`, `UpdateChange`, `RemoveChange`, `Close`, `Schedule`.
  - `PaymentChange`: `FromMonth`, `MonthlyPayment?`, `PaymentDay?` (at least one), `Note?`
    (max 200).
  - `LoanPayment` (stored payment) as described above.
- `LoanSchedule`: a pure calculator over the loan, its changes and its stored payments. It returns
  the payments between two dates, the latest paid month, the remaining amount and the status for a
  given `today`.
- Rules are returned as `Result` errors with field keys, like the card and input rules.

### Database migrations
- Migration `AddLoans` creates (snake_case):
  - `loan_category` (`name`, `allows_residual_value`, `is_predefined`, `hidden_on`), unique index
    on `lower(name)`, seeded with **Bank loan** (no residual value) and **Lease** (residual value).
  - `loan_subcategory` (`category_id`, `name`, `is_predefined`, `hidden_on`), unique index on
    (`category_id`, `lower(name)`), seeded with Mortgage, Cash loan, Car loan, Other loan / Car
    lease, Equipment lease, Other lease.
  - `loan` (`name`, `category_id`, `subcategory_id`, `lender_id` → `bank`, `payment_method`,
    `account_id` → `card`, `start_month`, `payment_day`, `monthly_payment_cents`,
    `payment_count`, `residual_value_cents`, `description`, `closed_on`), partial unique index on
    `lower(name) where closed_on is null`.
  - `loan_payment_change` (`loan_id`, `from_month`, `monthly_payment_cents`, `payment_day`,
    `note`), unique on (`loan_id`, `from_month`).
  - `loan_payment` (`loan_id`, `month`, `kind`, `amount_cents`, `due_date`, `is_paid`,
    `paid_date`, `paid_account_id` → `card`), unique on (`loan_id`, `month`, `kind`), and indexes on
    `paid_date` and `due_date`.
- Check constraints: amounts > 0; `payment_day between 1 and 31`; `payment_count >= 1`;
  `(payment_method = 'StandingOrder') = (account_id is not null)`; a payment change has
  `monthly_payment_cents` or `payment_day`; `is_paid = (paid_date is not null)`.
- Foreign keys restrict deletes. Payment changes and stored payments are deleted together with a
  deleted loan.
- Update `docs/INIT.md` section 3: replace the draft `loan` line with the tables above.

### API changes (endpoints, DTOs)
New `Api/Endpoints/LoanEndpoints.cs` and `Monthwise.Application/Loans` handlers, FluentValidation
validators and the same error mapping as cards and inputs.

| Method and path | Purpose | Notes |
|---|---|---|
| `GET /api/loans?show=Active\|Closed` | Loans list (FR-1) | current payment, next payment date, payments left of total, end month, remaining amount, residual value, `needsAccount` |
| `GET /api/loans/{id}` | Loan details (FR-9) | paid and remaining in number and amount, payment changes with the original values, payments with an override |
| `POST /api/loans`, `PUT /api/loans/{id}` | Add, edit (FR-2, FR-10) | either `paymentCount` or `endMonth`; `409` on a duplicate name |
| `POST /api/loans/{id}/close`, `DELETE /api/loans/{id}` | Close, delete (FR-10) | delete is `409` when a payment is paid |
| `POST /api/loans/{id}/payment-changes`, `PUT …/{changeId}`, `DELETE …/{changeId}` | Payment changes (FR-7) | `keepChangedPayments` flag; edit and delete are `409` once a covered payment is paid |
| `GET /api/loan-payments?month=yyyy-MM` | Month list and totals (FR-4) | expected, paid and all |
| `PUT /api/loans/{id}/payments/{month}/{kind}` | Change one payment (FR-4) | amount, due date |
| `POST /api/loans/{id}/payments/{month}/{kind}/paid`, `DELETE …/paid` | Tick (with `paidDate`, `accountId`), untick (FR-5) | |
| `GET /api/loans/dashboard` | Loans widget (FR-13) | active loans, totals, overdue and needs-account flags |
| `GET /api/loan-categories?includeHidden=`, `POST`, `PUT /{id}`, `POST /{id}/hide`, `POST /{id}/show`, `DELETE /{id}` | Categories (FR-8) | include the subcategories; delete is `409` when used |
| `POST /api/loan-categories/{id}/subcategories`, `PUT /api/loan-subcategories/{id}`, `POST …/hide`, `POST …/show`, `DELETE …` | Subcategories (FR-8) | |
| `GET /api/cards/dashboard`, `GET /api/cards/{id}/balance` (extended) | Balances (FR-11) | same response shape, values now include loan payments |
| `GET /api/cash-flow/chart` (extended) | Chart (FR-12) | output series, `loanCategoryId` filter |
| `GET /api/cash-flow/report` (extended) | Report (FR-12) | outputs and month result |

- Payment list items (`LoanPaymentResponse`) carry loan id, month and kind, loan name, category,
  subcategory, payment number (`18 of 60`, or "Residual value"), amount, bill date, due date, paid
  date, account, `isPaid`, `isChanged`, `isOverdue` and `needsAccount`.
- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (ADR 0003).

### Front-end changes (pages, components)
- Routes:
  - `/loans` (list with the **Closed** filter), `/loans/new`, `/loans/:id`, `/loans/:id/edit`.
    These are pages, as for cards, so they work well on a phone.
  - `/settings/loan-categories` for the categories and subcategories.
  - The Month payment edit is a dialog, because it has only amount and due date.
- `src/api/loans.ts`: TanStack Query hooks. Mutations invalidate the loans, loan payments, dashboard
  cards, Loans widget, chart and report queries.
- Components in `src/components/loans/`:
  - `LoanList` and `LoanDetails` (payments made and left, remaining amount, payment change history,
    **Close** and **Delete** with confirmation dialogs that name the loan).
  - `LoanForm`: name, category, subcategory filtered by category, lender (`ListSelectWithAdd` from
    the cards form), payment method, an account select only for standing orders (with the "needs a
    primary account" state), start month, payment day, monthly payment (`parseEuroInput`), number
    of payments ⇄ end month (whichever the user typed last wins), residual value shown only when
    the category allows it, description.
  - `PaymentChangeDialog`: from month, new amount, new payment day, note, and the
    keep-changed-payments question.
  - `ExpensesSection` on the Month page, below Income: payment list, totals, dimmed expected rows,
    **Paid** check box for bills (asks for the account, prefilled with the only primary account),
    a standing-order row correctable or untickable, markers for changed, overdue and needs-account.
    Both dates are shown when paid on another day.
  - `LoansWidget` on the Dashboard, not shown when there are no loans.
  - Labels and messages in `components/loans/texts.ts` (NFR-5).
- `LoanCategoriesSettingsPage`, linked from Settings like Input categories.
- `CashFlowChart` gets the output series (paid and expected in a second colour pair) and the loan
  category filter. `MonthlyReportPage` gets the outputs groups and the month result.
- `LoansPage` replaces its placeholder text ("Monthly payments, due dates and shares").

## Steps
1. Commit the approved requirements (done) and this plan.
2. Domain: loan entities, `DueDateRule`, `LoanSchedule`, payment changes, categories, signed
   `AccountBalance`, with unit tests.
3. Infrastructure: EF configurations, the `AddLoans` migration with seed data, indexes and checks,
   repositories and month queries.
4. Application: DTOs, validators, handlers, `LoanLedger`, balances, chart and report with outputs.
5. Api: endpoints, error mapping, OpenAPI metadata, regenerated OpenAPI document, API tests.
6. Front-end foundations: regenerated types and `loans.ts` hooks.
7. Settings: Loan categories section.
8. Loans screen: list, details, form, payment changes, close and delete.
9. Month screen: Expenses section and the payment dialogs.
10. Dashboard Loans widget; chart and report outputs.
11. Documentation: `docs/INIT.md` data model.
12. Manual check on desktop and phone width, then set this plan's status to `Done`.

## Tests
- Unit tests (Domain):
  - loan rules: each invalid case from acceptance criterion 3, end month ⇄ count, residual value
    only with an allowing category, standing order needs an account;
  - `DueDateRule`: payment day 31 in February and in 30-day months, bill on Saturday and Sunday
    moves to Monday, a bill on the last Saturday or Sunday of the month stays on its day, standing
    order on a weekend stays;
  - `LoanSchedule`: one payment per month from start to end, residual bill in the month after the
    end, several payment changes each from their own month, change of payment day, standing order
    paid after its due date and not on it, unticked standing order stays expected, close stops the
    payments, remaining amount, paid off;
  - payment change rules: from month must be after the latest paid payment, edit and delete
    blocked once a covered payment is paid;
  - `AccountBalance` with outputs: payments before and after a checkpoint, a correction after a
    payment, expected balance with overdue bills and with bills without an account;
  - category and subcategory rename, hide and unique-name rules.
- Application tests: needs-account when the account is blocked, expired, archived or loses its
  primary mark; a blocked account with a standing-order loan stays on the Dashboard Cards widget
  and leaves it once the loan is switched to bill; edit applies only to unpaid payments; `keepChangedPayments` true and false;
  changing the payment method keeps paid standing order payments.
- API / integration tests (real PostgreSQL in CI):
  - add a bank loan with a count and a lease with an end month and a residual value; month lists
    and totals;
  - tick and untick a bill; correct and untick a standing order payment; change one payment;
  - payment changes: create, the `409` cases;
  - close and delete rules; duplicate name `409` and the same name reused after closing;
  - categories: seeded lists, duplicate name `409`, delete used `409`, hide;
  - balances, chart buckets and report with outputs and the month result;
  - database checks reject a zero payment and a standing order without an account.
- Front-end (Vitest + Testing Library):
  - `LoanForm`: account prefill with one primary, required with several, the no-primary state;
    count ⇄ end month; residual value field by category; API validation errors;
  - `ExpensesSection`: dimmed expected rows, totals, ticking a bill asks for the account, markers;
  - `LoansWidget` totals and overdue highlight; report outputs and month result.
- Manual checks: add a mortgage by standing order from the 10th and a car lease paid as a bill on
  the 15th with a residual value. Then tick a bill a day late, record a payment change from a
  future month, and check the Month screen, the Dashboard balances and widget, the chart and the
  report on desktop and phone width.

## Open questions
- None.

## Changes during implementation
- **Automatic payments:** `loan_payment.is_paid` is nullable: `null` means the payment follows the
  rule (a standing order is paid at the end of its due date), `true` or `false` is the user's
  choice. Before a loan is edited or closed, standing order payments already paid automatically
  are stored as paid, so they stay paid. Check constraints:
  `(is_paid IS TRUE) = (paid_date IS NOT NULL)`, the same for `paid_account_id`, and
  `paid_date >= month`.
- **Payment count limit:** at most 1200 payments (100 years), so the end month stays a valid date.
- **Written without the .NET SDK:** the `AddLoans` migration, its model snapshot (no designer
  target model) and `backend/openapi/monthwise.json` were written by hand. The first build checks
  them: CI regenerates the OpenAPI document and fails on any difference, and
  `dotnet ef migrations has-pending-model-changes` shows whether the snapshot matches the model.
- **Accounts no longer usable:** a standing order stops being paid automatically on the day its
  account is archived, blocked, expired or no longer valid, or from today when it is no longer
  primary; such payments carry `needsAccount`.
- **Ongoing transactions:** `ICardActivityQuery` is scoped (it reads the loans), not a singleton.
- **Loan details** return all payments of the loan, paid and expected, not only a window.
- **Duplicate loan name:** a `400` with a `name` field error (like cards and categories), not a
  `409`. The database unique index on `lower(name)` for loans that are not closed still answers a
  race with `409`.
- **Paid date correction:** a paid payment is corrected with the same `POST …/paid` call (new paid
  date and account); its amount and due date with `PUT …/payments/{month}/{kind}`.
- **Chart colours:** outputs use an orange pair, paid strong (`#d4572a`, dark `#e5743d`) and
  expected lighter (`#f0b396`, dark `#8a3a14`), as `--chart-output-paid` and
  `--chart-output-expected`; inputs and outputs are two stacks per bucket.
- **Dialogs:** the payment, paid and payment change dialogs use the existing alert dialog
  component; no new shadcn component was added.
- **Tests:** the shared test clean-up SQL empties the loan tables first, because loans reference
  cards and banks.
