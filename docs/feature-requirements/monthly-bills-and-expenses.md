# Requirements: Monthly bills and expenses

- **Branch:** `feat/monthly_bills_and_expenses`
- **Status:** Approved (2026-10-09)
- **Date:** 2026-10-09

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-monthly_bills_and_expenses.md`) is written after these
> requirements are approved.

## 1. Goal

Besides loans and card payments, the household pays monthly bills (electricity, gas, internet,
phone, gym membership, ...) and spends money on the go (groceries, a haircut, cash from an ATM).
This feature lets the user record both as **outputs** in the cash flow:

- **Bills** arrive every month. The user keeps a **recurring bill** for each regular bill and, with
  one action, adds them to a month as empty bills, then fills in the amount when each bill is
  issued and ticks it when it is paid.
- **Expenses** are one-time outputs recorded when they happen, e.g. a grocery shop.
- **Cash withdrawals** are a separate kind of expense.

A bill or an expense is paid either from an account or with a credit card. When it is paid with a
credit card, it becomes part of that card's card payment instead of a separate output, so it is
never counted twice.

## 2. Terms

| Term | Meaning |
|---|---|
| Bill | A bill the household receives and pays, e.g. for electricity or internet (Croatian: *režije*, *račun*). |
| Recurring bill | A template for a bill that arrives every month, without dates or amount (FR-2). |
| Expense | A one-time output recorded when it happens, e.g. a grocery shop (FR-7). |
| Cash withdrawal | Cash taken from an account, e.g. at an ATM (Croatian: *podizanje gotovine*). A separate kind of expense (FR-8). |
| Expense category | The kind of a bill, expense or card purchase, from one shared list managed in Settings (FR-1). |
| Issue date | The date the bill was issued (Croatian: *datum računa*). |
| Due date | The date by which the bill should be paid (Croatian: *datum dospijeća*). |
| Paid from | The account or credit card a bill or expense is paid with. |
| Credit card | In this document, a Credit or Charge card that has a statement date and a payment date (see [credit-card-installments.md](credit-card-installments.md) FR-1). A Debit card is always an account here, also when it allows monthly installments: a bill or expense paid with it is never paid in installments. |
| Card payment, Billing period | As in [credit-card-installments.md](credit-card-installments.md). |
| Output, Account, Primary account | As in [credit-card-settings.md](credit-card-settings.md), [input-cash-flow.md](input-cash-flow.md) and [loans-and-leases.md](loans-and-leases.md). |

## 3. Functional requirements

### 3.1 Bills

**FR-1 Expense categories**
- Bills, recurring bills and expenses use the same category list as card purchases. The Settings
  section **Purchase categories** becomes **Expense categories**, one shared list (see
  [Appendix 1 of credit-card-installments.md](credit-card-installments.md#appendix-1-shared-expense-categories)),
  so e.g. "Mobile phone" is the same category whether it is paid by bill, as an expense or as a
  card purchase.
- The list starts with:
  - for bills: **Electricity**, **Gas**, **Water and waste**, **Building reserve**, **Internet**,
    **Television**, **Telephone**, **Mobile phone**, **Gym membership** and **Other bills**;
  - for expenses: **Groceries**, **Personal care**, **Kindergarten**, **Household**,
    **Transport**, **Other expenses** and **Cash withdrawal** (FR-8).
  Categories the user already added for card purchases stay; a starting category whose name is
  already in the list (ignoring upper/lower case) is not added twice.
- Every category can be chosen for a bill, an expense or a card purchase.
- The user can add and rename categories. Names are unique, ignoring upper/lower case, and at most
  50 characters.
- A category used by a bill, a recurring bill, an expense or a card purchase cannot be deleted,
  only hidden: it is then no longer offered for new entries, and entries that use it keep it.
- **Cash withdrawal** is a fixed category: it is always in the list and cannot be renamed or
  hidden.
- A category is required on bills, recurring bills and expenses; on card purchases it stays
  optional.

**FR-2 Recurring bills**
- The user keeps a list of **recurring bills**, one for each bill that arrives regularly, with
  these attributes:

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| R1 | Name | Yes | E.g. "Electricity, apartment". Unique among active recurring bills, ignoring upper/lower case. Maximum 50 characters. |
| R2 | Category | Yes | Chosen from the expense categories (FR-1). |
| R3 | Payee | No | Who issues the bill, e.g. the electricity supplier. Maximum 100 characters. |
| R4 | Paid from | No | The account or credit card the bill is usually paid with (FR-5). Used to prefill its bills. |
| R5 | Description | No | Short free note. Maximum 200 characters. |

- A recurring bill has **no dates and no amount**: they are entered on each bill when it is issued.
- **Due date hint:** when the user issues a bill added from a recurring bill, the due date is
  prefilled with the same day of the month as the due date of the last issued bill of that
  recurring bill (the last day of the month when the month is shorter). It is only a hint and can
  be changed. The first bill of a recurring bill gets no hint.
- The user can add, edit and **stop** a recurring bill. A stopped recurring bill is no longer added
  to new months; bills already added from it stay unchanged. A recurring bill that has never been
  added to a month can be deleted.

**FR-3 Add recurring bills to a month**
- On the **Month** screen the user can choose **Add recurring bills**. It adds one bill for every
  active recurring bill to the chosen month, by default the next month.
- Each added bill takes its name, category, payee, paid from and description from its recurring
  bill, has an amount of **0** and no dates, and is shown as **Not issued**.
- A recurring bill that already has a bill in that month is skipped, so running the action again
  never adds a bill twice. The app says how many bills were added.

**FR-4 Bill attributes and lifecycle**

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| B1 | Name | Yes | Taken from the recurring bill, or typed for a one-off bill. Maximum 50 characters. |
| B2 | Category | Yes | Chosen from the expense categories (FR-1). |
| B3 | Payee | No | Maximum 100 characters. |
| B4 | Amount | When issued | Amount in EUR, exact to the cent. **0** until the bill is issued, then greater than zero. |
| B5 | Issue date | When issued | Date the bill was issued (*datum računa*). |
| B6 | Due date | When issued | Date the bill should be paid by. The same as or later than the issue date. |
| B7 | Paid | No (default: No) | The bill is paid (FR-5). |
| B8 | Paid date | When paid | Set to today when *Paid* is ticked and can then be changed. The same as or later than the issue date. |
| B9 | Paid from | When paid | The account or credit card the bill was paid with (FR-5). |
| B10 | Description | No | Short free note. Maximum 200 characters. |

- A bill is in one of three states:
  - **Not issued:** added from a recurring bill, amount 0, no dates.
  - **Expected:** issued (amount, issue date and due date entered), not yet paid.
  - **Paid.**
- When the bill arrives, the user enters its amount, issue date and due date, and the bill becomes
  expected. A bill cannot be marked as paid while it is not issued.
- The user can also add a **one-off bill** that has no recurring bill, directly as expected or
  paid.
- A bill belongs to the month it was added to. A paid bill counts in the cash flow on its paid
  date; an expected bill on its due date.
- An expected bill whose due date has passed is highlighted as overdue. A not issued bill is shown
  dimmed and is not counted in totals.
- The user can edit any attribute of a bill with the same rules as when adding it, and delete a
  bill after confirming in a dialog that names it. Deleting a bill added from a recurring bill
  removes it only from its month; the recurring bill stays. Cancelling leaves the bill unchanged.

**FR-5 Paying a bill**
- Ticking **Paid** sets the paid date to today and asks what it was paid with, prefilled from the
  bill's *Paid from*, or with the primary account when exactly one account is primary. Unticking
  returns the bill to expected and clears the paid date.
- The bill can be paid from:
  - **an account** that is not blocked, expired or archived: the bill is an output of that
    account, or
  - **a credit card** that is Active or Expiring soon (see
    [credit-card-settings.md](credit-card-settings.md) FR-11): the bill is a charge on that card
    (FR-6).

**FR-6 Bills paid with a credit card**
- A bill paid with a credit card goes into that card's **card payment**, the same way as a card
  purchase without installments: it belongs to the billing period that contains its paid date (see
  [credit-card-installments.md](credit-card-installments.md) FR-4) and is paid with that card
  payment.
- It is therefore **not** a separate output of an account: it appears in the cash flow, the account
  balance and the Month screen only as part of the card payment, so it is never counted twice.
- It is shown in the card's details and in the card payment drill-down, together with the card's
  purchases, with its name, category, paid date and amount, marked as a bill.
- In the Month screen the bill stays in the bills list, marked as paid with that card, but its
  amount is counted in the card payment, not in the bills total.
- When such a bill is edited, unticked or deleted, its card payment changes accordingly; when the
  card payment's payment date has already passed, the confirmation says so, as for card purchases
  (see [credit-card-installments.md](credit-card-installments.md) FR-6).

### 3.2 Expenses

**FR-7 Record an expense**
- The user records an expense when it happens, e.g. a grocery shop, with these attributes:

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| E1 | Date | Yes | Date of the expense. Prefilled with today. |
| E2 | Amount | Yes | Amount in EUR, exact to the cent, greater than zero. |
| E3 | Category | Yes | Chosen from the expense categories (FR-1), except Cash withdrawal (FR-8). |
| E4 | Paid from | Yes | An account that is not blocked, expired or archived (e.g. a primary account or a debit card), or a credit card that is Active or Expiring soon. Prefilled with the last one used, or with the primary account when exactly one account is primary. |
| E5 | Description | No | What or where, e.g. "Konzum". Maximum 100 characters. |

- An expense is always **paid**: it has no expected state.
- An expense paid from an account is an output of that account on its date.
- An expense paid with a credit card is added to that credit card: it is listed in the card's
  details and goes into its card payment, exactly as a bill paid with a credit card (FR-6), marked
  as an expense. In the account balance it counts only within that card payment.
- Adding an expense is quick: the form fits on one phone screen and can be saved with the
  prefilled values plus amount and category.
- The user can edit and delete an expense with the same rules as a bill (FR-4, FR-6).

**FR-8 Cash withdrawals**
- A **cash withdrawal** is a separate kind of expense: the user records the date, the amount, the
  account it was taken from and an optional description.
- It can only be taken from a **primary account** that is not blocked, expired or archived (not
  from another account or a credit card). Prefilled when exactly one account is primary.
- Cash withdrawals are listed and totalled separately from other expenses in the Month screen and
  the monthly report, so the user sees how much cash was taken out. What the cash is then spent on
  is not tracked.

### 3.3 Presentation

**FR-9 Month screen**
- The Expenses part of the **Month** screen gets a **Bills** list and an **Expenses** list, next to
  loan payments and card payments.
- Bills show: name, category, amount, issue date, due date, paid date, paid from and state. The
  list shows totals for the month: expected, paid and all bills (not issued bills count as 0, bills
  paid with a credit card are in their card payment).
- Expenses show: date, category, description, amount and paid from, with the month total and the
  cash withdrawal total.
- The user can add bills, recurring bills to the month (FR-3) and expenses from there.

**FR-10 Account balance, cash flow chart and monthly report**
- Bills and expenses paid from an account count in the account balance the same way as loan
  payments (see [loans-and-leases.md](loans-and-leases.md) FR-11): paid ones are subtracted from
  the balance of the account they were paid from, and expected bills due by the end of the month
  from its expected end-of-month balance. An expected bill without *Paid from* is not counted.
- They are outputs on the **Cash flow** dashboard and in the **monthly cash flow report** (see
  [input-cash-flow.md](input-cash-flow.md) FR-12 and FR-13 and its Appendix 1), grouped by
  expense category, and can be filtered by expense category.
- Bills and expenses paid with a credit card appear there only as part of their card payment.

## 4. Non-functional requirements

- **NFR-1 Exact amounts:** amounts and totals are stored, calculated and shown exactly to the cent,
  never rounded.
- **NFR-2 Responsive:** the bill and expense forms, the recurring bills list and the Month screen
  lists work on phone and desktop; adding an expense on a phone takes only a few taps.
- **NFR-3 Access:** only signed-in users of the household can see or change bills and expenses.
- **NFR-4 Data protection:** bills, payees and expenses are personal financial data. They never
  appear in logs or error messages, and test data uses made-up values only, as required by
  `CONTRIBUTING.md`.
- **NFR-5 Language:** UI text in English for now; field labels must be easy to translate to
  Croatian later.

## 5. Out of scope

- Sharing a bill with someone else (e.g. 50 % of shared utilities).
- Paying a bill in installments, or a bill paid partly from an account and partly with a card.
- Tracking what cash from a withdrawal is spent on, and a cash balance.
- Recurring expenses (regular outputs are recurring bills).
- Importing bills or expenses from a bank, an e-bill service or a receipt.
- Reminders or notifications for due bills.
- Multiple currencies.

## 6. Acceptance criteria

1. Settings has one Expense categories list, used by card purchases, bills and expenses, that
   starts with the bill and expense categories of FR-1 and keeps categories already added for card
   purchases.
2. The user can add and rename expense categories; a duplicate name (ignoring upper/lower case) or
   a name longer than 50 characters is refused; a used category can only be hidden, and Cash
   withdrawal can be neither renamed nor hidden.
3. The user can add, edit, stop and (when never used) delete recurring bills, which have no dates
   or amount.
4. **Add recurring bills** adds one Not issued bill with amount 0 for each active recurring bill to
   the chosen month (the next month by default), and running it again adds nothing new.
5. When a bill from a recurring bill is issued, its due date is prefilled with the due day of the
   last issued bill of that recurring bill.
6. A Not issued bill becomes Expected when its amount, issue date and due date are entered, and
   cannot be marked as paid before that.
7. Ticking Paid records today and what the bill was paid with; unticking returns it to Expected.
   An expected bill past its due date is highlighted.
8. A bill or expense paid with a credit card is added to the card payment of the billing period
   that contains its date, appears in the card details and drill-down, and is not counted again as
   a separate output.
9. An expense is recorded with date, amount, category and paid from (an account, e.g. a debit
   card, or a credit card), and is always paid.
10. A cash withdrawal can only be recorded from a primary account and is totalled separately.
11. The Month screen shows bills and expenses with their totals, and the Dashboard balances, the
    Cash flow dashboard and the monthly report include bills and expenses paid from an account.

## Appendix 1: Shared bills

- **Date:** 2026-10-09
- **Branch:** `feat/shared_bills`
- **Changes:** section 5 (Out of scope)

Sharing a bill with someone else is no longer out of scope. A recurring bill or a bill can now be
shared with one participant, who pays back a part of it as an agreed contribution, as described in
[shared-bills.md](shared-bills.md).

- The out-of-scope item "Sharing a bill with someone else (e.g. 50 % of shared utilities)" no
  longer applies.
- The bill itself is unchanged: the household still pays its full amount, and it counts in full in
  the cash flow, the account balance and the card payment. The participant's part is a separate
  input.
- The Month screen also shows the own part of a shared bill and of the bills totals (see
  [shared-bills.md](shared-bills.md) FR-7).
