# Plan: Keep the minus sign of small negative amounts when editing

- **Branch:** `fix/negative_amount_edit`
- **Status:** Done
- **Date:** 2026-11-12
- **Requirements:** none (bug fix without behaviour change; the requirements already say a balance
  can be negative)

## Goal
Opening and saving a balance entry of −0,50 € keeps it at −0,50 €. Today the edit field shows
`0,50`, so saving without touching it stores +0,50 €. This is review finding H5.

## Scope
- In scope: `centsToEuroInput` in `frontend/src/lib/money.ts`, which turns cents into the text of
  an edit field.
- Out of scope:
  - The `parseEuroInput` dot rule (the rest of review finding L11). It changes the same file and
    tests and follows as its own fix after this one.
  - Any change to how amounts are typed, parsed or shown.

## Design
- Cause: `centsToEuroInput` takes the euros from `Math.trunc(cents / 100)` and the cents from
  `Math.abs(cents % 100)`. For −1 to −99 cents the euros are `0` (JavaScript has no `-0` in
  `String()`), so the sign is lost. From −100 cents on the euros carry the sign, so −1,50 € is
  shown correctly today.
- Fix: write the sign first, then build euros and cents from `Math.abs(cents)`:
  −50 → `-0,50`, −150 → `-1,50`, 0 → `0`, 123456 → `1234,56` (unchanged).
- Who is affected: only the balance entry form (`components/cashflow/BalanceEntries.tsx`) edits
  signed amounts. All other forms that use `centsToEuroInput` edit amounts that are never negative,
  so their output does not change.
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes: `centsToEuroInput` only.
- Database migrations: none.

## Steps
1. Change `centsToEuroInput` as above and update its comment.
2. Add the tests below; run the front-end lint, type check and tests.

## Tests
- Unit tests (`lib/money.test.ts`):
  - `centsToEuroInput` for −1, −50, −99, −100 and −150 cents gives `-0,01`, `-0,50`, `-0,99`,
    `-1` and `-1,50`, and 0 gives `0`.
  - Round trip: for −150, −99, −50, −1, 0, 5, 150 and 123456 cents,
    `parseSignedEuroInput(centsToEuroInput(cents))` gives the same cents back.
- Screen test (`components/cashflow/cashflow.test.tsx`): on the account details screen, editing a
  balance entry of −50 cents shows `-0,50` in the amount field, and saving it unchanged sends
  `balanceCents: -50`.
- Manual checks: none needed beyond the tests.

## Open questions
- None.
