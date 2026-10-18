# Requirements: Input cash flow

- **Branch:** `feat/input_cash_flow`
- **Status:** Approved (2026-10-06)
- **Date:** 2026-10-06

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-input_cash_flow.md`) is written after these
> requirements are approved.

## 1. Goal

A cash flow is money moving into or out of the user's accounts. This feature covers only
**inputs** (incoming cash flow): the user records who pays them, how much, into which account, when
the payment was issued, when it is expected and when it was actually received. Regular inputs such
as salary are recorded once as recurring. The user can also set an account's starting balance and
correct the balance when it does not match the bank.

The recorded inputs are then presented in three places: the balance of each account on the
Dashboard, a cash flow chart dashboard, and a monthly cash flow report.

Outputs (expenses, loan and card payments) are described in later requirements.

## 2. Terms

| Term | Meaning |
|---|---|
| Cash flow | A single movement of money into (input) or out of (output) an account. |
| Input | An incoming cash flow, e.g. salary, salary add-on, a contribution from someone sharing costs, a refund. |
| Payer | The person or organisation that pays an input (Croatian: *uplatitelj*), e.g. an employer. |
| Category | The kind of input, e.g. Salary, Salary add-ons. Managed in Settings (FR-6). |
| Account | A card or bank account registered in Cards and accounts settings (see [credit-card-settings.md](credit-card-settings.md)). |
| Primary account | An account marked as primary (*tekući račun*). Several accounts can be primary. |
| Issue date | The date the payment was created or invoiced (Croatian: *datum računa*). |
| Expected date | The date the money is expected to be credited to the account (Croatian: *datum naplate*). |
| Received date | The date the money was actually credited to the account. Set when the input is marked as received. |
| Expected input | An input that is not yet received. |
| Received input | An input marked as received (payment done). |
| Recurring input | An input that repeats on a regular schedule, e.g. monthly salary. |
| Balance | The amount on an account (Croatian: *saldo*). |
| Balance entry | A starting balance or a balance correction of an account (FR-9). Not a payment from a payer. |

## 3. Input attributes

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| A1 | Payer | Yes | Typed freely, or picked from the list of saved payers that is suggested while typing (autocomplete). Maximum 100 characters. |
| A2 | Account | Yes | The account the money lands on. Any account that is not blocked, expired or archived. Prefilled only when exactly one account is primary (FR-4). |
| A3 | Category | Yes | Chosen from the categories managed in Settings (FR-6). |
| A4 | Amount | Yes | Amount in EUR, exact to the cent, greater than zero. |
| A5 | Issue date | Yes | Date the payment was created (*datum računa*). |
| A6 | Expected date | Yes | Date the money is expected on the account (*datum naplate*). The same as or later than the issue date. |
| A7 | Received | No (default: No) | Check box: the payment is done and the money is on the account (FR-3). |
| A8 | Received date | When received | Date the money was actually credited. Set to today when *Received* is ticked and can then be changed. The same as or later than the issue date. Empty while the input is expected. |
| A9 | Recurring | No (default: No) | Marks the input as repeating, with a frequency and an optional end date (FR-7). |
| A10 | Description | No | Short free note, e.g. "September salary". Maximum 200 characters. |

General rules:
- All amounts are in EUR and exact to the cent.
- A received input belongs to the month of its **received date**. An expected input belongs to the
  month of its **expected date**.

## 4. Functional requirements

### 4.1 Recording inputs

**FR-1 List inputs**
- Inputs are entered and listed on the **Month** screen, in its Income part.
- The user can see the inputs of a chosen month with: payer, category, account, issue date,
  expected date, received date, amount, received marker and recurring marker.
- Default order: by received date (or expected date while not received), then by payer.
- The list shows totals for the month: expected, received and all inputs.
- Expected inputs are visually distinguished (dimmed) from received ones.
- When the month has no inputs, the list shows an empty state with an **Add input** action.

**FR-2 Add an input**
- The user can open an **Add input** form, fill in the attributes from section 3 and save.
- Required fields are clearly marked; the form cannot be saved until all rules in section 3 pass.
- Each validation error is shown next to the field it belongs to, in plain language.
- After saving, the input appears in the list and a confirmation is shown.
- The user can cancel; nothing is saved.

**FR-3 Mark as received**
- Ticking **Received**, in the form or directly in the list, marks the input as received and sets
  the received date to today. The issue date and the expected date stay unchanged.
- The user can change the received date afterwards, e.g. when the money arrived a few days earlier.
- Unticking **Received** returns the input to expected and clears the received date.
- An input received on a different day than expected shows both dates, so the difference is
  visible.

**FR-4 Account selection**
- When exactly one account is primary, it is prefilled and can be changed.
- When several accounts are primary, or none is, the account field is empty and the user must
  choose one before saving.
- Any account that is not blocked, expired or archived can be chosen, whatever its type.
- When no account is registered yet, the form says that an account must be set up first and links
  to Cards and accounts in Settings.

**FR-5 Payers**
- While typing the payer, the user gets suggestions from the saved payers.
- A newly typed payer is saved to the list automatically when the input is saved.
- The user can rename and remove saved payers in a **Payers** section in Settings. Removing a
  payer from the list does not change inputs already recorded with it.

**FR-6 Categories**
- Settings gets an **Input categories** section where the user manages the list of categories.
- The list starts with **Salary**, **Salary add-ons**, **Agreed contributions** and **Other
  income**.
- The user can add and rename categories. A category used by inputs cannot be removed, only
  hidden from selection lists; inputs keep it.
- Category names are unique, ignoring upper/lower case.

**FR-7 Recurring inputs**
- The user can mark an input as recurring and choose how often it repeats: **weekly**, **monthly**,
  **quarterly** or **yearly**, and optionally an end date.
- Each occurrence appears as an expected input in its month with the same payer, category, account
  and amount, so the user only confirms or corrects it. Occurrences appear in every month the user
  views, up to the end date when one is given.
- The user can change one occurrence (amount, dates, account) without changing the others.
- The user can change the recurring input itself (amount, account, frequency, end date); the change
  applies to occurrences that are not yet received.
- The user can stop a recurring input; occurrences already received stay unchanged.

**FR-8 Edit and delete an input**
- The user can change any attribute of an input, with the same rules as when adding. Cancelling
  leaves the input unchanged.
- The user can delete an input after confirming in a dialog that names payer, amount and date.
  Deleting one occurrence of a recurring input removes only that occurrence.

### 4.2 Account balance

**FR-9 Starting balance and balance correction**
- The user can record a **starting balance** for an account: an amount (zero, positive or
  negative, e.g. an overdraft) and the date it applies from. It is optional, but when an account
  has no starting balance the app suggests adding one.
- The user can record a **balance correction** on any date, e.g. when the balance in the app does
  not match the bank because an input or output was missed or entered wrongly. The user enters the
  actual balance on that date; the app shows the calculated balance and records the difference.
- Balance entries change the account balance, but they are not payments: they are shown separately
  and are not counted in the input totals, the chart or the report totals.
- Balance entries can be edited and deleted like inputs.

**FR-10 Balance calculation**
- The **current balance** of an account is its latest starting balance, plus all balance
  corrections and received inputs dated on or after it, up to today.
- The **expected balance** at the end of the current month is the current balance plus the expected
  inputs on that account due by the end of the month.

### 4.3 Presentation

**FR-11 Account balance on the Dashboard**
- The Cards widget on the Dashboard shows, for each account it lists, the **current balance** and
  the **expected balance** at the end of the current month.
- The expected balance is shown in a different color or dimmed, so it is clearly told apart from
  the current balance.
- Balances are shown in EUR to the cent. A negative balance is visibly highlighted.
- An account without a starting balance shows a hint to add one.

**FR-12 Cash flow chart dashboard**
- A separate **Cash flow** dashboard shows a chart of inputs over time.
- By default it shows the **current month**, by day. The user can choose a longer period (e.g. the
  last 12 months or a year), which is then shown by month.
- Received and expected inputs are shown separately, expected ones in a different color or dimmed.
- The user can filter the chart by account, payer and category.
- Selecting a month opens the monthly cash flow report for that month.
- The chart is designed so that outputs can be added once they exist; showing outputs is **not**
  part of this feature.

**FR-13 Monthly cash flow report**
- For a chosen month, the report shows all inputs grouped by category, account and payer, with
  totals: expected, received and all inputs.
- It lists the month's balance entries separately.
- It compares the month with the previous month (total inputs and the difference).
- The user can move to the previous and next month.
- The report is designed so that outputs and the month result can be added once outputs exist.

## 5. Non-functional requirements

- **NFR-1 Exact amounts:** amounts and balances are stored, calculated and shown exactly to the
  cent, never rounded.
- **NFR-2 Responsive:** the input list and form, the Dashboard balances, the Cash flow dashboard and
  the monthly report work on phone and desktop.
- **NFR-3 Access:** only signed-in users of the household can see or change inputs.
- **NFR-4 Data protection:** payer names and amounts are personal financial data. They never appear
  in logs or error messages, and real data is never committed to the repository; test data uses
  made-up values only, as required by `CONTRIBUTING.md`.
- **NFR-5 Language:** UI text in English for now; field labels must be easy to translate to
  Croatian later.

## 6. Out of scope

- Outputs (expenses, utilities, loan payments, card installments and card payments).
- Transfers between the user's own accounts.
- Importing inputs from a bank or a bank statement.
- Multiple currencies.
- Splitting one input across several accounts.
- Closing a month and carrying the balance over to the next month.

## 7. Acceptance criteria

1. The user can add an input with payer, account, category, amount, issue date and expected date,
   and optionally received, recurring and description; it then appears in the month of its
   expected date (or received date once received).
2. With exactly one primary account, the account field is prefilled with it; with several or no
   primary accounts, the user must choose an account before saving. Any account that is not
   blocked, expired or archived can be chosen.
3. Saving is blocked, with a clear message, when:
   - a required field is missing;
   - the amount is zero or negative;
   - the expected date or received date is before the issue date.
4. Typing a payer suggests matching saved payers; a newly typed payer is saved to the list
   automatically and can be renamed or removed in Settings.
5. Ticking **Received** sets the received date to today, keeps the issue and expected dates, and
   moves the amount from expected to received in the totals; unticking reverses this.
6. The user can add and rename input categories in Settings; the four default categories exist
   from the start.
7. A recurring input (weekly, monthly, quarterly or yearly) shows expected occurrences in the
   following months up to its optional end date; changing one occurrence does not change the
   others.
8. Editing or deleting an input updates the list, the totals, the Dashboard balances, the chart
   and the report.
9. After a starting balance is recorded, the account's current balance equals the starting balance
   plus received inputs and corrections since then; a correction with the actual balance makes the
   app's balance equal to it on that date.
10. The Dashboard shows the current balance and the end-of-month expected balance for each listed
    account, with the expected balance visibly distinguished.
11. The Cash flow dashboard shows the current month by default, with received and expected inputs
    distinguished, and opens the monthly report when a month is selected.
12. The monthly report shows the month's inputs grouped by category, account and payer with
    correct totals, its balance entries separately, and the difference to the previous month.

## Appendix 1: Loan payments in the account balance, chart and report

- **Date:** 2026-10-08
- **Branch:** `feat/loans_and_leases`
- **Changes:** FR-10, FR-11, FR-12, FR-13

The **current balance** of an account is its latest starting balance, plus all balance
corrections and received inputs dated on or after it, **minus the paid loan payments** dated on
or after it, up to today.

The **expected balance** at the end of the current month is the current balance plus the
expected inputs, **minus the expected loan payments**, on that account due by the end of the
month. An expected bill payment that has no account yet is not counted.

The Dashboard (FR-11) shows these balances unchanged in every other respect.

The **Cash flow** dashboard (FR-12) also shows outputs: loan payments, paid and expected ones
distinguished in the same way as inputs. It can be filtered by loan category in addition to the
existing filters.

The **monthly cash flow report** (FR-13) also lists the month's loan payments grouped by
category, account and loan, with totals (expected, paid and all), and shows the **month
result**: total inputs minus total outputs. The comparison with the previous month covers
outputs and the month result too.

## Appendix 2: Account balances move from the Dashboard to the Cards screen

- **Date:** 2026-10-18
- **Branch:** `feat/design_refresh`
- **Changes:** FR-11, Appendix 1 (where the balances are shown)

The current balance, the expected balance at the end of the month and the hint to add a starting
balance are **no longer shown on the Dashboard**. They are shown, unchanged, for each account in
the **Accounts** part of the **Cards** screen (see [design-refresh.md](design-refresh.md) FR-9 and
FR-12). Where FR-11, Appendix 1 or the acceptance criteria say "the Dashboard" for balances, read
"the Cards screen". How the balances are calculated does not change.
