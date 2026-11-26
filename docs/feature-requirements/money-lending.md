# Requirements: Money lending

- **Branch:** `feat/money-lending`
- **Status:** Approved (2026-11-26)
- **Date:** 2026-11-26

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-money-lending.md`) is written after these
> requirements are approved.

## 1. Goal

Sometimes the user lends money to someone outside the household, e.g. to a friend or a relative
(Croatian: *posudba*), and that person returns it later. Today the app can only record loans the
user **takes** from a bank ([loans-and-leases.md](loans-and-leases.md)) and cash taken from an
account ([monthly-bills-and-expenses.md](monthly-bills-and-expenses.md) FR-8); neither covers money
lent to someone and expected back.

This feature lets the user record a **lending**: who the money was lent to, how much, from which
account and when, and when it is expected back. In the cash flow:

- the money lent is an **output** of the account it left, on the lend date;
- each **repayment** is an **input** to the account it arrives on, on the date it is received;
- until the money is returned in full, the app shows how much the borrower still owes and whether a
  repayment is late.

## 2. Terms

| Term | Meaning |
|---|---|
| Lending | Money the user lends to a borrower once, to be returned later (Croatian: *posudba*). |
| Borrower | The person the money is lent to (Croatian: *dužnik*). Not a user of the app. A borrower is a **payer** from the payers list (FR-6). |
| Lend date | The date the money left the user's account (Croatian: *datum posudbe*). |
| Expected return date | The date by which the borrower is expected to return the money (Croatian: *očekivani datum povrata*). |
| Repayment | An amount the borrower returns (Croatian: *povrat*). A lending can be returned in one or several repayments (FR-4). |
| Received date | The date a repayment was actually credited to an account. |
| Outstanding | The lent amount minus all received repayments: what the borrower still owes. |
| Overdue | A lending whose expected return date has passed while something is still outstanding. |
| Returned | A lending whose outstanding amount is zero. |
| Account, Primary account | As in [credit-card-settings.md](credit-card-settings.md) and [input-cash-flow.md](input-cash-flow.md). |
| Input, Output, Expected input, Received input | As in [input-cash-flow.md](input-cash-flow.md) and [loans-and-leases.md](loans-and-leases.md). |

## 3. Lending attributes

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| L1 | Borrower | Yes | Typed freely, or picked from the saved payers suggested while typing (FR-6). Maximum 100 characters, as for payers. |
| L2 | Amount | Yes | Amount lent, in EUR, exact to the cent, greater than zero. |
| L3 | Lend date | Yes | The date the money left the account. Prefilled with today. |
| L4 | From account | Yes | The account the money left. Any account that is not blocked, expired or archived. Prefilled only when exactly one account is primary (as in [input-cash-flow.md](input-cash-flow.md) FR-4). Not a credit card. |
| L5 | Expected return date | Yes | The date the money is expected back. The same as or later than the lend date. |
| L6 | Description | No | Short free note, e.g. "For the car repair". Maximum 200 characters. |

Repayment attributes:

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| R1 | Amount | Yes | In EUR, exact to the cent, greater than zero, and not more than the outstanding amount. |
| R2 | To account | Yes | The account the money arrived on. Any account that is not blocked, expired or archived. Prefilled with the lending's *From account*. |
| R3 | Received date | Yes | The date the money arrived. Prefilled with today. The same as or later than the lend date. |
| R4 | Note | No | Short free note. Maximum 200 characters. |

General rules:
- All amounts are in EUR and exact to the cent.
- The lent amount belongs to the month of its **lend date**. A received repayment belongs to the
  month of its **received date**. The expected return of the outstanding amount belongs to the
  month of the **expected return date**.

## 4. Functional requirements

### 4.1 Recording lendings

**FR-1 List lendings**
- A **Lending** screen lists the user's lendings with: borrower, amount lent, lend date, from
  account, expected return date, amount returned, outstanding amount and state (Open, Overdue,
  Returned).
- Default order: open and overdue lendings first, by expected return date; then returned ones, the
  latest first.
- Shown by default: lendings that are not returned. A **Returned** filter shows returned lendings
  with their repayments.
- The screen shows the total outstanding amount and, separately, the overdue amount.
- When there are no lendings, the screen shows an empty state with an **Add lending** action.

**FR-2 Add a lending**
- The user can open an **Add lending** form, fill in the attributes from section 3 and save.
- Required fields are clearly marked; the form cannot be saved until all rules in section 3 pass.
- Each validation error is shown next to the field it belongs to, in plain language.
- After saving, the lending appears in the list and a confirmation is shown. The user can cancel;
  nothing is saved.
- When no account is registered yet, the form says that an account must be set up first and links
  to Cards and accounts in Settings.

**FR-3 Edit and delete a lending**
- The user can change any attribute of a lending with the same rules as when adding. The amount
  cannot be lowered below the sum of the repayments already received.
- The user can delete a lending after confirming in a dialog that names the borrower, amount and
  lend date. Deleting a lending also deletes its repayments, and the dialog says so.

**FR-4 Record a repayment**
- From a lending, the user records a repayment with the attributes R1 to R4. The amount is
  prefilled with the outstanding amount, so returning everything at once takes one action.
- A lending can be returned in **several repayments**; each one lowers the outstanding amount.
- When the outstanding amount reaches zero, the lending becomes **Returned**.
- The user can edit and delete a repayment; the outstanding amount and the state follow.
- The user can change the expected return date at any time, e.g. when the borrower asks for more
  time. The new date applies to the outstanding amount.

**FR-5 Overdue lendings**
- A lending whose expected return date is before today while something is still outstanding is
  **Overdue** and is highlighted wherever it is listed.

**FR-6 Borrowers**
- Borrowers come from the same list as the **payers** of inputs (see
  [input-cash-flow.md](input-cash-flow.md) FR-5). While typing the borrower, the user gets
  suggestions from the saved payers.
- A newly typed borrower is saved to the payers list automatically when the lending is saved, and
  is then also suggested as a payer of inputs.
- Borrowers are renamed and removed in the **Payers** section in Settings, like any payer. Renaming
  a payer also renames it on its lendings. Removing a payer from the list does not change lendings
  already recorded with it.
- The user can see, per borrower, the total lent, returned and outstanding.

### 4.2 Cash flow

**FR-7 Lent amount as an output**
- The lent amount is an **output** of its *From account* on the lend date: it is subtracted from
  that account's current balance from the lend date on (see [input-cash-flow.md](input-cash-flow.md)
  FR-10 and its Appendix 1).
- It is not an expense: it is not counted in the bills or expenses totals, nor in the cash
  withdrawal total.

**FR-8 Repayments as inputs**
- A received repayment is an **input** of its *To account* on its received date and is added to
  that account's current balance from then on.
- While a lending is not returned, its outstanding amount is an **expected input** of the
  lending's *From account* in the month of the expected return date. It counts in that account's
  expected end-of-month balance when the expected return date is in the current month.
- Repayments are shown with the borrower as payer, but they are not counted in the input
  categories: they are counted in their own **Lending** group (FR-9, FR-10).

**FR-9 Month screen**
- The **Month** screen shows the month's lendings and repayments in their own **Lending** list,
  separately from incomes and expenses: money lent in the month, repayments received in the month
  and expected returns due in the month (overdue ones highlighted).
- The list shows the month totals: lent, returned and expected back.

**FR-10 Cash flow dashboard and monthly report**
- On the **Cash flow** dashboard and in the **monthly cash flow report**
  ([input-cash-flow.md](input-cash-flow.md) FR-12, FR-13 and Appendix 1), money lent is shown as
  an output and repayments as inputs, each in its own **Lending** group, received and expected ones
  distinguished as for other inputs and outputs. They can be filtered by borrower.
- Lending counts in the **month result** (inputs minus outputs) like any other input and output.

### 4.3 Other screens

**FR-11 Dashboard**
- When anything is outstanding, the Dashboard shows the total outstanding amount, with the overdue
  amount highlighted, and opens the Lending screen when selected.

**FR-12 Backup and restore**
- Lendings and repayments are part of the household data that is exported and imported
  (see [backup-and-restore.md](backup-and-restore.md) FR-2).

## 5. Non-functional requirements

- **NFR-1 Exact amounts:** amounts, outstanding amounts and totals are stored, calculated and shown
  exactly to the cent, never rounded.
- **NFR-2 Responsive:** the Lending screen, the lending and repayment forms and the Month screen
  list work on phone and desktop.
- **NFR-3 Access:** only signed-in users of the household can see or change lendings.
- **NFR-4 Data protection:** borrower names and amounts are personal financial data. They never
  appear in logs or error messages, and test data uses made-up values only, as required by
  `CONTRIBUTING.md`.
- **NFR-5 Language:** every new text is in English and Croatian (see
  [croatian-language.md](croatian-language.md) FR-9). Croatian terms: *Posudbe* (Lending),
  *posudba* (lending), *dužnik* (borrower), *povrat* (repayment), *nepodmireno* (outstanding),
  *kasni* (overdue), *vraćeno* (returned).

## 6. Out of scope

- Interest on lent money, or a repayment of more than was lent.
- Money the user borrows from a person (only bank loans exist, see
  [loans-and-leases.md](loans-and-leases.md)).
- A repayment schedule with several planned instalments and their own dates.
- Lending in cash or with a credit card (no cash balance is tracked).
- Forgiving or writing off an outstanding amount.
- Reminders or notifications to the user or the borrower.
- Multiple currencies.

## 7. Acceptance criteria

1. The user can add a lending with borrower, amount, lend date, from account and expected return
   date, and optionally a description; it appears on the Lending screen as Open.
2. Saving is blocked, with a clear message, when a required field is missing, the amount is zero
   or negative, or the expected return date is before the lend date.
3. The lent amount is subtracted from the *From account* balance on the lend date and is not
   counted as an expense or a cash withdrawal.
4. Recording a repayment adds it to the *To account* balance on its received date and lowers the
   outstanding amount; a repayment larger than the outstanding amount is refused.
5. After repayments equal to the lent amount, the lending is Returned and is hidden from the default
   list but shown with the **Returned** filter.
6. A lending past its expected return date with an outstanding amount is shown as Overdue and
   highlighted on the Lending screen, the Month screen and the Dashboard.
7. The outstanding amount is an expected input in the month of the expected return date and counts
   in the expected end-of-month balance of the *From account*.
8. The Month screen, the Cash flow dashboard and the monthly report show lent amounts as outputs and
   repayments as inputs in their own Lending group, and the month result includes them.
9. Typing a borrower suggests saved payers; a new borrower is saved to the payers list
   automatically and can be renamed or removed in Settings, Payers.
10. Editing or deleting a lending or a repayment updates the list, totals, balances, chart and
    report.
11. An export followed by an import brings back all lendings and repayments.
12. All new texts are shown in Croatian when Croatian is chosen.
