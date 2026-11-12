# Plan: Entering amounts in euros

- **Branch:** `fix/euro_amount_input`
- **Status:** Done
- **Date:** 2026-11-12
- **Requirements:** [amount-input.md](../feature-requirements/amount-input.md)

## Goal
The same typed amount always means the same number of cents. Text where a dot could be either a
thousands dot or a decimal point (`1.234`, `1.500`, `1234.567`) is refused instead of guessed
(code review finding L11, `parseEuroInput` part; it follows H5, merged in #35).

## Scope
- In scope: `parseEuroInput` and `parseSignedEuroInput` in `frontend/src/lib/money.ts`, which
  every amount field uses (FR-1, FR-2), and the tests that type amounts.
- Out of scope: percentages (`frontend/src/lib/percent.ts`), how amounts are shown
  (`formatCents`), the back end (it receives cents) and the messages of the amount fields, which
  stay as they are (FR-2 uses each field's existing message). The archived-account part of L11 was
  done in #31.

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes (pages, components):
  - `frontend/src/lib/money.ts`, `parseEuroInput`: after removing spaces and `€`, the text must
    match one of these shapes, otherwise the result is `null`:
    - with a comma (FR-1): `\d+,\d{1,2}` or `\d{1,3}(\.\d{3})+,\d{1,2}`;
    - without a comma: `\d+` (whole euros), `\d+\.\d{1,2}` (decimal point), or
      `\d{1,3}(\.\d{3}){2,}` (two or more thousands dots).
    A single dot followed by exactly three digits matches none of them and is refused (FR-2), as
    are badly placed dots and more than two decimals. Cents are then built from the digits as
    integers, as today, with the same safe-integer check. The doc comment is updated with the rule.
  - `parseSignedEuroInput` keeps delegating to `parseEuroInput`, so negative balances follow the
    same rule (FR-1).
  - No change to any form: they already show their amount message when the parser returns `null`
    (FR-2), and `centsToEuroInput` output (`1234,56`, `-0,50`) still parses back (FR-3).
- Database migrations: none.

## Steps
1. Rewrite the shape check in `parseEuroInput` and update its doc comment.
2. Update `money.test.ts`: `1.500` moves from accepted to refused; add the accepted and refused
   examples from the acceptance criteria; keep the round-trip test with `centsToEuroInput`.
3. Update `revolving.test.tsx`, which types `1.500` as a debt limit, to type `1.500,00`.
4. Run typecheck, lint, format check and all front-end tests. Leave everything staged for review.

## Tests
- Unit tests (`money.test.ts`): every example of acceptance criteria 1 and 2, the signed variants
  (`-1.234,56`, `-1.234` refused), and the existing round trip for criterion 3.
- API / integration tests: none (no back-end change).
- Manual checks: in an amount field, type `1.500` and see the amount message; type `1.500,00`
  and `12.30` and save.
