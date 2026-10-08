# Requirements: Loans and leases

- **Branch:** `feat/loans_and_leases`
- **Status:** Approved (2026-10-08)
- **Date:** 2026-10-08

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-loans_and_leases.md`) is written after these
> requirements are approved.

## 1. Goal

Bank loans and leases are the user's long-term obligations: they are paid back in monthly payments
over a fixed period. This feature covers them as **outputs** (outgoing cash flow): the user records
a loan or lease once, with its monthly payment and the number of payments left, and the app shows
every upcoming payment in its month, so the user only confirms or corrects it.

A payment is either charged automatically from a primary account by a standing order (*trajni
nalog*) on the payment day, or paid by the user as a regular bill (*uplatnica*). When the interest
rate changes, the user records the new monthly payment, and it applies only to the payments that
follow.

Bank loans and leases are the same kind of output: they are told apart by their category and
subcategory. Other outputs (utilities, variable expenses, card installments and card payments) are
described in later requirements.

## 2. Terms

| Term | Meaning |
|---|---|
| Output | An outgoing cash flow: money leaving one of the user's accounts. |
| Loan | In this document, a bank loan or a lease. Both are recorded the same way; the category tells them apart. |
| Lender | The bank or leasing company the loan is paid to, e.g. ZaBa, Erste Leasing. |
| Category | The kind of loan, e.g. **Bank loan** or **Lease**. Managed in Settings (FR-8). |
| Subcategory | A finer kind of loan within its category, e.g. Mortgage (*stambeni kredit*), Cash loan (*gotovinski kredit*), Car lease. Managed in Settings (FR-8). |
| Payment | One monthly payment of a loan (*rata*). |
| Start month | The month of the loan's first payment recorded in the app. Its bill is dated the first day of that month. |
| Payment day | The day of the month a payment is due, e.g. the 10th or the 15th. |
| Standing order | A payment charged automatically from a primary account on the payment day (*trajni nalog*). |
| Bill | A payment the user pays themselves, like a regular bill (*uplatnica*), and marks as paid. |
| Residual value | The amount paid once at the end of a lease period (*ostatak vrijednosti*), as a separate bill. Offered for loans whose category allows it (FR-8). |
| Payment change | A new monthly payment or payment day from a given month on, e.g. after the bank changes the interest rate (FR-7). |
| Expected payment | A payment that is not yet paid. |
| Paid payment | A payment that is charged or marked as paid. |
| Account, Primary account | As in [credit-card-settings.md](credit-card-settings.md) and [input-cash-flow.md](input-cash-flow.md). |

## 3. Loan attributes

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| A1 | Name | Yes | Free name chosen by the user, e.g. "Apartment mortgage". Unique among the user's loans that are not closed, ignoring upper/lower case. Maximum 50 characters. |
| A2 | Category | Yes | Chosen from the loan categories managed in Settings (FR-8). |
| A3 | Subcategory | Yes | Chosen from the subcategories of the chosen category, managed in Settings (FR-8). |
| A4 | Lender | Yes | Chosen from the bank list of Cards and accounts settings, or added as a new entry (e.g. a leasing company), which is then available everywhere that list is used. |
| A5 | Payment method | Yes | **Standing order** or **Bill**. |
| A6 | Account | For a standing order | The primary account the standing order charges. Prefilled only when exactly one account is primary (FR-3). For a bill, the account is chosen when a payment is marked as paid. |
| A7 | Start month | Yes | Month and year of the first payment recorded in the app (`MM/YYYY`). Its bill is dated the first day of that month. For a loan that is already running, this is the month of the next payment. |
| A8 | Payment day | Yes | Day of the month, 1 to 31, set when the loan is added and used for all its payments. It can later be changed for future payments (FR-7). In a month that has fewer days, the payment is due on the last day of the month. |
| A9 | Monthly payment | Yes | Amount in EUR, exact to the cent, greater than zero. |
| A10 | Number of payments | Yes, or A11 | Number of monthly payments left from the start month on, including it. Whole number, 1 or more. Entered, or calculated from the end month. |
| A11 | End month | Yes, or A10 | Month and year of the last payment (`MM/YYYY`), the same as or later than the start month. Entered, or calculated from the number of payments. |
| A12 | Residual value | No | Amount in EUR, exact to the cent, greater than zero, paid once after the last payment as a separate bill (FR-6). Offered only when the chosen category allows a residual value (FR-8), e.g. Lease. |
| A13 | Description | No | Short free note, e.g. contract number or what was financed. Maximum 200 characters. |

General rules:
- All amounts are in EUR and exact to the cent.
- A paid payment belongs to the month of its **paid date**. An expected payment belongs to the
  month of its **due date**.

## 4. Functional requirements

### 4.1 Recording loans

**FR-1 List loans**
- The **Loans** screen lists the user's loans with: name, category, subcategory, lender, payment
  method, account (for a standing order), current monthly payment, payment day, next payment
  date, payments left (e.g. 18 of 60), end month, remaining amount (FR-9) and residual value.
- Default order: by category, then by name.
- Shown by default: loans that are not closed. A **Closed** filter shows closed loans with their
  history.
- When no loans exist, the screen shows an empty state with an **Add loan** action.

**FR-2 Add a loan**
- The user can open an **Add loan** form, fill in the attributes from section 3 and save.
- The user enters either the number of payments (A10) or the end month (A11); the other one is
  calculated from the start month and shown while the user types. Changing either one updates the
  other.
- Required fields are clearly marked; the form cannot be saved until all rules in section 3 pass.
- Each validation error is shown next to the field it belongs to, in plain language.
- After saving, the loan appears in the list, its payments appear in their months (FR-4) and a
  confirmation is shown.
- The user can cancel; nothing is saved.

**FR-3 Account for a standing order**
- Only a primary account that is not blocked, expired or archived can be chosen.
- When exactly one account is primary, it is prefilled and can be changed. When several are
  primary, the user must choose one before saving.
- When no account is primary, the form says that a standing order needs a primary account and
  links to Cards and accounts in Settings; the user can still save the loan as a bill.
- When the account of a standing order is later blocked, expired, archived or loses its primary
  mark, the loan and its expected payments are visibly marked as needing a new account, until the
  user chooses one or changes the payment method.
- A standing-order loan with expected payments is an ongoing transaction of its account (see
  [credit-card-settings.md](credit-card-settings.md)). So while the account is blocked or expired,
  it stays on the Dashboard Cards widget until the loan has a new account, is switched to bill, or
  has no expected payments left.

**FR-4 Payments**
- Each loan has one payment per month, from the start month to the end month. Each payment shows
  in the **Month** screen, in its Expenses part, with: loan name, category, subcategory, payment
  number (e.g. 18 of 60), amount, bill date, due date, paid date, account and paid marker.
- The bill date of a payment is the first day of its month; the due date is the payment day in
  that month.
- **Weekends:** a standing order is charged on its payment day, also when that day is a Saturday
  or Sunday. For a bill, a due date that falls on a Saturday or Sunday moves to the following
  Monday, except when that Monday is in the next month: then the due date stays on its day, so the
  bill stays in its own month.
- The amount and the due date of a payment follow the monthly payment and the payment day that
  apply to its month (FR-7).
- The Month screen shows the loan payment totals for the month: expected, paid and all payments.
- Expected payments are visually distinguished (dimmed) from paid ones.
- The user can change one expected payment (amount, due date) without changing the others, e.g.
  when the bank charges on a different day that month. A changed payment is visibly marked.

**FR-5 Paying a payment**
- **Standing order:** a payment is paid automatically from the loan's account at the end of its
  due date, without any action from the user; until then it is expected. The user can change its
  paid date or amount, or untick it, e.g. when the bank did not charge it.
- **Bill:** the user receives the bill and decides when to pay it: before, on or after its due
  date. The payment stays expected until the user ticks **Paid**, in the Month screen or on the
  loan. Ticking sets the paid date to today, which can then be changed, and asks for the account
  it was paid from: any account that is not blocked, expired or archived, prefilled only when
  exactly one account is primary. Unticking returns the payment to expected and clears the paid
  date.
- A payment paid on a different day than due shows both dates, so the difference is visible.
- The paid date is the same as or later than the bill date.
- Changing the payment method of a loan applies to its expected payments; paid payments stay
  unchanged.

**FR-6 Residual value of a lease**
- A loan with a residual value gets a separate bill for it after the last payment, in the month
  after the end month. The bill is dated the first day of that month, is due on the loan's payment
  day in that month (moved to Monday when it falls on a weekend, FR-4) and is shown marked
  **Residual value**.
- It is always paid as a bill (FR-5), also when the other payments of the loan are a standing
  order. It can be changed on its own like any payment (FR-4).

**FR-7 Payment changes**
- When the bank changes the interest rate or the payment day, the user records a **payment change**
  on the loan: the month it applies from, and a new monthly payment, a new payment day, or both,
  with an optional note (e.g. "rate change after 5 years"). The interest rate itself is not
  recorded; only the new amount matters.
- The month must be after the month of the latest paid payment, so a change applies only to
  payments that are not yet paid. Payments before that month, paid or not, keep their amount.
- Expected payments from that month on get the new amount and due date, unless the user changed
  one of them by hand (FR-4); the app then asks whether to keep the hand-changed values.
- A loan can have any number of payment changes. The newest change that applies to a month
  decides the amount and the payment day of that month's payment.
- The loan shows the history of its payment changes: from month, monthly payment, payment day
  and note, together with the original values.
- The user can edit or delete a payment change while none of the payments it applies to is paid.

**FR-8 Categories and subcategories**
- Settings gets a **Loan categories** section where the user manages the loan categories and the
  subcategories of each category, in the same way as input categories (see
  [input-cash-flow.md](input-cash-flow.md) FR-6).
- The list starts with:
  - **Bank loan**: **Mortgage**, **Cash loan**, **Car loan**, **Other loan**
  - **Lease**: **Car lease**, **Equipment lease**, **Other lease**
- Each category has an **Allows residual value** check box. It is ticked for Lease and not for
  Bank loan from the start. Unticking it does not change loans that already have a residual
  value.
- The user can add and rename categories and subcategories. A category or subcategory used by a
  loan cannot be removed, only hidden from selection lists; loans keep it.
- Category names are unique, and subcategory names are unique within their category, ignoring
  upper/lower case.

**FR-9 Remaining amount**
- The **remaining amount** of a loan is the sum of its expected payments, including the residual
  value. It is the amount still to be paid, not the bank's outstanding principal.
- The loan shows the payments made so far and the payments left, in number and amount.

**FR-10 Edit, close and delete a loan**
- The user can change any attribute of a loan, with the same rules as when adding. Changes to the
  monthly payment, number of payments, start month or payment day apply to expected payments
  only; paid payments stay unchanged. A new monthly payment or payment day from a later month is a
  payment change (FR-7), not an edit.
- A loan whose last payment (and residual value, when it has one) is paid is shown as **Paid off**.
- The user can **close** a loan early, e.g. when it is paid off ahead of time or refinanced, after
  confirming in a dialog that names the loan. Its expected payments are removed, its paid payments
  stay, and the loan is shown under the **Closed** filter. Closing cannot be undone.
- The user can delete a loan that has no paid payments, after confirming in a dialog that names
  the loan. A loan with paid payments can only be closed.

### 4.2 Presentation

**FR-11 Account balance**
- Paid loan payments are subtracted from the balance of the account they were paid from, and
  expected payments due by the end of the month from the expected balance, as described in
  [Appendix 1 of input-cash-flow.md](input-cash-flow.md#appendix-1-loan-payments-in-the-account-balance-chart-and-report).

**FR-12 Cash flow chart and monthly report**
- Loan payments are outputs, and they are shown on the existing **Cash flow** dashboard and the
  **monthly cash flow report** (see [input-cash-flow.md](input-cash-flow.md) FR-12 and FR-13), as
  described in Appendix 1 of that document.
- Further reports on loans are described in later requirements.

**FR-13 Loans on the Dashboard**
- The Dashboard shows a **Loans** widget with every loan that is not closed or paid off: name,
  category, monthly payment, next payment date, payments left and remaining amount.
- It shows the total monthly payment and the total remaining amount of all listed loans.
- A payment that is overdue (due date passed and not paid), or a loan that needs a new account
  (FR-3), is visibly highlighted.
- When no loans exist, the widget is not shown.
- Selecting an entry takes the user to that loan.

## 5. Non-functional requirements

- **NFR-1 Exact amounts:** amounts, totals and remaining amounts are stored, calculated and shown
  exactly to the cent, never rounded.
- **NFR-2 Responsive:** the Loans screen, the loan form, the payments in the Month screen and the
  Dashboard widget work on phone and desktop.
- **NFR-3 Access:** only signed-in users of the household can see or change loans.
- **NFR-4 Data protection:** loan names, lenders, amounts and contract details are personal
  financial data. They never appear in logs or error messages, and real data is never committed
  to the repository; test data uses made-up values only, as required by `CONTRIBUTING.md`.
- **NFR-5 Language:** UI text in English for now; field labels must be easy to translate to
  Croatian later.

## 6. Out of scope

- Other outputs: utilities, variable expenses, card installments and card payments.
- Recording the interest rate, and calculating payments, interest or the outstanding principal
  from the loan amount and the interest rate (amortization schedule).
- Sharing a loan payment with someone else (e.g. 50 %).
- Insurance or fees paid together with a loan, as separate amounts.
- Partial early repayments that lower the following payments or shorten the loan (they can be
  recorded as a payment change).
- Loans in other currencies or linked to a foreign currency.
- Importing loans or payments from a bank.
- Loan reports beyond the Cash flow dashboard and the monthly cash flow report.

## 7. Acceptance criteria

1. With no loans, the Loans screen shows an empty state with an **Add loan** action, and the
   Dashboard shows no Loans widget.
2. The user can add a bank loan and a lease with all attributes from section 3; entering the number
   of payments calculates the end month and entering the end month calculates the number of
   payments, and one payment per month appears in the Month screen from the start month to the
   end month.
3. Saving is blocked, with a clear message, when:
   - a required field is missing;
   - the monthly payment or residual value is zero or negative;
   - the number of payments is not a whole number of 1 or more, or the end month is before the
     start month;
   - the payment day is not between 1 and 31;
   - a standing order has no primary account;
   - the name is already used by a loan that is not closed.
4. A payment's bill date is the first day of its month and its due date is the payment day; with
   payment day 31, the due date in February is its last day. A bill due on a Saturday or Sunday
   is due the following Monday, unless that Monday is in the next month; a standing order keeps
   its payment day.
5. With exactly one primary account, a standing order is prefilled with it; with several, the user
   must choose. A standing order payment is paid at the end of its due date without user action,
   and the user can correct or untick it.
6. A bill payment stays expected until the user ticks **Paid**, which sets the paid date to today
   and records the account it was paid from; unticking reverses this. A bill can be marked as
   paid before, on or after its due date.
7. A lease with a residual value shows a separate **Residual value** bill in the month after its
   end month, paid as a bill even when the lease is paid by standing order; a loan whose category
   does not allow a residual value offers none.
8. A payment change from a future month changes the amount, the payment day or both of expected
   payments from that month on, leaves earlier and paid payments unchanged, and is shown in the
   loan's history; several changes on one loan each apply from their own month.
9. A payment change cannot start in or before the month of the latest paid payment.
10. Changing one payment does not change the other payments of the loan.
11. The remaining amount equals the sum of expected payments plus an unpaid residual value, and
    decreases as payments are paid.
12. Paid payments lower the current balance of their account, and expected payments due this
    month lower its end-of-month expected balance on the Dashboard.
13. Closing a loan removes its expected payments, keeps its paid ones and moves it to the
    **Closed** filter; a loan with paid payments cannot be deleted.
14. The default categories and subcategories exist from the start; the user can add and rename
    categories and subcategories, and a used one can only be hidden.
15. The Dashboard Loans widget lists active loans with the totals of monthly payments and
    remaining amounts, and highlights overdue payments.
16. Loan payments appear as outputs on the Cash flow dashboard and in the monthly cash flow report,
    paid and expected ones distinguished, and the report shows the month result (inputs minus
    outputs).
