# Plan: Revolving card daily interest and starting debt order

- **Branch:** `claude/project-thread-no7dqr` (restarted from `main` after #15; this plan has its own
  file name because the branch's first plan, [feat-revolving_credit_card.md](feat-revolving_credit_card.md),
  is the merged feature's plan)
- **Status:** Draft
- **Date:** 2026-11-03
- **Requirements:** [revolving-credit-card.md](../feature-requirements/revolving-credit-card.md),
  Appendix 1 (approved)

## Goal
A payment made on the same day as the starting debt lowers the debt, and a revolving card's interest
follows the debt of every day, as the bank charges it, for past and estimated bills.

## Requirement changes
[Appendix 1: Starting debt order and daily interest](../feature-requirements/revolving-credit-card.md#appendix-1-starting-debt-order-and-daily-interest):
- **FR-5:** a starting debt is the debt at the **start** of its date; charges, payments and
  transfers on that date come after it. A correction stays the debt at the **end** of its date.
- **FR-7:** interest = Σ (debt at the end of each day × interest rate ÷ 365) for the days from the
  day after the previous bill day up to and including this bill day, rounded to the cent once and
  added on the bill day before the minimum payment. The first bill counts from the starting debt or
  first charge. A bill's interest can still be corrected (FR-4).
- **FR-4:** estimated bills use the same daily interest, with the minimum paid on each due date.

## Scope
- In scope: the event order in `RevolvingDebtCalculator`, daily interest for real and estimated
  bills, tests, `docs/INIT.md`.
- Out of scope:
  - leap years: always 365 days;
  - interest booked on another day than the bill day;
  - fees;
  - matching the bank's figure of 12,99 € from the report. The user corrects a bill's interest or
    the debt when the bank differs.

## Design
- Domain / data model changes:
  - `RevolvingDebtCalculator`:
    - three orders on a date: `StartOfDay` (starting debt) before `Relative` (charges, credits,
      transfers, payments) before `EndOfDay` (correction). The bill of a bill date comes after all of
      them, as now.
    - Daily accrual: while events are applied in date order, the calculator keeps a running sum of
      debt-cent-days since the previous bill day. Before the debt changes on a new date, each skipped
      day adds the debt at the end of that day. On the bill day, after that day's events, the bill
      day itself is added. The interest is then `RevolvingMath.DailyInterest(sum, terms)` and the sum
      starts again from zero.
    - A stored corrected interest still wins.
  - `RevolvingMath`:
    - `DailyInterest(long debtCentDays, RevolvingTerms terms)` = debtCentDays × interest bp ÷
      (10000 × 365), in integer math, rounded half away from zero (NFR-1);
    - `MonthlyInterest` is removed.
  - No new tables or columns. Stored bills keep their state.
  - Existing cards recalculate their debt history: past bills whose interest was not corrected get
    the daily interest, and a starting debt dated on a day with payments now counts before them.
    Paid amounts stay as stored.
- API changes (endpoints, DTOs): none; the same responses carry the new amounts.
- Front-end changes (pages, components): none.
- Database migrations: none.

## Steps
1. Commit this plan after approval.
2. Domain: event order, daily accrual, `DailyInterest`; update and add unit tests.
3. API tests: a starting debt and an extra payment on the same day; the interest of a month with a
   payment in the middle.
4. `docs/INIT.md` key rule for the debt and interest.
5. Run format, all backend and front-end checks; stage for review.
6. Pull request, then UAT and production as before.

## Tests
- Unit tests:
  - a starting debt of 1.612,98 € and an extra payment of 400,00 € on the same day give 1.212,98 €;
  - a correction on a day with a payment stays the corrected amount;
  - interest for a month with a constant debt: 30 days × 1.000,00 € at 12,75 % = 10,48 €;
  - a payment in the middle of the month lowers that month's interest by its days;
  - the first bill counts only the days since the starting debt;
  - estimated bills accrue daily with the minimum paid on the due date;
  - a corrected interest still replaces the calculated one;
  - `DailyInterest` rounding half away from zero.
- API / integration tests: the existing revolving tests updated, plus the same-day starting debt and
  payment case.
- Manual checks: the card page and the Month screen on UAT with the user's card.

## Open questions
- None.

## Changes during implementation
- The same-day unit test uses made-up amounts (1.500,00 € starting debt, 400,00 € payment, giving
  1.100,00 €) instead of the figures from the user's bank, so no real financial data is committed.
- The API tests run with 0 % interest, so the mid-month payment case is covered by the domain tests
  only; the API test checks the same-day starting debt and payment.
