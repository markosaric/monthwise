# Requirements: Mark planned features

- **Branch:** `docs/mark-planned-features`
- **Status:** Approved (2026-11-12)
- **Date:** 2026-11-12

> This document describes **what** the change must do, not **how** it is built.
> The implementation plan (`docs/plans/docs-mark-planned-features.md`) is written after these
> requirements are approved.

## 1. Goal

The project description in `docs/INIT.md` and the app itself promise more than is built today.
`docs/INIT.md` lists tables, rules and screens that do not exist yet (household, account, period,
balance snapshot, user, month closing and carryover, authentication), and the **Savings** screen
in the app opens to a page with only a title and no content. A reader of the document, or a user of
the app, cannot tell what works today and what is still planned.

After this change, everything that is planned but not built is clearly marked as **planned**, both
in `docs/INIT.md` and on the Savings screen, so the documentation matches the code.

## 2. Terms

| Term | Meaning |
|---|---|
| Built | Exists in the code on `main` and can be used in the app (or, for a table, exists in the database). |
| Planned | Described as a goal of Monthwise but not built yet. |

## 3. Functional requirements

**FR-1 Planned tables in the data model**
- In the data model of `docs/INIT.md` (section 3), every table that is not built is marked as
  planned. Today these are `household`, `account`, `period`, `balance_snapshot` and `user`.
- The marking is visible at a glance next to each such table, and a short note at the top of the
  data model says what the marking means.
- Tables that are built keep their current description.

**FR-2 Planned rules and features**
- Key rules and features in `docs/INIT.md` that are not built are marked as planned in the same way.
  Today these are at least:
  - month closing and carrying the actual balance over to the next month (opening carryover),
  - break-even income,
  - card statement reconciliation against the actual bank statement,
  - savings balances over time,
  - authentication (ASP.NET Core Identity) and users.
- The key rule "A month is generated from `recurring_item`, …" names records that exist today
  (recurring inputs, recurring bills, card purchases and loans) instead of the non-existent
  `recurring_item`.

**FR-3 Screens**
- In the list of screens (section 5 of `docs/INIT.md`), each screen or part of a screen that is not
  built is marked as planned (the Savings screen, month closing and carryover on the Month screen,
  statement reconciliation on the Cards screen).
- The plan of first steps (section 6) says that it is the original plan of the first steps and that
  the feature requirements in `docs/feature-requirements/` describe what is built.

**FR-4 The Savings screen says it is planned**
- The Savings screen stays in the menu and opens as today, with its title.
- Under the title it shows a short, friendly message that the screen is not built yet and that
  saving account balances over time is planned, so the empty page is not mistaken for missing data
  or an error.
- The message uses the existing look of the app (design refresh) and works on phone and desktop.

## 4. Non-functional requirements

- No data, database table or API changes.
- Other screens, the menu order and the Savings colour and icon stay as they are.
- The user manual already says that the Savings screen is not built yet and needs no change.

## 5. Out of scope

- Building any of the planned features (savings, month closing, authentication, reconciliation).
- Rewriting `docs/INIT.md` beyond marking what is planned and fixing the `recurring_item` name.
- Hiding the Savings screen from the menu.

## 6. Acceptance criteria

1. Every table in the data model of `docs/INIT.md` that has no matching table in the database on
   `main` is marked as planned; no built table is marked as planned.
2. Month closing, carryover, break-even income, statement reconciliation,
   savings and authentication are marked as planned wherever `docs/INIT.md` describes them.
3. `docs/INIT.md` no longer mentions `recurring_item`.
4. Opening **Savings** in the menu shows the title and a message that the screen is planned and not
   built yet, on phone and desktop.
5. All front-end tests and the build pass.
