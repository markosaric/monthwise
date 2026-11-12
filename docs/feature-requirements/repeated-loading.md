# Requirements: Load each part of the data once per request

- **Branch:** `fix/repeated-loading`
- **Status:** Approved (2026-11-12)
- **Date:** 2026-11-12

> This document describes **what** the change must do, not **how** it is built.
> The implementation plan (`docs/plans/fix-repeated-loading.md`) is written after these
> requirements are approved.

## 1. Goal

Every screen of Monthwise is calculated from the whole history: all loans, cards, bills, expenses,
inputs, purchases and shared costs. Today one request reads the same tables from the database many
times, because every calculation it combines reads them again. Measured on a nearly empty database,
one Dashboard request runs about 330 SQL statements and reads the cards table 59 times, the bills
table 41 times and the loans table 17 times; the monthly report runs about 120 statements and a
one-year cash flow chart about 57. The Dashboard still answers in about 0.1 s today, but the time
grows with every month of history, so this is a growth risk rather than a problem the user sees now.

After this change a request reads each part of the data at most once and reuses it for every
calculation in that request. What the user sees does not change, only how much work the server does
to show it.

This is code review finding H4.

## 2. Terms

| Term | Meaning |
|---|---|
| Request | One call of the API by the front-end, for example loading the Dashboard or saving a bill. |
| Read-only request | A request that shows data and changes nothing (the `GET` endpoints). |
| Changing request | A request that creates, changes or deletes data (the `POST`, `PUT`, `PATCH` and `DELETE` endpoints). |
| Whole-table read | Reading all rows of one kind of data (all cards, all bills, all loans and so on), which the calculations need. |
| Seeded history | Made-up test data that covers several years of normal household use, used to measure the change. |

## 3. Functional requirements

**FR-1 Every response stays the same**
- Every endpoint returns exactly the same status code and response body as before the change, for
  the same data and the same day.
- This covers all screens and figures: Dashboard, month lists, monthly report, cash flow chart,
  Loans, Cards, card installments, revolving cards, Shared costs, Recurring bills, Settings lists,
  and the export file.

**FR-2 A read-only request reads each table at most once**
- Within one read-only request, each whole-table read happens at most once, however many
  calculations of that request use the data.
- Reads of a single record or a check (for example "is this name already used?") may still happen
  where they are needed, but they do not repeat a whole-table read already done in the request.

**FR-3 A changing request answers with the saved state**
- When a changing request saves data and then answers with calculated figures (for example the
  updated loan with its schedule, or the month list after a bill is paid), the answer reflects what
  was saved in that request, exactly as today.
- A check made before saving (for example whether an account can still be charged) sees the same
  data it sees today, including changes the request has made but not yet saved, where it does so
  today.

**FR-4 Nothing is reused across requests**
- Data read in one request is never reused by another request. Every request starts from what is
  stored in the database at that moment, so a change saved by one request (or by a restore of an
  export file) is visible to the next request, as today.

**FR-5 Fewer statements on the heavy screens**
- Measured on the seeded history, the number of SQL statements of these requests drops at least to
  the following, and their response time does not get worse:

  | Request | Today (nearly empty database) | After the change, at most |
  |---|---|---|
  | Dashboard | about 330 | 40 |
  | Monthly report (one month) | about 120 | 30 |
  | Cash flow chart (one year) | about 57 | 30 |

- The numbers before and after, on both the nearly empty database and the seeded history, are
  written into the plan of this branch.

**FR-6 Guarded by tests**
- Automated back-end tests fail if a Dashboard, monthly report or cash flow chart request again
  reads one table more than once, so the problem cannot return unnoticed.
- The existing tests keep passing unchanged, which shows FR-1.

## 4. Non-functional requirements

- **NFR-1** No database migration and no change to the API contract (OpenAPI document and the
  generated front-end client stay the same).
- **NFR-2** The front-end does not change.
- **NFR-3** No new service (for example a shared cache server) is needed to run Monthwise.
- **NFR-4** Memory use per request stays proportional to the data one request already reads today;
  nothing is kept in memory after the request ends.
- **NFR-5** The seeded history uses made-up values only and no secret is added to the code.

## 5. Out of scope

- Reading only part of a table (for example only the months a screen shows). The calculations need
  the whole history for balances and schedules; narrowing the reads is a later change, only if the
  measurements in FR-5 show it is needed.
- Reusing data across requests or between users.
- The remaining part of review finding L9: loading a single card (`GET /api/cards/{id}`) still
  calculates every card. It reads each table only once already, so this change does not solve it;
  it follows as its own fix after this one.
- Concurrent changes by two requests at the same time (review finding M8).

## 6. Acceptance criteria

1. All existing back-end and front-end tests pass without changes to what they check.
2. With the seeded history, the Dashboard, monthly report and one-year cash flow chart stay within
   the statement counts of FR-5, and their response times are not worse than before.
3. A test request to the Dashboard, the monthly report and the cash flow chart reads no table more
   than once (FR-2, FR-6).
4. After a changing request (for example paying a bill, changing a loan payment, adding a card
   purchase, adding a shared cost contribution) the response and the next read-only request show
   the saved change (FR-3, FR-4).
5. After restoring an export file, the next request shows the restored data (FR-4).
6. No migration is added and the OpenAPI document is unchanged (NFR-1).
