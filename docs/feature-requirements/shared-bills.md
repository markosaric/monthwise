# Requirements: Shared bills and loan payments

- **Branch:** `feat/shared_bills`
- **Status:** Approved (2026-10-09)
- **Date:** 2026-10-09

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-shared_bills.md`) is written after these
> requirements are approved.

## 1. Goal

Some bills and some loans are shared with people outside the household, e.g. the utilities and the
mortgage of an apartment shared with a relative who pays back half. The household pays the
**whole** bill or loan payment, and the other person pays their part back to the household as an
**agreed contribution** (see [input-cash-flow.md](input-cash-flow.md) FR-6).

This feature lets the user split a bill or a loan into **shares** and say how many of them the
participant pays. The app then:

- calculates the participant's part from the number of shares,
- records it as an expected **contribution** in the input cash flow, so the user only ticks it when
  the money arrives,
- shows how much of each bill, loan payment and month the household really pays itself, and how
  much each participant still owes.

## 2. Terms

| Term | Meaning |
|---|---|
| Participant | A person outside the household who shares some costs (Croatian: *sudionik u troškovima*), e.g. a relative living in the shared apartment. Not a user of the app. |
| Shared cost | A bill, recurring bill, loan or loan payment that is split into shares with one participant. |
| Shares | The number of equal parts a shared cost is split into, e.g. 2 for half and half. |
| Participant's shares | How many of those parts a participant pays, e.g. 1 of 2. |
| Participant's part | The amount a participant pays back for one bill or loan payment (FR-4). |
| Own part | The amount the household pays itself: the full amount minus the participant's part. |
| Contribution | The input in which a participant pays back their part of one bill or loan payment. Category **Agreed contributions** (see [input-cash-flow.md](input-cash-flow.md) FR-6). |
| Outstanding | A participant's contributions that are expected but not yet received. |
| Bill, Recurring bill, Paid from | As in [monthly-bills-and-expenses.md](monthly-bills-and-expenses.md). |
| Loan, Payment, Residual value | As in [loans-and-leases.md](loans-and-leases.md). In this document a loan payment includes the residual value bill of a lease. |
| Input, Payer, Expected input, Received input | As in [input-cash-flow.md](input-cash-flow.md). |

## 3. Functional requirements

### 3.1 Participants

**FR-1 Participants**
- The user keeps the people costs are shared with, with these attributes:

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| P1 | Name | Yes | E.g. "Ana". Unique among participants, ignoring upper/lower case. Maximum 50 characters. |
| P2 | Description | No | Short free note. Maximum 200 characters. |

- Participants are managed on the **Shared costs** screen (FR-8) and can also be added directly
  while sharing a cost (**Add new…**).
- A participant is also a **payer** of inputs (see [input-cash-flow.md](input-cash-flow.md) FR-5):
  their contributions are recorded with the participant's name as payer.
- The user can edit participants. A participant used by a shared cost cannot be deleted, only
  **archived**: an archived participant is no longer offered for new shared costs, and costs that
  use it keep it. A participant never used can be deleted.

### 3.2 Sharing a cost

**FR-2 Shares**
- A shared cost has these share attributes:

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| S1 | Shares | Yes | Number of equal parts the cost is split into. Whole number, 2 to 4. Default 2. |
| S2 | Participant | Yes | The one participant who pays back a part. A shared cost has exactly one participant. |
| S3 | Participant's shares | Yes | How many of the shares the participant pays. Whole number, 1 or more and fewer than S1, so the household always holds at least one share and pays its own part. Default 1. |

- Examples:
  - 2 shares, Ana 1: Ana pays half.
  - 3 shares, Ana 1: Ana pays a third.
  - 4 shares, Ana 3: Ana pays three quarters.
- The screen shows the participant's shares also as a percentage, rounded to whole percent, so the
  split is easy to read.

**FR-3 Where the user shares a cost**
- **Recurring bill or loan:** on the recurring bill (see
  [monthly-bills-and-expenses.md](monthly-bills-and-expenses.md) FR-2) or the loan (see
  [loans-and-leases.md](loans-and-leases.md) section 3) the user ticks **Shared** and sets the
  shares. This is the usual way: every bill added from the recurring bill, and every loan payment
  that is not yet paid, takes these shares, so the user sets them once.
- **One bill or loan payment:** on the **Month** screen, in the Expenses part, the user can share
  a single bill or loan payment, change its shares or stop sharing it, e.g. when a participant
  moves out halfway through a month. Such a change applies only to that bill or payment and marks
  it as changed by hand.
- Changing or stopping the shares of a recurring bill or a loan applies to bills added to months
  later and to loan payments that are not yet paid and not changed by hand; when some expected
  ones were changed by hand, the app asks whether to keep their shares. Paid bills and payments
  keep their shares.
- A one-off bill can be shared on the Month screen too.

**FR-4 Participant's part**
- The participant's part of a bill or loan payment is its full amount × the participant's shares
  (S3) ÷ the shares (S1), rounded to the nearest cent (half a cent rounded up).
- The own part is the full amount minus the participant's part, so the two always add up exactly
  to the full amount.
- A not issued bill (amount 0) has no parts yet.
- A bill or loan payment paid with a credit card is shared in the same way: its full amount is in
  the card payment, and the participant's contribution is an input as usual.

**FR-5 Contributions**
- For every shared bill that is **issued** and every shared **loan payment**, the app adds exactly
  one **expected contribution** from its participant, so each month shows one input per shared
  bill or loan payment:

| Input attribute | Value |
|---|---|
| Payer | The participant. |
| Category | **Agreed contributions**. |
| Amount | The participant's part (FR-4). |
| Issue date | The bill's issue date, or the loan payment's bill date. |
| Expected date | The bill's or loan payment's due date. |
| Account | The account the bill or payment is paid from when it is an account; otherwise the primary account when exactly one account is primary; otherwise the user chooses it. |
| Description | The name of the bill or loan, e.g. "Electricity, apartment" or "Apartment mortgage 18/60". |

- A contribution is shown in the **Income** part of the Month screen as an input cash flow, linked
  to its bill or loan payment. It is an ordinary input in every other way: it is ticked as received
  when the participant pays (see [input-cash-flow.md](input-cash-flow.md) FR-3) and counts in the
  account balance, the cash flow chart and the monthly report like any other input.
- A shared bill or loan payment shows its contribution with its state (expected or received).

**FR-6 Keeping contributions in step**
- When the amount, dates, account or shares of a shared bill or loan payment change (including a
  loan payment change, see [loans-and-leases.md](loans-and-leases.md) FR-7), its **expected**
  contribution is updated, added or removed to match.
- A **received** contribution is never changed automatically. When a change would change it, the
  confirmation says so and shows the difference between the received amount and the new part, so
  the user can settle it with the participant.
- Deleting a shared bill, or closing or deleting a shared loan, removes the expected contributions
  of the removed bills and payments. Received contributions stay as ordinary inputs, no longer
  linked; the confirmation names them.
- The user can edit a contribution like any input, e.g. when a participant paid a slightly
  different amount. It then no longer follows its bill or payment until the user chooses **Reset
  to part**.

### 3.3 Presentation

**FR-7 Month screen**
- In the Expenses part, a shared bill or loan payment is marked as shared and shows its **own
  part** next to its full amount. The bills and loan payment totals of the month also show the own
  part total.
- In the Income part, contributions show their bill or loan payment, so they are easy to tell
  apart from other agreed contributions.

**FR-8 Shared costs screen**
- A **Shared costs** screen lists the participants and, for each one and for a chosen month:
  - their shared bills and loans with their shares,
  - the parts of that month, received contributions, and **outstanding** contributions in total
    (all months), oldest first, with overdue ones (expected date passed) highlighted.
- The user can tick a contribution as received from there.
- The user manages participants there (FR-1).

**FR-9 Cash flow and monthly report**
- The cash flow stays **gross**: the full bill or loan payment is an output (or part of a card
  payment) and each contribution is an input, so account balances match the bank.
- The **monthly cash flow report** (see [input-cash-flow.md](input-cash-flow.md) FR-13) also shows,
  for the month, the total of shared costs, the participants' parts, the own part, and
  contributions received and outstanding.

## 4. Non-functional requirements

- **NFR-1 Exact amounts:** parts are calculated and shown exactly to the cent (FR-4), never with
  floating-point rounding.
- **NFR-2 Responsive:** sharing a cost, the Shared costs screen and ticking a contribution as
  received work on phone and desktop.
- **NFR-3 Access:** only signed-in users of the household can see or change participants, shares
  and contributions. Participants do not sign in.
- **NFR-4 Data protection:** participant names and amounts are personal data. They never appear in
  logs or error messages, and test data uses made-up names and values only, as required by
  `CONTRIBUTING.md`.
- **NFR-5 Language:** UI text in English for now; field labels must be easy to translate to
  Croatian later.

## 5. Out of scope

- Sharing expenses, cash withdrawals or card purchases (only bills and loans).
- Sharing one bill or loan with more than one participant.
- Shares given as a percentage or a fixed amount instead of a number of shares.
- A participant paying their part directly to the payee or lender instead of to the household.
- Participants as users of the app, or sending them requests, reminders or statements.
- One contribution for several bills or payments (e.g. one transfer for the whole month).
- Multiple currencies.

## 6. Acceptance criteria

1. The user can add, edit, archive and (when never used) delete participants; a duplicate name
   (ignoring upper/lower case) or a name longer than 50 characters is refused.
2. The user can share a recurring bill or a loan with one participant by setting the number of
   shares (2 to 4) and the participant's shares, which are fewer than the total.
3. Bills added from a shared recurring bill and unpaid payments of a shared loan take its shares;
   the user can change the shares of one bill or loan payment on the Month screen.
4. The participant's part is the amount × their shares ÷ the shares, rounded to the nearest cent,
   and the participant's part and the own part add up exactly to the full amount.
5. Each issued shared bill and each shared loan payment gets exactly one expected Agreed
   contributions input from its participant, shown in the Income part of the Month screen and linked to its cost.
6. Changing a shared bill, loan payment or loan updates its expected contributions; received
   contributions are not changed and the difference is shown.
7. Deleting a shared bill or closing a shared loan removes its expected contributions and keeps
   received ones as ordinary inputs.
8. The Month screen shows the own part of each shared cost and of the month's totals.
9. The Shared costs screen shows each participant's shared costs, parts, received and outstanding
   contributions, with overdue ones highlighted.
10. Account balances, the cash flow chart and the monthly report count the full cost as an output
    and contributions as inputs; the monthly report also shows the own part.
