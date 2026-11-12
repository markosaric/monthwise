# Plan: Balance entries of an archived account are read-only

- **Branch:** `fix/archived_balance_entries`
- **Status:** In progress
- **Date:** 2026-11-12
- **Requirements:** [input-cash-flow.md, Appendix 3](../feature-requirements/input-cash-flow.md#appendix-3-balance-entries-of-an-archived-account-are-read-only)

## Goal
The starting balance and the balance corrections of an archived account can no longer be edited or
deleted, in the API or on the account's screen, the same way adding one is already refused.

## Requirement changes
[Appendix 3](../feature-requirements/input-cash-flow.md#appendix-3-balance-entries-of-an-archived-account-are-read-only)
extends FR-9:

- Balance entries of an archived account cannot be added, edited or deleted. They stay visible
  with their date, balance, difference and note.
- The account's screen shows no **Edit** or **Delete** action for them, as it already shows no
  action to add one.
- A change attempted anyway is refused with a message that the account cannot be changed, and
  nothing is saved.
- Blocked and expired accounts are not affected.

**Why:** the code review of `main` (finding L11) found that `BalanceEntryHandler.UpdateAsync`
does not check whether the account is archived, while `CreateAsync` does. `DeleteAsync` has the
same gap, and the UI shows Edit and Delete on archived accounts.

## Scope
- In scope: the archived-account part of review finding L11 (update and delete of balance
  entries, back end and the account's screen).
- Out of scope: the `parseEuroInput` part of L11 (a dot meaning thousands in `'1.234'` but
  decimals in `'1.23'`). It changes `frontend/src/lib/money.ts` and its tests, the same file as
  high-priority finding H5 (minus sign lost), so it is done on its own branch **after H5 is
  merged**. Blocked and expired accounts keep their current behaviour.

## Design
- Domain / data model changes: none.
- Application (`backend/src/Monthwise.Application/CashFlow/BalanceHandlers.cs`,
  `BalanceEntryHandler`):
  - `UpdateAsync` and `DeleteAsync` load the entry's account after finding the entry and return
    `CardErrors.ReadOnly` (409 Conflict, the error `CreateAsync` already uses) when
    `ArchivedOn` is set, before any change is made or saved.
  - The check is a small private helper shared by create, update and delete, so the three stay in
    line.
- API changes (endpoints, DTOs): no new endpoints or DTOs. `PUT /api/balance-entries/{id}` and
  `DELETE /api/balance-entries/{id}` document the 409 response (`ProducesProblem(409)`); the
  generated `frontend/src/api/schema.d.ts` is regenerated if it changes.
- Front-end changes (pages, components):
  - `frontend/src/components/cashflow/BalanceEntries.tsx`: the `canAdd` prop becomes `readOnly`
    and hides **Add**, **Edit** and **Delete** together.
  - `frontend/src/pages/settings/CardDetailsPage.tsx`: passes `readOnly={archived}` (same
    condition as today's `canAdd={!archived}`).
- Database migrations: none.

## Steps
1. Add the archived check to `UpdateAsync` and `DeleteAsync` through a shared helper, and document
   409 on both endpoints.
2. Add API tests for update and delete on an archived account, and for blocked/expired accounts
   still allowing them.
3. Change `BalanceEntries` to a `readOnly` prop that also hides Edit and Delete; update
   `CardDetailsPage`.
4. Add a front-end test for the account screen of an archived account.
5. Run `dotnet build`, `dotnet test`, and front-end typecheck, lint, format check and tests.
   Leave everything staged for review.

## Tests
- Unit tests: none needed in the domain (no domain change).
- API / integration tests (`CashFlowEndpointTests`):
  - On an archived account, `PUT` and `DELETE` of an existing balance entry return 409 and the
    entry is unchanged (`GET /api/cards/{id}/balance` still lists it with its old values).
  - On a blocked account, `PUT` and `DELETE` still succeed.
- Front-end tests (`cashflow.test.tsx`): the details page of an archived account lists its balance
  entries without Edit, Delete or Add actions; an active account still shows them.
- Manual checks: archive an account with a starting balance and check its screen shows the entry
  without actions.
