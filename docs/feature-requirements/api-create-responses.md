# Requirements: Consistent answers when the API adds an item

- **Branch:** `fix/api_inconsistencies`
- **Status:** Approved (2026-11-12)
- **Date:** 2026-11-12

> This document describes **what** must change, not **how** it is built.
> The implementation plan (`docs/plans/fix-api_inconsistencies.md`) is written after these
> requirements are approved.

## 1. Goal

When the API adds a new item, it answers in two different ways today. Adding a card or a
participant answers **201 Created** with the address of the new item, while adding anything else
(an input, a bill, a loan, a card purchase, a category, a balance entry and so on) answers
**200 OK**. A client cannot rely on one rule, and the API description (OpenAPI) shows the same
kind of action two ways.

After this change, every endpoint that adds a new item answers the same way: **201 Created**.

This is finding L9 of the main branch code review (first part). Nothing changes for the user in
the app: every screen keeps working exactly as before.

## 2. Terms

| Term | Meaning |
|---|---|
| Create endpoint | An API endpoint that adds one new item, for example `POST /api/inputs`. |
| Item address | The API address where the new item can be read, edited or deleted, for example `/api/inputs/{id}`. |

## 3. Functional requirements

**FR-1 Create endpoints answer 201 Created**
- Every create endpoint answers **201 Created** when the item is added. This covers adding:
  inputs, input categories, balance entries, card issuers, banks, loans, loan payment changes,
  loan categories, loan subcategories, card purchases, revolving card extra payments, revolving
  card debt entries, expense categories, recurring bills, bills, expenses and cash withdrawals.
- Cards and participants already answer 201 Created and stay as they are.

**FR-2 The body stays the same**
- The response body of each create endpoint is unchanged: the same fields as today's 200 answer.

**FR-3 Address of the new item**
- The answer carries the **address of the new item** (the `Location` header) wherever the new item
  has its own address in the API, for example `/api/bills/{id}` for a new bill or
  `/api/expense-categories/{id}` for a new expense category.
- A loan payment change is part of its loan and the answer returns the whole loan, so its address
  is the address of the new payment change (`/api/loans/{id}/payment-changes/{changeId}`).
- Card issuers and banks have no address of their own in the API, so their answer has no address.

**FR-4 Errors stay the same**
- Validation errors (400), missing items (404) and conflicts (409) are answered exactly as today.

**FR-5 The API description matches**
- The published API description (OpenAPI document) shows 201 for every create endpoint, and the
  front-end's generated API types are updated from it.

## 4. Non-functional requirements

- **NFR-1** No change to stored data, no database migration, no change to the export file format.
- **NFR-2** No visible change in the app; all existing screens that add items keep working.

## 5. Out of scope

- Endpoints that change, mark, hide, show, stop, restart, close, archive or reverse an existing
  item. They do not add a new item and keep answering 200 OK.
- Actions that add several items at once or work on data as a whole: *Add recurring bills* to a
  month, export, import and safety copy restore. They keep answering 200 OK.
- The other two parts of review finding L9 are not behaviour changes and need no requirements:
  - moving the card activity query from the infrastructure layer to the application layer, next to
    the similar queries, is done on this branch as a refactor (see the plan);
  - loading a single card without loading everything is left for after review finding H4, which
    adds the per-request reuse of loaded data that solves it.

## 6. Acceptance criteria

1. Adding each item listed in FR-1 through the API answers 201 Created with the same body as
   before.
2. Where the item has its own address (FR-3), the answer's `Location` header points to it.
3. Adding a card issuer or a bank answers 201 Created without a `Location` header.
4. An invalid request to any create endpoint still answers 400, 404 or 409 as before.
5. The OpenAPI document lists 201 for every create endpoint, and CI's OpenAPI check passes.
6. Adding items in the app (inputs, bills, expenses, loans, card purchases, categories, balance
   entries, extra payments, debt entries, card issuers, banks) works as before.
