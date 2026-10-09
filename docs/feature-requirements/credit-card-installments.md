# Requirements: Credit card installments

- **Branch:** `feat/credit_card_installments`
- **Status:** Approved (2026-10-08)
- **Date:** 2026-10-08

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-credit_card_installments.md`) is written after these
> requirements are approved.

## 1. Goal

The user buys things with a card, paid in full or in monthly installments (Croatian: *kupnja na
rate*). Each card collects its purchases and installments over a billing period and charges them
in one card payment, from a paying account, on a known date. This feature lets the user record
card purchases, reverse, edit or delete them, and see for every month what each card will charge
and when.

Every card that allows monthly installments gets a statement date and a payment date, which define
its billing period, and a paying account. Each month's card payment is an output in the cash flow,
and the user can drill down from it to the purchases it contains.

## 2. Terms

| Term | Meaning |
|---|---|
| Card | A card registered in Cards and accounts settings (see [credit-card-settings.md](credit-card-settings.md)). |
| Card purchase | A purchase paid with a card, either in full or in monthly installments. |
| Installment purchase | A card purchase paid in a number of monthly installments (*kupnja na rate*). |
| Installment | One monthly part of an installment purchase (*rata*). |
| Statement date | The day of the month the card's bill is made (Croatian: *datum obračuna* / *datum izvoda*). Everything charged to the card up to and including this day is collected into the next card payment. |
| Payment date | The day of the month the card payment for a statement is expected (Croatian: *datum dospijeća* / *datum plaćanja*). |
| Billing period | The days from the day after one statement date up to and including the next statement date. |
| Card payment | The one amount the card charges for a billing period, paid on the payment date. |
| Paying account | The primary account card payments are paid from. |
| Reversal | Cancelling a purchase or an installment (*storno*), e.g. when goods are returned. |
| Output, Account, Primary account | As in [credit-card-settings.md](credit-card-settings.md), [input-cash-flow.md](input-cash-flow.md) and [loans-and-leases.md](loans-and-leases.md). |

## 3. Functional requirements

### 3.1 Card billing details

**FR-1 Billing dates and paying account on the card**
- Every card that allows monthly installments has these attributes: Credit and Charge cards
  always, and Debit cards when *Allows monthly installments* is ticked (see
  [credit-card-settings.md](credit-card-settings.md) A11). Prepaid cards never have them.

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| B1 | Statement date | Yes | Day of the month, 1 to 30. Everything charged to the card up to and including this day belongs to the billing period that ends on it and is collected into its card payment. |
| B2 | Payment date | Yes | Day of the month, 2 to 31, later than the statement date. The card payment for a statement is expected on this day **in the same month** as the statement (e.g. statement on the 5th, payment on the 20th). |
| B3 | Paying account | Yes | The primary account the card payments are paid from. Prefilled when exactly one account is primary; with several, the user chooses. |
| B4 | Connected to the paying account | No (default: No) | Ticked when the bank charges the card payment directly from the paying account. Decides whether card payments are paid automatically or by the user (FR-8). |
| B5 | Max installments | Yes | The highest number of installments the card allows for one purchase: **12**, **24** or **36**. |

- In a month that has fewer days, a date of 29, 30 or 31 falls on the last day of that month.
  When both dates fall on the same last day, the payment is expected on that day.
- When a payment date falls on a Saturday or Sunday, the payment is expected on the following
  Monday, except when that Monday is in the next month: then it stays on its day, so the payment
  stays in its own month (the same rule as loan bills in
  [loans-and-leases.md](loans-and-leases.md)). The statement date does not move.
- When *Allows monthly installments* is unticked on a Debit card, the card no longer has these
  attributes.
- When there is no primary account yet, the form says so and links to Cards and accounts in
  Settings.
- The user sets these attributes when adding or editing the card, with the same rules as the other card
  attributes (required fields marked, errors shown next to the field, cancel saves nothing).
- They are shown on the card's details and in the Settings list.

**FR-2 Changing the billing dates**
- The user can change the statement date and the payment date of a card at any time (banks
  sometimes move them), with the same rules as in FR-1.
- A change applies from the **next payment month** on: the card payment of the current month, and
  of every earlier month, keeps its old statement and payment dates.
- The first billing period under the new dates runs from the day after the last statement date
  under the old dates up to and including the new statement date, so no charge is left out of a
  card payment or counted twice.

### 3.2 Card purchases

**FR-3 Record a card purchase**
- The user can record a purchase made with a card, either in monthly installments or without
  installments (paid in full with one card payment), with these attributes:

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| P1 | Card | Yes | A card that allows monthly installments and has status Active or Expiring soon (see [credit-card-settings.md](credit-card-settings.md) FR-11). |
| P2 | Description | Yes | What was bought or where, e.g. "Washing machine, Links". Maximum 100 characters. |
| P3 | Purchase date | Yes | The date of the purchase. Not before the card's *Valid from*, when given. |
| P4 | Original amount | Yes | The full purchase amount in EUR, exact to the cent, greater than zero. |
| P5 | In installments | Yes (default: No) | Whether the purchase is paid in monthly installments. When not, the whole amount is charged with one card payment. |
| P6 | Number of installments | For installments | Whole number, from 2 up to the card's *Max installments* (B5). |
| P7 | Postpone first installment | No (default: No) | For installments only. When ticked, the first installment is moved one billing period later (FR-4). |
| P8 | Category | No | Chosen from the purchase categories managed in Settings (FR-10). |

- Required fields are clearly marked, each error is shown next to its field, and cancelling saves
  nothing.

**FR-4 Purchases on future card payments**
- A purchase without installments is charged in full with the card payment of the billing period
  that contains the purchase date (FR-1).
- For a purchase in installments, the original amount is split into the given number of installments,
  one per month, and each installment is added to a card payment of that card.
- The first installment belongs to the billing period that contains the purchase date (FR-1), or
  to the billing period after it when *Postpone first installment* is ticked. Each next
  installment belongs to the following billing period.
- All installments are equal, exact to the cent; when the amount cannot be split evenly, the
  remaining cents are added to the last installment, so the installments always add up to the
  original amount.
- Each installment shows its number (e.g. 3 of 12), amount, statement date and payment date of
  its card payment.
- The purchase shows its original amount, the installments already charged, and the remaining
  amount.
- Purchases are interest free: the installments always add up to the original amount.

**FR-5 Reverse a purchase or an installment**
- The user can **reverse** a whole purchase (e.g. the goods were returned) or a single installment
  of it (e.g. the bank cancelled one charge).
- Reversing asks for confirmation in a dialog that names the purchase (and the installment), and
  records the reversal date and an optional reason.
- A reversed installment, or every installment of a reversed purchase, is removed from its card
  payment when that card payment's payment date has not passed yet.
- An installment whose card payment date has already passed is credited back: its amount is
  subtracted from the next card payment of that card.
- A reversed purchase or installment is not deleted: it stays visible with its reversal date and
  reason, marked **Reversed**, and is no longer counted in the remaining amount.
- The same applies to a purchase without installments, which has one charge.

**FR-6 Edit or delete a purchase**
- The user can edit any attribute of a purchase that is not reversed, with the same rules as when
  recording it. Saving recalculates its installments and the card payments they belong to.
- The user can delete a purchase, e.g. one entered by mistake. Deleting asks for confirmation in a
  dialog that names the purchase and removes the purchase with all its installments from the card
  payments.
- When the purchase already has charged installments, the confirmation of an edit or delete says
  that card payments whose payment date has passed will change.
- Cancelling an edit or a delete leaves the purchase unchanged.

### 3.3 Purchases on the card and in the cash flow

**FR-7 Purchases on the card**
- Every purchase is added to its card. The card's details show its purchases (with installments,
  remaining amounts and reversals) and its card payments by month.
- Each card payment shows its billing period, statement date, payment date and total amount.

**FR-8 Card payment in the cash flow**
- The card payment of each month is an **output** (outgoing cash flow) in that month, on its
  payment date (FR-1), and is shown on the existing **Cash flow** dashboard and in the **monthly
  cash flow report** (see [input-cash-flow.md](input-cash-flow.md) FR-12 and FR-13), the same way
  as loan payments (see [loans-and-leases.md](loans-and-leases.md) FR-12).
- The amount of a card payment is the sum of everything in its billing period: purchases without
  installments, installments, minus credits from reversals (FR-5).
- The user can **drill down** from a card payment in the cash flow to its details: every purchase,
  installment and credit of that card in that billing period, with description, purchase date,
  installment number (e.g. 3 of 12), category and amount. From there the user can open the
  purchase.
- On the **Month** screen, card payments appear as expenses: one summed-up line per card with its
  card payment for that month, not the single purchases. Selecting the line opens the card's
  details for that month (the drill-down above).
- How a card payment is paid depends on the card (B4):
  - **Connected to the paying account:** the card payment is paid automatically from the paying
    account at the end of its payment date, without any action from the user; until then it is
    expected. The user can correct or untick it.
  - **Not connected:** the card payment stays expected until the user ticks **Paid**, which sets
    the paid date to today and records the account it was paid from, prefilled with the paying
    account. Unticking reverses this. It can be marked as paid before, on or after its payment
    date.
- A card payment that is overdue (payment date passed and not paid) is highlighted.

**FR-9 Account balance**
- Card payments count in the account balance the same way as loan payments (see
  [loans-and-leases.md](loans-and-leases.md) FR-11): paid card payments are subtracted from the
  balance of the account they were paid from, and expected card payments due by the end of the
  month from the account's expected end-of-month balance on the Dashboard.

### 3.4 Settings: purchase categories

**FR-10 Purchase categories**
- The Settings screen gets a **Purchase categories** list, used for the optional category of a
  purchase (P8).
- The user can add and rename categories. Names are unique, ignoring upper/lower case, and at most
  50 characters.
- A category used by a purchase cannot be deleted, only hidden: it is then no longer offered for
  new purchases, and purchases that use it keep it.

## 4. Non-functional requirements

- **NFR-1 Exact amounts:** purchase amounts, installments, card payments and remaining amounts are
  stored, calculated and shown exactly to the cent, never rounded beyond what FR-4 describes.
- **NFR-2 Responsive:** the purchase form, the card details with purchases and card payments, and
  the card payment drill-down work on phone and desktop.
- **NFR-3 Access:** only signed-in users of the household can see or change purchases and card
  payments.
- **NFR-4 Data protection:** purchases are personal financial data. Card numbers and IBANs stay
  masked as in [credit-card-settings.md](credit-card-settings.md) NFR-1, never appear in logs or
  error messages, and test data uses made-up values only.
- **NFR-5 Language:** UI text in English for now; field labels must be easy to translate to
  Croatian later.

## 5. Out of scope

- Fees and interest on purchases; every purchase is interest free.
- Reversing only part of a purchase or installment amount, and choosing which card payment a
  credit goes to.
- Undoing a reversal.
- Purchases on cards that do not allow monthly installments (e.g. Prepaid cards, or Debit cards
  without the check box), and other outputs such as utilities and variable expenses.
- Card limits: the debt and remaining limit per card (see
  [credit-card-settings.md](credit-card-settings.md) FR-12).
- Importing purchases or card statements from a bank, and reconciling them with the bank.
- Multiple currencies.

## 6. Acceptance criteria

1. A Credit or Charge card, or a Debit card with *Allows monthly installments* ticked, cannot be
   saved without a statement date, a payment date, a paying account and max installments (12, 24
   or 36); other cards do not offer them.
2. The payment date must be later than the statement date; a date of 29 to 31 falls on the last
   day of shorter months, and a payment date on a Saturday or Sunday moves to the following Monday
   unless that Monday is in the next month.
3. With exactly one primary account, the paying account is prefilled with it; with several, the
   user must choose.
4. Changing a card's billing dates changes card payments from the next payment month on, and leaves
   the current and earlier ones unchanged; no charge is left out or counted twice.
5. A purchase is not in installments by default and is then charged in full with the card payment
   of the billing period that contains its purchase date.
6. A purchase of 100.00 EUR in 3 installments gives installments of 33.33, 33.33 and 33.34 EUR in
   three consecutive card payments, starting with the billing period of the purchase date, or one
   period later when *Postpone first installment* is ticked.
7. A purchase can only be recorded on a card that allows monthly installments and is Active or
   Expiring soon, and cannot be saved with a missing required field, an amount of zero or less, or
   fewer than 2 installments or more than the card's max installments.
8. Reversing a purchase or an installment removes it from card payments not yet due, subtracts
   already charged amounts from the next card payment, and keeps it visible as **Reversed** with
   its date and reason.
9. Editing a purchase recalculates its installments; deleting it removes it and all its
   installments; both warn when card payments whose payment date has passed will change.
10. The card details list the card's purchases and its card payments by month.
11. Each month's card payment appears as an output on its payment date on the Cash flow dashboard
    and in the monthly cash flow report, and its amount is the sum of that billing period's
    purchases and installments minus credits.
12. Drilling down from a card payment shows every purchase, installment and credit in it, and
    opens the purchase. On the Month screen each card payment is one summed-up expense line per
    card, which opens that card's details for the month.
13. A card payment of a card connected to its paying account is paid automatically at the end of
    its payment date; for other cards it stays expected until the user ticks **Paid**, which
    records today and the account (prefilled with the paying account); unticking reverses it.
14. An unpaid card payment past its payment date is highlighted.
15. Paid card payments lower the balance of the account they were paid from, and expected card
    payments due this month lower its end-of-month expected balance on the Dashboard.
16. The user can add and rename purchase categories in Settings; a used category can only be
    hidden, and a purchase can be saved without a category.

## Appendix 1: Shared expense categories

- **Date:** 2026-10-09
- **Branch:** `feat/monthly_bills_and_expenses`
- **Changes:** FR-10, acceptance criterion 16

The purchase categories become one shared list of **expense categories**, used by card purchases
and by bills and expenses (see [monthly-bills-and-expenses.md](monthly-bills-and-expenses.md)
FR-1), so e.g. "Mobile phone" is the same category whether it is paid by bill, as an expense or as
a card purchase.

- The Settings section **Purchase categories** is renamed **Expense categories**. Categories the
  user already added stay, with their names and hidden state.
- The list gets the starting categories described in
  [monthly-bills-and-expenses.md](monthly-bills-and-expenses.md) FR-1; a starting category whose
  name is already in the list (ignoring upper/lower case) is not added twice.
- A category used by a purchase, a bill, a recurring bill or an expense cannot be deleted, only
  hidden.
- The category of a card purchase (P8) stays optional.

Acceptance criterion 16 becomes: the user can add and rename expense categories in Settings; the
same list is offered for card purchases, bills and expenses; a used category can only be hidden,
and a purchase can be saved without a category.
