# Requirements: Overall balance tile

- **Branch:** `claude/project-thread-snccf3`
- **Status:** Approved (2026-11-03)
- **Date:** 2026-11-03

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/claude-project-thread-snccf3.md`) is written after these
> requirements are approved.

## 1. Goal

The key figures show what comes in, what goes out, the month result and what is still to pay, but
not the one number the user checks most: **how much money is left on the primary accounts once
everything of the month is paid**. Today the user has to open the Cards screen and add up the
expected end-of-month balances of the primary accounts by hand.

This feature adds a fifth key figure, **Overall balance**, as the **last tile** of the key figures
row on the Dashboard and on the Month screen. It shows the overall balance of the primary accounts
after every input of the month is received and every output of the month is paid.

## 2. Terms

| Term | Meaning |
|---|---|
| Primary account | A current account marked as primary (see [credit-card-settings.md](credit-card-settings.md) FR-6). There can be several. |
| Expected balance | An account's balance at the end of the month once every expected input is received and every expected output is paid, calculated as on the Cards screen (see [input-cash-flow.md](input-cash-flow.md) FR-10 and its appendices, [loans-and-leases.md](loans-and-leases.md) FR-11, [credit-card-installments.md](credit-card-installments.md) FR-9, [revolving-credit-card.md](revolving-credit-card.md)). |
| Overall balance | The sum of the balances of all primary accounts at the end of the shown month (Croatian: *saldo*): expected for the current and future months, actual for past months. |
| Output without account | A bill, loan payment or other output of the month that has no "paid from" account yet. |

## 3. Functional requirements

**FR-1 The Overall balance tile**
- The key figures row gets a fifth tile, **Overall balance**, placed **last**, after Still to pay.
  It uses the same look as the other key figures (FR-2 of [design-refresh.md](design-refresh.md)):
  icon, big number and a short label.
- The label is **Overall balance**.
- The tile is shown on the **Dashboard** (for the current month) and on the **Month** screen (for
  the month shown there).

**FR-2 Value**
- The value is the sum of the balances of **all primary accounts** at the end of the shown month.
- For the current month it counts everything that the expected balance counts: the current
  balance, plus expected inputs to the primary accounts, minus expected bills, expenses, loan
  payments, card payments and revolving card bills paid from the primary accounts, due by the end
  of the month. Outputs from earlier months that are still unpaid are counted too, because they
  still have to be paid.
- **Outputs without account:**
  - with exactly **one** primary account, they are counted as paid from it;
  - with **several** primary accounts, they are not counted, and the detail line says how many
    there are (FR-3).
- Amounts are shown in EUR to the cent. A negative overall balance is shown in red, like a
  negative month result.

**FR-3 Detail line**
- Under the number, the tile names what it covers:
  - with one primary account: its name, e.g. "Overall balance · Tekući ZaBa";
  - with several: their number, e.g. "Overall balance · 2 primary accounts".
- With several primary accounts and outputs without account in the month, it adds how many are
  not counted, e.g. "3 outputs without account".
- When any primary account has no starting balance, the detail line says so ("starting balance
  missing"), because the overall balance is then not reliable.

**FR-4 Other months on the Month screen**
- For the **current** month, the overall balance is the expected balance at the end of this month
  (FR-2).
- For a **past** month, it is the actual balance of the primary accounts at the end of that month:
  what was received and paid by then.
- For a **future** month, it is the **expected** balance at the end of that month, counting the
  expected inputs and outputs of every month up to and including it, by the same rules as FR-2.
- The tile shows which kind it is: future and current values are marked as expected (dimmed or
  with the word "expected"), past values are not.

**FR-5 No primary account**
- When no account is marked as primary, the tile shows "–" and the hint "Mark a primary account",
  and selecting it opens the Cards screen.

**FR-6 Opening the screen behind it**
- Selecting the tile opens the **Cards** screen, where the expected balance of each account is
  shown (see [design-refresh.md](design-refresh.md) FR-12).
- The tile has no change pill against last month.

**FR-7 Changes update the tile**
- Ticking an input as received or an output as paid, adding, changing or deleting an input or an
  output, or adding a balance entry, changes the overall balance at once, the same way it changes
  the expected balance on the Cards screen.

## 4. Non-functional requirements

- **NFR-1 Consistency:** for the current month, the overall balance equals the sum of the
  "expected at end of month" balances of the primary accounts shown on the Cards screen, apart
  from the outputs without account counted under FR-2.
- **NFR-2 Responsive:** on desktop the five tiles fit in one row; on tablet and phone they wrap
  the same way the four tiles do today, and the Overall balance tile stays last.
- **NFR-3 Money:** the overall balance is calculated in whole cents, never with floating point.

## 5. Out of scope

- An overall balance for accounts that are not primary, savings or revolving cards (they keep
  their balances on the Cards and Savings screens).
- Choosing which primary accounts are counted.
- A chart or history of the overall balance over time.
- A comparison of the overall balance with last month.

## 6. Acceptance criteria

1. The Dashboard and the Month screen show five key figures, with Overall balance last.
2. With one primary account, the overall balance equals that account's expected end-of-month
   balance on the Cards screen and the detail line names the account.
3. With two primary accounts, the overall balance is the sum of both expected balances and the
   detail line says "2 primary accounts".
4. Marking an expected bill paid from the primary account as paid does not change the overall
   balance (the bill was already counted); adding a new expected bill of €50.00 from it lowers the
   overall balance by €50.00.
5. Ticking an expected input to the primary account as received does not change the overall
   balance; adding a new expected input of €100.00 to it raises the overall balance by €100.00.
6. An overdue bill from last month, still unpaid, lowers the overall balance of the current month.
7. With one primary account, a bill of €40.00 without account lowers the overall balance by
   €40.00; with two primary accounts it does not, and the detail line says "1 output without
   account".
8. On the Month screen, a past month shows the actual balance at the end of that month, and a
   future month shows the expected balance at its end, marked as expected.
9. A negative overall balance is shown in red.
10. Without a primary account, the tile shows "–" and "Mark a primary account", and opens the
    Cards screen.
11. A primary account without a starting balance shows "starting balance missing" in the detail
    line.
12. On a phone the tiles wrap and Overall balance is the last one.

## Changes to approved requirements

The redesigned Dashboard shows nothing about account balances
([design-refresh.md](design-refresh.md) FR-9) and four key figures on the Dashboard (FR-10) and
the Month screen (FR-15). This feature changes that through
[Appendix 1 of design-refresh.md](design-refresh.md#appendix-1-overall-balance-key-figure); the
approved text of design-refresh.md is not rewritten.
