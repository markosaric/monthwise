# Plan: Recurring bill options

- **Branch:** `feat/recurring_bill_options`
- **Status:** In progress
- **Date:** 2026-11-04
- **Requirements:** [monthly-bills-and-expenses.md, Appendix 2](../feature-requirements/monthly-bills-and-expenses.md#appendix-2-recurring-bill-from-a-bill-optional-amount-and-dates)

## Goal
The user can give a recurring bill an optional amount, issue day and due day, so that its bills are
filled in (or fully Expected) when they are added to a month, and can turn a one-off bill into a
recurring bill with one tick box in the bill form.

## Requirement changes
[Appendix 2](../feature-requirements/monthly-bills-and-expenses.md#appendix-2-recurring-bill-from-a-bill-optional-amount-and-dates)
changes FR-2, FR-3, FR-4 (states) and acceptance criteria 3, 4 and 5:

- **A2.1:** a recurring bill gets three optional attributes: R6 Amount (greater than zero),
  R7 Issue day and R8 Due day (1 to 31, the last day of a shorter month; a due day earlier than the
  issue day means the following month). Changes apply to bills added later.
- **A2.2:** Add recurring bills adds a bill as **Expected** when all three are set, otherwise as
  **Not issued** with the known values already filled in. The bill's values can be changed in its
  month. A due day replaces the due date hint. Not issued now means "amount, issue date or due date
  still missing".
- **A2.3:** the Add and Edit bill forms get a **Recurring** tick box for one-off bills. Ticked, it
  shows the optional Amount, Issue day and Due day (prefilled from the bill) and, on saving, adds an
  active recurring bill with the bill's details and shares; the bill becomes its bill for that month.
  A bill from a recurring bill shows the box ticked, locked, with a link to its recurring bill.
- **A2.4:** expenses, card-paid bills, paying and sharing rules, and the Dashboard Recurring bills
  card are unchanged.

## Scope
- In scope: A2.1 to A2.5; the recurring bill form and list; the bill form; the user manual
  paragraphs on recurring bills and bills.
- Out of scope: recurring expenses; unlinking a bill from its recurring bill; a monthly total of
  recurring bills on the Dashboard; changing bills already in a month when a recurring bill changes.

## Design
- **Domain / data model changes** (`Monthwise.Domain/Expenses`):
  - `RecurringBill`: new `Money? Amount`, `int? IssueDay`, `int? DueDay`, added to
    `RecurringBillDetails`. Validation: amount, when given, greater than zero
    (`ExpenseErrors.AmountNotPositive`); days, when given, 1 to 31 (new `ExpenseErrors.DayOutOfRange`).
  - New `RecurringBill.DatesIn(YearMonth month)` returning `(DateOnly? IssueDate, DateOnly? DueDate)`:
    each day clamped to the month's last day; the due date moves to the following month when the
    due day is earlier than the issue day (R8).
  - `Bill.IsIssued` becomes `Amount.IsPositive && IssueDate is not null && DueDate is not null`
    (A2.2). All totals, outputs, shares and the Dashboard already go through `IsIssued` / `State`,
    so they keep counting a Not issued bill as 0.
  - `Bill.CreateFromRecurring` copies the recurring bill's amount (or 0) and `DatesIn(month)`, so the
    bill is Expected when all three are set (A2.2).
  - `Bill` validation: a one-off bill still needs all three (unchanged). A bill from a recurring bill
    may hold any subset: amount not negative, due date not before issue date when both are given; it
    is Expected only when all three are complete. Paid still needs an issued bill.
  - New `Bill.LinkTo(RecurringBill recurringBill, DateTimeOffset now)` for a one-off bill (A2.3); it
    fails with new `ExpenseErrors.BillAlreadyRecurring` when the bill already has a recurring bill.
- **Application** (`Monthwise.Application/Expenses`):
  - `RecurringBillRequest` / `RecurringBillResponse`: `long? AmountCents`, `int? IssueDay`,
    `int? DueDay`. `RecurringBillHandler` maps them.
  - `BillRequest`: new optional `RecurringOptionsRequest? Recurring` (`long? AmountCents`,
    `int? IssueDay`, `int? DueDay`). In `BillHandler.CreateAsync` / `UpdateAsync`, when it is set on a
    one-off bill: build a `RecurringBill` from the bill's name, category, payee, paid from,
    description and the options; run the same checks as `RecurringBillHandler` (name unique among
    active recurring bills, category, paid from); copy the bill's resolved share to it; add it and
    `LinkTo` it in the same save, so either both are stored or neither (A2.3). Errors use the field
    names of the bill form (`name`, `recurring.amountCents`, `recurring.issueDay`,
    `recurring.dueDay`). Ignored when the bill already has a recurring bill.
  - Extract the shared name/category/paid-from check of `RecurringBillHandler` into a small
    `RecurringBillChecker` used by both handlers.
  - `ExpenseLedger.DueDateHint`: unchanged logic, but returns null when the bill already has a due
    date (it now can while Not issued).
  - `BillResponse` gets `string? RecurringBillName` for the link in the form.
- **API changes:** the new fields above on `POST/PUT /api/recurring-bills`, `GET /api/recurring-bills`,
  `POST /api/bills?month=yyyy-MM` and `PUT /api/bills/{id}`; no new endpoints. Regenerate
  `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (`npm run api:generate`).
- **Front-end changes:**
  - `billFormModel.ts`: recurring bill schema and values with `amount`, `issueDay`, `dueDay`
    (empty allowed); `billFormSchema` relaxes the "all three" rule for bills from a recurring bill
    and adds the `recurring` block (`isRecurring`, `amount`, `issueDay`, `dueDay`) for one-off bills;
    `billFormValuesOf` shows the stored amount and dates of a Not issued bill.
  - `BillForm.tsx`: a **Recurring** switch (same style as **Paid** and **Shared**) under the
    Description, with a **Recurring bill** section holding Amount (€), Issue day and Due day
    (numeric, 1 to 31). Ticking it prefills them from the bill's amount and the days of its issue
    and due dates. On a bill from a recurring bill the switch is ticked and disabled, with
    "From recurring bill <name>" linking to `/recurring-bills/<id>/edit`.
  - `RecurringBillsPages.tsx`: the three optional fields in the form; the list row shows the amount
    and "issued on day N, due on day M" when set.
  - `texts.ts`: labels and messages in English, easy to translate (NFR-5).
- **Database migrations:** `AddRecurringBillOptions`:
  - `recurring_bill`: nullable `amount_cents bigint`, `issue_day int`, `due_day int`, with check
    constraints `amount_cents > 0`, `issue_day BETWEEN 1 AND 31`, `due_day BETWEEN 1 AND 31`.
  - `bill`: replace `ck_bill_dates` and `ck_bill_amount` with `ck_bill_amount_not_negative`
    (`amount_cents >= 0`) and `ck_bill_one_off_issued` (`recurring_bill_id IS NOT NULL OR (amount_cents > 0
    AND issue_date IS NOT NULL AND due_date IS NOT NULL)`); `ck_bill_paid_issued` becomes
    `NOT is_paid OR (amount_cents > 0 AND issue_date IS NOT NULL AND due_date IS NOT NULL)`.
  - Existing rows already satisfy the new constraints; no data change. Export and import pick up the
    new columns automatically (all tables are copied and migrated on import).

## Steps
1. Domain: `RecurringBill` options and `DatesIn`, `Bill.IsIssued`, `CreateFromRecurring`, relaxed
   validation and `LinkTo`, new errors; domain tests.
2. Infrastructure: EF configuration and the `AddRecurringBillOptions` migration; migration test.
3. Application and API: contracts, `RecurringBillChecker`, `RecurringBillHandler`, `BillHandler`
   (Recurring options on create and update), `DueDateHint`; API tests; regenerate OpenAPI.
4. Front-end: schema types, recurring bill form and list, bill form Recurring switch and section,
   Not issued values; component tests.
5. User manual: recurring bills (optional amount and days, Expected when complete) and the Recurring
   tick box on a bill.
6. Run back-end tests, front-end lint, type check and tests; stage everything for review.

## Tests
- Unit tests (domain):
  - recurring bill amount must be positive, days 1 to 31, all optional;
  - `DatesIn`: day 31 in February is the last day; due day earlier than issue day is next month;
  - `CreateFromRecurring`: all three set → Expected; some set → Not issued with those values; none →
    as before;
  - bill from a recurring bill can be saved with a subset, becomes Expected when complete, cannot
    be paid while Not issued; one-off bill still needs all three;
  - `LinkTo` on a bill that already has a recurring bill fails.
- API / integration tests:
  - recurring bill CRUD with and without the options; invalid day or amount refused;
  - Add recurring bills adds Expected and Not issued bills accordingly, totals count only Expected,
    running it twice adds nothing;
  - creating a one-off bill with `recurring` adds an active recurring bill with the bill's details and
    share, links the bill, and Add recurring bills skips that month and adds the next one;
  - same name as an active recurring bill: error on `name`, neither the bill nor the recurring bill
    is saved;
  - editing a one-off bill with `recurring` links it; `recurring` on a bill from a recurring bill is
    ignored;
  - migration test passes on the new constraints.
- Front-end tests (`expenses.test.tsx`):
  - Recurring switch shows the section, prefilled from amount and dates, and sends `recurring`;
  - a bill from a recurring bill shows the switch ticked and disabled with the link;
  - recurring bill form sends and validates the optional fields;
  - a Not issued bill with a stored amount shows it and can be saved without dates.
- Manual checks: on phone and desktop, add a one-off bill with Recurring ticked, run Add recurring
  bills for the next month, and check the bill appears as Expected or Not issued as expected.
