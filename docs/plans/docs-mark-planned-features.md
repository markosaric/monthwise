# Plan: Mark planned features

- **Branch:** `docs/mark-planned-features`
- **Status:** In progress
- **Date:** 2026-11-12
- **Requirements:** [planned-features.md](../feature-requirements/planned-features.md)

## Goal
A reader of `docs/INIT.md` and a user of the app can tell what is built and what is still planned:
unbuilt tables, rules and screens are marked as planned, and the Savings screen says it is planned
instead of showing an empty page (code review item L7).

## Scope
- In scope: `docs/INIT.md` (sections 1, 3, 5 and 6), the Savings screen, a front-end test for it.
- Out of scope: building any planned feature, hiding Savings from the menu, the user manual (it
  already says the Savings screen is not built yet), any other document.

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes (pages, components):
  - `frontend/src/pages/SavingsPage.tsx`: keep the `PageHeader` ("Savings", "Account balances over
    time."), and add one white card in the existing design-refresh look (rounded card, green
    Savings left edge and `PiggyBank` icon from `sectionColors.savings`) with the text
    *"This screen is planned and not built yet. It will show how your savings account balances
    change over time."* (FR-4). Plain markup inside the page, no new shared component.
- Documentation changes (`docs/INIT.md`):
  - A legend at the start of section 3: *"(planned)" marks a table, rule or screen that is
    described here but not built yet; the feature requirements in `docs/feature-requirements/`
    describe what is built.*
  - Data model (FR-1): append `(planned)` to `household`, `account`, `period`, `balance_snapshot`
    and `user`. These are the only tables in the list without a `DbSet` in
    `MonthwiseDbContext`; every other row stays as it is.
  - Key rules (FR-2): replace `recurring_item` with recurring inputs, recurring bills, card
    purchases and loans; mark the carryover part of the "Available" rule and the "Closing a month"
    rule as planned.
  - Goal and monthly summary (FR-2): mark statement reconciliation, savings tracking, carryover
    (ADDITIONAL FUND) and break-even income as planned.
  - Architecture table (FR-2): mark the Authentication row as planned.
  - Screens (FR-3): mark Savings, month closing and carryover on the Month screen, and statement
    reconciliation on the Cards screen as planned.
  - Plan (FR-3): one line saying it is the original plan of the first steps and that the feature
    requirements describe what is built.
- Database migrations: none.

## Steps
1. Edit `docs/INIT.md` as described above.
2. Add the planned message to `SavingsPage.tsx`.
3. Add a test that opening `/savings` shows the title and the planned message.
4. Run front-end lint, tests and build; check the page on phone and desktop width.
5. Leave all changes staged for review.

## Tests
- Unit tests: none (no logic).
- API / integration tests: none.
- Front-end tests: `/savings` renders the heading "Savings" and the planned message.
- Manual checks: Savings screen on phone and desktop width; read through INIT.md and compare each
  `(planned)` mark with the code on `main` (acceptance criteria 1 to 3).

## Open questions
- None.
