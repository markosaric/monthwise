# Requirements: Revolving credit card

- **Branch:** `claude/project-thread-no7dqr`
- **Status:** Approved (2026-11-03)
- **Date:** 2026-11-03

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/claude-project-thread-no7dqr.md`) is written after these
> requirements are approved.

## 1. Goal

A revolving credit card (Croatian: *revolving kartica*) lets the holder spend up to a limit and
pay the debt back in parts: each month the bank asks only for a **minimum payment**, a percentage
of the whole debt with a fixed lowest amount, e.g. *"Pay at least 8.33 % of the total amount (or
13.27 EUR when 8.33 % is less than 13.27 EUR)"*. Interest is charged on the debt that is left.

Unlike a Credit or Charge card, a revolving card has **no billing periods and no card payments**
that collect purchases and installments (see [credit-card-installments.md](credit-card-installments.md)).
It has a **debt**, and as long as there is debt, a **monthly bill** for the minimum payment comes
every month.

This feature lets the user register a revolving card, see its debt and remaining limit, get the
monthly minimum payment bill in the cash flow, and pay the debt down at any time with an **extra
payment** from one of their accounts (e.g. from the current account, *tekući*, to the revolving
card).

## 2. Terms

| Term | Meaning |
|---|---|
| Revolving card | A card of the new type **Revolving** (FR-1). |
| Debt | The amount owed on the revolving card on a given day (Croatian: *dug*, *iskorišteni iznos*). Shown as a negative balance of the card. |
| Charge | Anything paid with the revolving card that raises its debt: a bill, an expense or a card purchase paid with it (FR-6). |
| Interest | The amount the bank adds to the debt each month for the debt that is left (Croatian: *kamata*). |
| Bill day | The day of the month the bank makes the monthly bill. |
| Due day | The day of the month by which the monthly bill must be paid. |
| Monthly bill | The bill for the minimum payment of one month (FR-4). |
| Minimum payment | The lowest amount that must be paid with a monthly bill (FR-3). |
| Extra payment | A payment from an account to the revolving card, made at any time, that lowers the debt (FR-8). |
| Remaining limit | The max debt limit minus the debt. |
| Account, Primary account, Output | As in [credit-card-settings.md](credit-card-settings.md), [input-cash-flow.md](input-cash-flow.md) and [loans-and-leases.md](loans-and-leases.md). |
| Credit card, Card payment, Billing period | As in [credit-card-installments.md](credit-card-installments.md) and [monthly-bills-and-expenses.md](monthly-bills-and-expenses.md). A revolving card is **not** a credit card in this sense. |

## 3. Functional requirements

### 3.1 The revolving card

**FR-1 Revolving card type**
- The card *Type* (see [credit-card-settings.md](credit-card-settings.md) A8) gets a fifth value:
  **Revolving**.
- A revolving card has all the general card attributes of
  [credit-card-settings.md](credit-card-settings.md) section 3 (custom name, issuer, owner, card
  number, IBAN, valid from, valid through, max debt limit) and follows the same rules for adding,
  editing, viewing, blocking, expiring, archiving and card status (FR-1 to FR-11 there).
- A revolving card cannot be a primary account, and does not have the billing details of a credit
  card (statement date, payment date, max installments; see
  [credit-card-installments.md](credit-card-installments.md) FR-1).
- The *Max debt limit* (A10) is **required** for a revolving card.
- The type of a card that has a debt, charges or payments cannot be changed to or from Revolving.

**FR-2 Revolving terms**
- A revolving card has these additional attributes:

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| R1 | Minimum payment percentage | Yes | Percentage of the debt to pay each month, e.g. 8.33 %. Greater than 0 and at most 100, with up to two decimals. |
| R2 | Minimum payment amount | Yes | The lowest minimum payment in EUR, exact to the cent, e.g. 13.27 EUR. Zero or greater. |
| R3 | Interest rate | Yes | Yearly regular interest rate in percent, e.g. 12.75 %, with up to two decimals. Zero or greater. Used for the interest (FR-7). |
| R3a | Minimum payment interest rate | No | The part of the minimum payment percentage that is interest, in percent, as stated by the bank (Croatian: *minimalna kamatna stopa za plaćanje*). With up to two decimals, zero or greater and not more than R1. For information only: shown on the card, not used in any calculation. |
| R4 | Bill day | Yes | Day of the month, 1 to 31, on which the monthly bill is made. |
| R5 | Due day | Yes | Day of the month, 1 to 31, by which the monthly bill must be paid. When it is not later than the bill day, it falls in the next month. |
| R6 | Paying account | Yes | The account the monthly bills are usually paid from; a Debit account that is not blocked, expired or archived. Prefilled when exactly one account is primary. |
| R7 | Connected to the paying account | No (default: No) | Ticked when the bank charges the monthly bill directly from the paying account (standing order). |
| R8 | Starting debt | No | The debt on a given date, entered once when the card is added, e.g. for a card already in use (FR-5). |

- Days 29 to 31 fall on the last day of shorter months. A due day on a Saturday or Sunday moves to
  the following Monday, except when that Monday is in the next month (the same rule as credit card
  payment dates in [credit-card-installments.md](credit-card-installments.md) FR-1).
- The user sets the terms when adding or editing the card. A change of R1 to R5 applies from the
  next monthly bill on; bills already made keep their values.
- The terms are shown on the card's details and in the Settings list.

**FR-3 Minimum payment**
- The minimum payment of a monthly bill is the **greater** of:
  - the minimum payment percentage (R1) of the debt on the bill day, rounded to the cent, and
  - the minimum payment amount (R2),
- but never more than the whole debt on the bill day.
- Example: with 8.33 % and 13.27 EUR, a debt of 1,000.00 EUR gives 83.30 EUR, and a debt of
  100.00 EUR gives 13.27 EUR (8.33 EUR is less), and a debt of 10.00 EUR gives 10.00 EUR.

### 3.2 Debt and monthly bills

**FR-4 Monthly bill**
- On every bill day on which the card has a debt, the card gets a **monthly bill**. When the debt
  on the bill day is zero, no bill is made for that month.
- The monthly bill shows: the card, the debt on the bill day, the interest added that month (FR-7),
  the minimum payment, the bill date, the due date, the paid amount, the paid date and the account
  it was paid from.
- Bills keep coming while there is debt, also when the card is blocked, expired or marked as
  expired: debt on such a card is an ongoing transaction (see
  [credit-card-settings.md](credit-card-settings.md) section 2). FR-9 covers a new card issued
  for the same debt.
- The user can correct the minimum payment and the interest of one monthly bill to match the bank's
  bill. A corrected bill is visibly marked.
- Bills of future months are **estimated** from today's debt, assuming only the minimum payment is
  paid and no new charges are made, and are shown as estimated until their bill day.

**FR-5 Debt**
- The debt of a revolving card on a day is its starting debt (R8), plus charges (FR-6) and interest
  (FR-7) up to that day, minus paid monthly bills and extra payments (FR-8) up to that day, adjusted
  by debt corrections.
- The user can record a **debt correction** on any date, entering the debt shown by the bank; the
  app shows the calculated debt and records the difference, the same way as a balance correction
  (see [input-cash-flow.md](input-cash-flow.md) FR-9).
- The debt is never below zero: a payment cannot be larger than the debt on its date.
- The card shows its debt, max debt limit and remaining limit. When the debt is over the limit, the
  card is highlighted.

**FR-6 Charges on the revolving card**
- A bill, an expense or a card purchase without installments can be paid with a revolving card that
  is Active or Expiring soon, in the same places where a credit card can be chosen (see
  [monthly-bills-and-expenses.md](monthly-bills-and-expenses.md) FR-5 and FR-7 and
  [credit-card-installments.md](credit-card-installments.md) FR-3).
- A charge raises the debt on its date (the paid date of a bill, the date of an expense, the
  purchase date of a purchase). It is **not** an output of an account: money leaves an account
  only when the debt is paid (FR-8), so nothing is counted twice.
- A charge that would make the debt larger than the max debt limit shows a warning but can be saved.
- Charges are listed on the card's details with their date, description, category and amount,
  marked as bill, expense or purchase. Editing, unticking or deleting a charge changes the debt
  accordingly.
- A revolving card does not allow purchases in installments: every purchase is paid in full and
  becomes debt on the card. *In installments* (see
  [credit-card-installments.md](credit-card-installments.md) P5) is not offered for it.

**FR-7 Interest**
- On each bill day, the app adds the interest for the past month to the debt: the debt on the bill
  day × interest rate (R3) ÷ 12, rounded to the cent. The interest
  is part of the debt the minimum payment is calculated from, and is shown on the monthly bill.
- The user can correct the interest of a monthly bill to the amount the bank charged (FR-4).

### 3.3 Paying the debt

**FR-8 Paying a monthly bill and extra payments**
- **Monthly bill:** paid the same way as a loan payment (see
  [loans-and-leases.md](loans-and-leases.md) FR-5): automatically from the paying account at the
  end of its due date when the card is connected to it (R7), otherwise when the user ticks **Paid**,
  which sets the paid date to today and asks for the account, prefilled with the paying account.
  The paid amount is prefilled with the minimum payment and can be raised up to the debt.
- **Extra payment:** the user can create an extra payment at any time, from the card's details, the
  Month screen or the quick actions, with:

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| X1 | Revolving card | Yes | The card the debt is paid to. |
| X2 | From account | Yes | A Debit account that is not blocked, expired or archived. Prefilled with the card's paying account. |
| X3 | Amount | Yes | EUR, exact to the cent, greater than zero and not more than the debt on its date. A **Pay the whole debt** action fills in the debt. |
| X4 | Date | Yes | Prefilled with today; today or earlier, not in the future. |
| X5 | Description | No | Maximum 100 characters. |

- An extra payment is always **paid** on its date: it has no expected state and cannot be planned.
- Both lower the debt on their paid date. An extra payment only lowers the debt, and with it the
  minimum payment of the **next** monthly bill. It does not count towards a monthly bill that is
  already made: that bill is still paid on its own.
- Both are **outputs** of the account they were paid from and count in its balance the same way as
  loan payments (see [loans-and-leases.md](loans-and-leases.md) FR-11).
- An unpaid monthly bill past its due date is highlighted as overdue. Late fees and default
  interest are not calculated (section 5).
- The user can edit and delete an extra payment, and untick or correct a paid monthly bill, with
  the same confirmations as for loan payments.

### 3.4 Card renewal and replacement

**FR-9 Debt moves to a new card**
- When the bank renews the card with new dates, the user can simply update *Valid from* and
  *Valid through* on the existing card; its debt, terms and history stay unchanged.
- When the bank issues a new card for the same debt, the user marks the old card as expired (or
  blocks it) with the new card as its **replacement** (see
  [credit-card-settings.md](credit-card-settings.md) FR-7 and FR-8). The replacement must be a
  revolving card.
  - The debt of the old card on that date moves to the replacement card. Both cards show the
    transfer, and the old card's history stays visible.
  - From then on monthly bills, charges and extra payments are on the replacement card; the old
    card is inactive and has no debt.
- A revolving card with debt **cannot be archived**. The archive action says so and suggests a
  replacement card or paying the whole debt.

### 3.5 Presentation

**FR-10 Cards screen and card details**
- In the Cards screen (see [design-refresh.md](design-refresh.md) FR-12), each revolving card shows
  its debt, max debt limit, remaining limit, and the next monthly bill (minimum payment and due
  date). A key figure **Revolving debt** shows the debt of all revolving cards together.
- The card's details show its terms, its debt over time, its charges, its monthly bills and its
  extra payments by month, and the debt corrections.

**FR-11 Month screen, cash flow and monthly report**
- On the **Month** screen, monthly bills and extra payments appear as expenses, next to card
  payments, one line each, with the expected, paid and total amounts of the month.
- They are outputs on the **Cash flow** dashboard and in the **monthly cash flow report** (see
  [input-cash-flow.md](input-cash-flow.md) FR-12 and FR-13), the same way as card payments.
- Selecting a monthly bill opens the card's details for that month.

## 4. Non-functional requirements

- **NFR-1 Exact amounts:** debts, charges, interest, minimum payments and payments are stored,
  calculated and shown exactly to the cent, never with floating point; rounding happens only where
  FR-3 and FR-7 say so.
- **NFR-2 Responsive:** the card form, the card details and the extra payment form work on phone and
  desktop; an extra payment on a phone takes only a few taps.
- **NFR-3 Access:** only signed-in users of the household can see or change revolving cards, their
  debt and payments.
- **NFR-4 Data protection:** card numbers and IBANs stay masked as in
  [credit-card-settings.md](credit-card-settings.md) NFR-1; debts and payments never appear in logs
  or error messages, and test data uses made-up values only.
- **NFR-5 Language:** UI text in English for now; field labels must be easy to translate to Croatian
  later.

## 5. Out of scope

- Late fees, default interest (*zatezna kamata*), card fees and annual membership fees.
- Cash withdrawals with a revolving card.
- Planned (future-dated) extra payments.
- Importing bills or transactions from the bank.
- Changing a Credit or Charge card into a revolving card with its history.
- Reports and charts of the debt over time beyond the card's details, and a pay-off forecast.
- Sharing a revolving card's bills with participants (see [shared-bills.md](shared-bills.md)).
- Multiple currencies.

## 6. Acceptance criteria

1. The user can add a card of type **Revolving** with the general card attributes, a required max
   debt limit and the required revolving terms (R1 to R7, with R3a and R8 optional); it cannot be saved without them, and it offers no
   statement date, payment date, max installments or primary mark.
2. With 8.33 % and 13.27 EUR, the minimum payment is 83.30 EUR for a debt of 1,000.00 EUR, 13.27 EUR
   for 100.00 EUR, and 10.00 EUR for 10.00 EUR.
3. A card with debt on its bill day gets a monthly bill for that month; a card without debt does not.
4. A bill, expense or purchase paid with a revolving card raises its debt and is not an output of
   an account.
5. A paid monthly bill or extra payment lowers the debt and the balance of the account it was paid
   from, and appears as an output on the Month screen, the Cash flow dashboard and the monthly
   report.
6. An extra payment can be made on any day up to today for any amount up to the debt, and is
   recorded as paid; it cannot have a future date. **Pay the whole debt** brings the debt to zero,
   and no further monthly bills come until new charges are made.
7. An unpaid monthly bill past its due date is highlighted.
8. A debt correction makes the card's debt match the amount entered.
9. The Cards screen shows each revolving card's debt, remaining limit and next monthly bill.
10. A blocked or expired revolving card with debt still gets monthly bills.
11. An extra payment made after a monthly bill is made lowers only the next bill's minimum payment;
    the bill already made keeps its minimum payment.
12. A purchase on a revolving card cannot be made in installments.
13. A revolving card with debt cannot be archived. Marking it as expired with a revolving
    replacement card moves its debt to the replacement, and the next monthly bill comes on the
    replacement card.
14. The minimum payment interest rate (R3a) is shown on the card and changes no amount.
