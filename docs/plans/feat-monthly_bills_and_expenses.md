# Plan: Monthly bills and expenses

- **Branch:** `feat/monthly_bills_and_expenses`
- **Status:** In progress
- **Date:** 2026-10-09
- **Requirements:** [monthly-bills-and-expenses.md](../feature-requirements/monthly-bills-and-expenses.md) (approved), and
  [credit-card-installments.md, Appendix 1](../feature-requirements/credit-card-installments.md#appendix-1-shared-expense-categories)

## Goal
The user keeps recurring bills and adds them to a month with **Add recurring bills** as Not issued
bills (amount 0), fills them in when they arrive and ticks them when paid. The user records
one-time expenses and cash withdrawals as they happen. Bills and expenses are paid from an account
or with a credit card; card-paid ones become lines of that card's card payment, so they are counted
once. Bills, expenses and card purchases share one **Expense categories** list. Everything shows on
the Month screen, in the account balances, on the Cash flow dashboard and in the monthly report.

## Requirement changes
- [credit-card-installments.md, Appendix 1](../feature-requirements/credit-card-installments.md#appendix-1-shared-expense-categories)
  (2026-10-09, changes FR-10 and acceptance criterion 16 of card installments):
  - The **Purchase categories** list becomes one shared **Expense categories** list, used by card
    purchases, bills, recurring bills and expenses. Existing categories stay with their names and
    hidden state.
  - The list gets the starting categories of FR-1, skipping names already in it, and the fixed
    **Cash withdrawal** category.
  - A category used anywhere can only be hidden. The category of a card purchase stays optional.
  - Implementation: see "Expense categories" below.

## Scope
- In scope:
  - Domain: shared expense categories, recurring bills, bills, expenses (with cash withdrawals),
    card charges from bills and expenses in the card payment, and bills and expenses in the balance.
  - Database: one migration that renames and extends the category table and adds the bill and
    expense tables.
  - API endpoints for categories (renamed), recurring bills, bills, expenses, and the extended card
    payment drill-down, balances, chart and report.
  - Front-end: Expense categories in Settings, recurring bills page, Bills and Expenses lists on the
    Month screen with their forms, card drill-down lines for bills and expenses, chart filter and
    report groups.
  - Tests at every layer.
- Out of scope:
  - Everything in section 5 of the requirements: shared bills (50 %), split payments, cash
    tracking, recurring expenses, imports, reminders, currencies.
  - **Access control** (NFR-3): there is still no authentication, as for cards, inputs and loans.
  - Households: one implicit household.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| FR-1 | One `ExpenseCategory` table (the renamed `purchase_category`). Name 1 to 50, unique case-insensitive. `IsPredefined` for starting entries, `IsFixed` for **Cash withdrawal** (cannot be renamed or hidden). A category used by a purchase, bill, recurring bill or expense can only be hidden; an unused, non-predefined one can be deleted. |
| FR-1 | Category required on bills, recurring bills and expenses; optional on card purchases. **Cash withdrawal** cannot be chosen for a bill, a recurring bill, a card purchase or a normal expense; it is set only by recording a cash withdrawal. |
| FR-2, R1 to R5 | Name 1 to 50, unique among active recurring bills (case-insensitive); payee max 100; description max 200; optional default *Paid from*. `StoppedOn` marks a stopped recurring bill. Delete only when no bill was added from it. |
| FR-2, due date hint | For a Not issued bill, the API returns `dueDateHint`: the day of the due date of the latest issued bill of the same recurring bill, clamped to the bill's month. No hint for the first bill. |
| FR-3 | `POST /api/bills/add-recurring?month=` adds a Not issued bill for every active recurring bill without a bill in that month (unique `(recurring_bill_id, month)`), returns the number added. The front-end defaults the month to next month. |
| FR-4 | A bill has a `Month` (the month it was added to). State: **Not issued** when `IssueDate` is null (amount 0, no dates), **Expected** when issued and not paid, **Paid** when paid. Issuing requires amount > 0, issue date and due date ≥ issue date; paying requires issued. Paid date ≥ issue date. |
| FR-4 | A paid bill counts in the cash flow on its paid date, an expected one on its due date; Not issued bills count as 0 and are not in totals. Overdue = expected and due date < today. |
| FR-5 | *Paid from* is stored as `PaidFromId` (a `card` id: accounts and cards share the table). What it means follows from the card type: a **Credit or Charge card** is a credit card, so the bill or expense is a card charge (FR-6); it must have billing details and be Active or Expiring soon on the paid date and today. Any other account, **Debit cards included** (also with installments), is an account: not blocked, expired or archived, and the bill or expense is an output of that account. |
| FR-6, FR-7 | A bill or expense paid with a credit card is a **card charge**: a line of the card payment of the billing period that contains its paid date (`calendar.PaymentMonthOf(date)`), the same way a purchase paid in full is placed. It is never a separate output. |
| FR-6 | Editing, unticking or deleting a card-paid bill or expense whose card payment date has passed returns `changesChargedCardPayment = true` so the form warns first, as for purchases. |
| FR-7, E4 | An expense is always paid. Prefill: the last *Paid from* used (`GET /api/expenses/last-paid-from`), otherwise the only primary account. |
| FR-8 | A cash withdrawal is an expense with the fixed Cash withdrawal category, a **primary** account that is not blocked, expired or archived. |
| FR-9 | Month screen: bills by `Month`; expenses by date in the month. Bill totals (expected, paid, all) leave out Not issued and card-paid bills; expense totals give the month total and the cash withdrawal total, leaving out card-paid expenses from the account total but listing them. |
| FR-10 | Account-paid bills and expenses are balance movements on their paid date / date; expected account bills with a *Paid from* and due by the end of the month lower the expected balance. Chart and report count them as outputs, grouped by expense category. |

### Expense categories (Appendix 1)
- Rename `Monthwise.Domain.CardPurchases.PurchaseCategory` to
  `Monthwise.Domain.Expenses.ExpenseCategory`, add `IsPredefined` and `IsFixed`. `Rename` and `Hide`
  fail for a fixed category.
- `ExpenseCategorySeedData` holds the starting names and the fixed id of **Cash withdrawal**.
- The migration inserts the starting categories with SQL (`insert … where not exists (… lower(name)
  = lower(@name))`) instead of `HasData`, because a user may already have a purchase category with
  the same name; such a category is then marked predefined instead. Cash withdrawal is inserted with
  its fixed id; an existing user category called "Cash withdrawal" is renamed "Cash withdrawal (own)"
  first, so the fixed one can take the name.
- `PurchaseCategoryHandler` becomes `ExpenseCategoryHandler`; "in use" checks purchases, bills,
  recurring bills and expenses. Endpoints move from `/api/purchase-categories` to
  `/api/expense-categories` (same shape plus `isPredefined`, `isFixed`).
- `PurchaseForm` and card purchase validation reject the fixed category.

### Card charges in the card payment
- New `CardCharge` record (Domain/CardPurchases): `SourceId`, `CardId`, `Kind` (`Bill` or
  `Expense`), `Date`, `Description`, `CategoryId`, `Amount`.
- `CardPaymentLineKind` gets `Bill` and `Expense`. `CardPaymentLine.PurchaseId` is renamed
  `SourceId` (the purchase, bill or expense id).
- `CardPaymentSchedule` takes the card's charges as an extra input and adds one line per charge in
  `calendar.PaymentMonthOf(charge.Date)`. Everything else (amount, paid state, automatic payment,
  overdue) is unchanged, so card-paid bills and expenses flow into the balance, chart and report
  through the card payment only.
- `CardPaymentLedger.LoadAsync` loads the charges from bills and expenses paid with a
  Credit or Charge card. The drill-down response gets `kind`, `sourceId` and the category for
  bill and expense lines; the front-end links them to the bill or expense.
- `HasChargedInstallments` logic is reused for charges: a charge is charged when its card payment
  date is before today.

### Domain / data model changes
- `Monthwise.Domain/Expenses`:
  - `ExpenseCategory` (moved and extended), `ExpenseCategorySeedData`;
  - `PaidFrom` rules: `IsCreditCard(card)` (Credit or Charge type) and the account / credit card checks;
  - `RecurringBill`: `Create`, `Update`, `Stop`, `Restart`;
  - `Bill`: `CreateFromRecurring(recurringBill, month)`, `CreateOneOff`, `Issue`, `Update`,
    `MarkPaid(date, paidFrom)`, `MarkUnpaid`, `State`;
  - `Expense`: `Create`, `CreateCashWithdrawal`, `Update`, `IsCashWithdrawal`;
  - `ExpenseErrors` with field keys.
- `Monthwise.Domain/CardPurchases`: `CardCharge`, the new line kinds, `CardPaymentSchedule` with
  charges.
- Entities use Guid v7 ids, `CreatedAt` and `UpdatedAt`; rules return `Result` errors with field
  keys, as for cards, inputs and loans.
- Choosing *Paid from* (usable account, primary account, usable credit card) is checked in the
  handlers, which load the card, as for loans and purchases (`LoanAccount.CanBeChosen`,
  `CanBeUsedOn`).

### Database migrations
- Migration `AddBillsAndExpenses` (snake_case):
  - rename `purchase_category` → `expense_category`, add `is_predefined` (bool, default false) and
    `is_fixed` (bool, default false); rename the FK and index on `card_purchase.category_id`;
    insert the starting categories as described above.
  - `recurring_bill` (`name`, `category_id` → `expense_category`, `payee`, `paid_from_id` →
    `card`, `description`, `stopped_on`), unique index on `lower(name)` where
    `stopped_on is null`.
  - `bill` (`recurring_bill_id` → `recurring_bill` nullable, `month`, `name`, `category_id`,
    `payee`, `amount_cents`, `issue_date`, `due_date`, `is_paid`, `paid_date`, `paid_from_id` →
    `card`, `description`), unique on (`recurring_bill_id`, `month`) where
    `recurring_bill_id is not null`, index on `month`, index on (`paid_from_id`, `paid_date`).
  - `expense` (`date`, `amount_cents`, `category_id`, `paid_from_id` → `card`,
    `description`), index on `date`, index on (`paid_from_id`, `date`).
- Check constraints:
  - `bill`: `(issue_date is null) = (due_date is null)`; `issue_date is null and amount_cents = 0
    or issue_date is not null and amount_cents > 0`; `due_date >= issue_date`; `is_paid = (paid_date
    is not null)`; `not is_paid or issue_date is not null`; `not is_paid or paid_from_id is not
    null`; `paid_date >= issue_date`.
  - `expense`: `amount_cents > 0`, `paid_from_id is not null`.
- Foreign keys restrict deletes; deleting a bill does not touch its recurring bill.

### API changes (endpoints, DTOs)
New `Api/Endpoints/BillEndpoints.cs` and `ExpenseEndpoints.cs`, `Monthwise.Application/Expenses`
handlers, FluentValidation validators and the same error mapping as before.

| Method and path | Purpose | Notes |
|---|---|---|
| `GET /api/expense-categories?includeHidden=`, `POST`, `PUT /{id}`, `POST /{id}/hide`, `POST /{id}/show`, `DELETE /{id}` | Categories (FR-1, Appendix 1) | replaces `/api/purchase-categories`; `409` for delete when used, rename or hide of the fixed one |
| `GET /api/recurring-bills?includeStopped=`, `POST`, `GET/PUT/DELETE /{id}`, `POST /{id}/stop`, `POST /{id}/restart` | Recurring bills (FR-2) | delete `409` when bills exist |
| `POST /api/bills/add-recurring?month=yyyy-MM` | Add recurring bills (FR-3) | returns `{ added }` |
| `GET /api/bills?month=yyyy-MM` | Month list and totals (FR-4, FR-9) | state, overdue, `dueDateHint`, paid from, `paidWithCard` |
| `POST /api/bills`, `GET/PUT/DELETE /api/bills/{id}` | One-off bill, issue/edit, delete (FR-4) | details include `changesChargedCardPayment` |
| `POST /api/bills/{id}/paid`, `DELETE /api/bills/{id}/paid` | Tick (with `paidDate`, `paidFromId`), untick (FR-5) | `400` when not issued |
| `GET /api/expenses?month=yyyy-MM` | Month list and totals (FR-7, FR-8, FR-9) | total, cash withdrawal total |
| `POST /api/expenses`, `POST /api/expenses/cash-withdrawals`, `GET/PUT/DELETE /api/expenses/{id}` | Record, edit, delete (FR-7, FR-8) | |
| `GET /api/expenses/last-paid-from` | Prefill (E4) | |
| `GET /api/cards/{id}/card-payments/{month}` (extended) | Drill-down with bill and expense lines (FR-6) | `kind`, `sourceId`, category |
| `GET /api/cards/dashboard`, `GET /api/cards/{id}/balance`, `GET /api/cash-flow/chart`, `GET /api/cash-flow/report` (extended) | Balances, chart, report (FR-10) | chart gets an `expenseCategoryId` filter; report outputs gain expense categories in `byCategory` and account-paid bills and expenses in `byAccount` and the totals |

- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (ADR 0003).

### Front-end changes (pages, components)
- Routes (pages, as for loans and purchases, so they work well on a phone):
  - `/month/:yearMonth/bills/new`, `/month/:yearMonth/bills/:id/edit`;
  - `/month/:yearMonth/expenses/new`, `/month/:yearMonth/expenses/new?kind=cash`,
    `/month/:yearMonth/expenses/:id/edit`;
  - `/recurring-bills`, `/recurring-bills/new`, `/recurring-bills/:id/edit`;
  - `/settings/expense-categories` replaces `/settings/purchase-categories` (the old path
    redirects).
- `src/api/bills.ts`, `src/api/expenses.ts`, `src/api/expenseCategories.ts`: TanStack Query hooks.
  Mutations invalidate the month lists, card payments, drill-downs, dashboard balances, chart and
  report queries. `cardPurchases.ts` switches to `expenseCategories.ts`.
- Components in `src/components/bills/` and `src/components/expenses/`:
  - `BillsSection` on the Month screen: rows with name, category, amount, issue, due and paid
    dates, paid from and state (Not issued dimmed, overdue highlighted, "with card" marker), the
    **Paid** check box (asks for the date and *Paid from*), totals, and the buttons **Add recurring
    bills** (to next month by default, shows how many were added), **Add bill** and a link to
    **Recurring bills**.
  - `BillForm`: issue a bill (amount, issue date, due date prefilled with `dueDateHint`) or add a
    one-off bill; edit and delete with the card warning.
  - `RecurringBillsPage` and `RecurringBillForm`.
  - `ExpensesSection` on the Month screen: rows with date, category, description, amount and paid
    from, the month total and the cash withdrawal total, and **Add expense** / **Cash withdrawal**.
  - `ExpenseForm`: date (today), amount, category, paid from (prefilled), description; fits on one
    phone screen. `CashWithdrawalForm`: date, amount, primary account, description.
  - `PaidFromSelect`: grouped list **Accounts** and **Credit cards**, Debit cards are in Accounts; Credit and Charge cards with billing details in Credit cards.
  - Labels and messages in `texts.ts` files (NFR-5).
- The current loan `ExpensesSection` (titled "Expenses") is renamed `LoanPaymentsSection` with the
  title **Loan payments**, so the Month screen reads: Income, Bills, Expenses, Loan payments, Card
  payments.
- `CardPaymentDrillDown` shows bill and expense lines with their marker; they open the bill or
  expense form.
- `ExpenseCategoriesSettingsPage` (renamed from `PurchaseCategoriesSettingsPage`): fixed category
  without rename or hide actions, predefined marker.
- `CashFlowChart` gets the expense category filter; `MonthlyReportPage` shows the new category and
  account groups (no new group type).

### Documentation
- `docs/INIT.md` section 3: replace the draft `category`, `recurring_item` and `transaction` lines
  with `expense_category`, `recurring_bill`, `bill` and `expense`, and update `card_purchase` /
  `purchase_category`.

## Steps
1. Commit the approved requirements (done) and this plan.
2. Domain: `ExpenseCategory` (moved), `RecurringBill`, `Bill`, `Expense`, `PaidFrom`, `CardCharge`
   and the card payment schedule with charges, with unit tests.
3. Infrastructure: EF configurations, the `AddBillsAndExpenses` migration with renames, seed SQL,
   indexes and checks, repositories and month queries.
4. Application: DTOs, validators, handlers, `CardPaymentLedger` with charges, balances, chart and
   report with bills and expenses.
5. Api: endpoints, error mapping, OpenAPI metadata, regenerated OpenAPI document, API tests.
6. Front-end foundations: regenerated types and hooks; category rename.
7. Settings: Expense categories page.
8. Recurring bills page and form.
9. Month screen: Bills and Expenses sections, forms, Paid dialog, Loan payments rename.
10. Card drill-down lines, chart filter and report groups.
11. Documentation: `docs/INIT.md` data model.
12. Manual check on desktop and phone width, then set this plan's status to `Done`.

## Tests
- Unit tests (Domain):
  - `ExpenseCategory`: unique names, fixed category cannot be renamed or hidden.
  - `RecurringBill`: required fields, unique-name rules handled in the handler, stop and restart.
  - `Bill`: Not issued has amount 0 and no dates; issuing needs amount > 0 and due ≥ issue; cannot
    pay before issued; paid date ≥ issue date; untick clears the paid date; state and overdue.
  - `Expense`: amount > 0; cash withdrawal only from a primary account, never a Credit or Charge card.
  - `CardPaymentSchedule` with charges: a bill paid on the statement date and the day after lands
    in the right card payment; the card payment amount adds charges to purchases and credits;
    charged state for the warning.
  - `AccountBalance` with account-paid bills and expenses before and after a checkpoint.
- Application tests:
  - Add recurring bills twice adds each bill once; stopped recurring bills are skipped.
  - `dueDateHint` from the latest issued bill, clamped in a shorter month; none for the first bill.
  - Card-paid bills and expenses are not counted twice in balances, chart and report.
  - Paid from rules: blocked or archived account refused; non-primary account refused for cash
    withdrawal; card without billing details refused as credit card.
- API / integration tests (real PostgreSQL in CI):
  - the migration keeps existing purchase categories, skips duplicate starting names and adds Cash
    withdrawal;
  - categories CRUD under the new path, `409` cases;
  - recurring bills CRUD, add-recurring, bills issue / pay / untick / delete;
  - expenses and cash withdrawals CRUD;
  - drill-down lines for a card-paid bill and expense;
  - balances, chart buckets and report totals with bills and expenses;
  - database checks reject an issued bill with amount 0 and a paid Not issued bill.
- Front-end (Vitest + Testing Library):
  - `BillsSection`: Not issued dimmed and without a Paid box, overdue highlight, totals, Add
    recurring bills message.
  - `BillForm`: due date prefilled from the hint, validation errors, card warning.
  - `ExpenseForm`: prefilled paid from and date, grouped *Paid from* list, cash withdrawal form
    offers primary accounts only.
  - `ExpenseCategoriesSettingsPage`: fixed category has no rename or hide.
  - Drill-down shows bill and expense lines and links them.
- Manual checks:
  - Add three recurring bills (one paid with a credit card by default), add them to next month,
    run the action again, issue and pay them.
  - Record a grocery expense with a primary account and one with a credit card, and a cash
    withdrawal.
  - Check the Month screen, the card drill-down, the Dashboard balances, the chart and the report
    on desktop and phone width.

## Decisions
- **Debit cards** are always accounts for bills and expenses, also when they allow installments,
  because a bill or expense is never paid in installments (requirements, *Credit card* term).
- **Recurring bills** get their own page (`/recurring-bills`), linked from the Bills section.
- **Month screen:** the current loan "Expenses" card becomes **Loan payments**, next to the new
  Bills and Expenses cards.

## Changes during implementation
- **Validation:** bill, recurring bill and expense rules live in the domain with field keys, as for
  card purchases; no FluentValidation validators were needed.
- **Card payment lines:** `CardPaymentLineResponse.purchaseId` is now `sourceId` (the purchase, bill
  or expense) and `purchaseDate` is now `date`; the kinds `Bill` and `Expense` were added.
- **Bills API:** a one-off bill is added with `POST /api/bills?month=yyyy-MM`. `BillRequest.paidDate`
  sets the paid state when adding or editing (null = not paid); the `/paid` endpoints tick and untick
  from the Month screen. The month list also returns `withCardCents` and `notIssuedCount`.
- **Expenses API:** a cash withdrawal is edited with `PUT /api/expenses/cash-withdrawals/{id}`. The
  month totals are `totalCents`, `fromAccountsCents`, `withCardCents` and `cashWithdrawalCents`.
- **Expected bills with a credit card:** an expected bill whose usual *Paid from* is a credit card is
  an expected output without an account in the chart and report (it joins the card payment only once
  paid), and does not lower any account's expected balance.
- **Routes:** bills and expenses are edited at `/bills/:id/edit` and `/expenses/:id/edit`, so the
  card drill-down can open them; adding stays under `/month/:yearMonth/…`. The *Add recurring bills*
  dialog asks for the month (next month by default) and then opens it.
- **Recurring bills:** restarting a stopped recurring bill checks that its name is still unique
  among the active ones.
- **Chart:** the new filter is labelled **Bills and expenses** and lists the expense categories.
- **Migration:** `Down` removes the starting categories that no purchase uses and Cash withdrawal,
  and renames the table back to `purchase_category`.
