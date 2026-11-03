# Plan: Overall balance tile

- **Branch:** `claude/project-thread-snccf3`
- **Status:** Done
- **Date:** 2026-11-03
- **Requirements:** [overall-balance-tile.md](../feature-requirements/overall-balance-tile.md) (approved),
  [design-refresh.md Appendix 1](../feature-requirements/design-refresh.md#appendix-1-overall-balance-key-figure)

## Goal
The Dashboard and the Month screen show a fifth key figure, **Overall balance**: the balance of all
primary accounts at the end of the month once everything is received and paid (expected for the
current and future months, actual for past months).

## Requirement changes
[Appendix 1 of design-refresh.md](../feature-requirements/design-refresh.md#appendix-1-overall-balance-key-figure)
changes FR-9, FR-10 and FR-15 of the design refresh:
- FR-9: the Dashboard shows no account balances **except** the Overall balance key figure.
- FR-10: the Dashboard shows five key figures (K1 to K5); K5 is Overall balance, no change pill,
  opens Cards.
- FR-15: the Month screen's key figures are Inputs, Outputs, Month result, Still to pay and
  Overall balance.

## Scope
- In scope: FR-1 to FR-7, NFR-1 to NFR-3 of the requirements; the Dashboard and Month screen tile;
  the user manual paragraph about key figures.
- Out of scope: everything in section 5 of the requirements (non-primary accounts, choosing
  accounts, history chart, change pill).

## Design
- **Domain / data model changes:** none. The `AccountBalance` calculator already gives the balance
  on a date and the expected balance; no new tables.
- **Application:**
  - Refactor `AccountBalanceQuery` so the expected end-of-month balance can be calculated for any
    month end, not only the current one: extract the "expected movements due by `monthEnd`" part
    (expected inputs, loan payments, card payments, revolving bills, bills) into one method that
    takes `monthEnd` and also returns the outputs **without an account** of that range (unpaid,
    `AccountId` null or `NeedsAccount`). `ForAccountsAsync` keeps its behaviour and result.
  - New `OverallBalanceQuery` (`Application/CashFlow`) with `ForMonthAsync(YearMonth month)`:
    1. primary accounts = cards with `IsPrimary` (archived, blocked and expired cards lose the mark,
       so no extra filter is needed);
    2. none → result with no value (FR-5);
    3. `monthEnd = month.LastDay`; **past** month (`monthEnd < today`): actual balance
       `BalanceOn(monthEnd)` from `CalculatorsAsync(monthEnd)` (FR-4); **current or future** month:
       `BalanceOn(today)` + expected movements due by `monthEnd` (FR-2, FR-4), summed over the
       primary accounts;
    4. outputs without account (current and future months only): with one primary account they
       are subtracted from its value; with several they are only counted (FR-2, FR-3);
    5. `MissingStartingBalance` when any primary account has no checkpoint (FR-3).
  - All sums use `Money` (integer cents) (NFR-3).
- **API changes:**
  - `GET /api/overall-balance?month=yyyy-MM` (month optional, default the current month; an invalid
    month is a validation problem, like the other month endpoints).
  - `OverallBalanceResponse(string Month, bool IsExpected, long? BalanceCents, int PrimaryAccountCount,
    int OutputsWithoutAccountCount, bool MissingStartingBalance)`.
  - Regenerate `frontend/src/api/schema.d.ts`.
- **Front-end changes:**
  - `frontend/src/api/cashFlow.ts`: `useOverallBalance(month)` with query key
    `['cards', 'overall-balance', month]`, so every mutation that already invalidates `cardsQueryKey`
    (inputs, bills, expenses, loans, card payments, revolving, balance entries) refreshes it (FR-7).
  - New `components/design/OverallBalanceCard.tsx`: a `StatCard` with the Wallet icon, label
    "Overall balance", value in EUR, red when negative, `muted` when `IsExpected`, detail line by
    FR-3 ("Tekući ZaBa", "2 primary accounts", "3 outputs without account", "starting balance
    missing", joined with " · "), "–" and "Mark a primary account" without a primary account
    (FR-5), and `to="/cards"` (FR-6). No pill.
  - `DashboardPage.tsx`: add the card last in the key figures with the current month and use
    `StatGrid five`; update the component comment (no balances except K5).
  - `MonthPage.tsx`: add the card last with the shown month and use `StatGrid five`.
  - Texts in `components/dashboard/texts.ts` (and shared with the Month screen).
- **Database migrations:** none.
- **Docs:** user manual (`docs/user-manual/`) key figures paragraph mentions Overall balance; INIT.md
  needs no change (no data model change).

## Steps
1. Refactor `AccountBalanceQuery` to calculate expected movements for any month end, with existing
   tests still green.
2. Add `OverallBalanceQuery`, register it, and add the endpoint and response.
3. Unit and API tests for the backend (below).
4. Regenerate the API schema; add `useOverallBalance` and `OverallBalanceCard`.
5. Add the card to the Dashboard and the Month screen with five-tile layout; check phone layout
   (NFR-2).
6. Front-end tests; update the user manual; set this plan to Done.
7. Leave all changes staged for review.

## Tests
- **Unit tests:** none new in the domain (no domain change).
- **API / integration tests** (`OverallBalanceEndpointTests`), matching the acceptance criteria:
  - one primary account: equals its expected balance from `/cards/{id}/balance` (AC 2, NFR-1);
  - two primary accounts: the sum, count 2 (AC 3);
  - a new expected bill of €50.00 lowers it by 5000 cents, marking it paid does not change it (AC 4);
  - a new expected input of €100.00 raises it, receiving it does not change it (AC 5);
  - an unpaid overdue bill from last month lowers the current month (AC 6);
  - a €40.00 bill without account: counted with one primary account, only counted (count 1) with
    two (AC 7);
  - past month = actual balance at its end and `IsExpected` false; future month = expected and
    `IsExpected` true (AC 8);
  - no primary account: `BalanceCents` null (AC 10); missing starting balance flag (AC 11);
  - invalid month → 400.
- **Front-end tests** (Vitest): Dashboard and Month screen render five key figures with Overall
  balance last (AC 1); negative in red (AC 9); "–" and "Mark a primary account" (AC 10); detail line
  texts (AC 2, 3, 7, 11).
- **Manual checks:** desktop five tiles in one row, phone wrap with Overall balance last (AC 12);
  ticking a bill paid on the Month screen updates the tile without reload (FR-7).

## Open questions
- None. Known limit (from how the app works today, not a new rule): for future months, recurring
  bills that are not issued yet have no amount, so they do not lower the expected overall balance
  until they are issued.

## Changes during implementation
- After review, the tile shows no account name and no "expected" marker (requirements FR-3 and FR-4
  changed on this branch); `PrimaryAccountName` was removed from the response.
- The detail text is built in `components/dashboard/overallBalance.ts`, apart from the card
  component, so the component file only exports components (lint rule).
- Outputs without account with a zero amount (bills not issued yet) are not counted.

