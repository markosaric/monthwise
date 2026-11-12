# Plan: Load each part of the data once per request

- **Branch:** `fix/repeated-loading`
- **Status:** In progress
- **Date:** 2026-11-12
- **Requirements:** [repeated-loading.md](../feature-requirements/repeated-loading.md)

## Goal
A request reads each table at most once and reuses the rows for every calculation in that
request, so the Dashboard, the monthly report and the cash flow chart no longer slow down with
every month of history (code review finding H4). Nothing the user sees changes (FR-1).

## Measurements before the change

Measured with the new `RequestLoadingTests` (see Tests): every SQL statement EF Core runs during
one request is collected and grouped by the table it reads. The seeded history is three years of
made-up data: 4 cards (two accounts, a credit card with installments, a revolving card), 3 loans
(one shared), 2 inputs, 4 bills (one shared) and up to 13 expenses each month, and one card
purchase in 6 installments each month. The timings are from the test container and only show the
trend.

| Request | Empty: statements | Seeded: statements | Seeded: time | Most read tables (seeded) |
|---|---|---|---|---|
| `GET /api/dashboard` | 330 | 332 | 141 ms | card 59, bill 42, expense 23, expense_category 23, cash_flow_input 22, loan and 8 other tables 17 each |
| `GET /api/cash-flow/report` (one month) | 120 | 120 | 44 ms | card 21, bill 14, cash_flow_input 9, expense 8 |
| `GET /api/cash-flow/chart` (12 months) | 57 | 57 | 30 ms | card 10, bill 7, cash_flow_input 4 |

The Dashboard reads 21 different tables, so a request that reads each table once runs about
20 to 25 statements. That is well within the limits of FR-5 (40, 30 and 30).

## Scope
- In scope:
  - A per-request store of loaded rows in the infrastructure layer, used by every repository's list
    methods (FR-2).
  - Dropping the store at every save and when an export file is restored (FR-3, FR-4).
  - The statement-count tests and the before and after measurements (FR-5, FR-6).
- Out of scope:
  - Reading only part of a table, for example by date range (requirements, section 5). The
    measurements show it is not needed now.
  - `AsNoTracking`, which the review also suggested. The change handlers change the very rows the
    ledgers list (loan payments, revolving bills, contributions) and save them through the same
    context, so the rows have to stay tracked. Once each table is read once per request, tracking
    costs the same as one read, so dropping it gains little and puts saving at risk.
  - Reusing the calculated books (`LoanBook`, `CardPaymentBook` and so on) inside a request. They
    are calculated in memory from rows that are already loaded. If the after numbers show that the
    time goes into calculating rather than into the database, this is a follow-up.
  - Review finding L9, third part (`GetCardHandler`): a separate fix after this one.

## Design
- Domain / data model changes: none.
- Persistence (`Monthwise.Infrastructure`):
  - New internal `LoadedRows` class, one per `MonthwiseDbContext` (and so one per request). For
    each entity type it keeps the whole-table list read in this request, and loads it on first
    use.
  - Every repository list method answers from the whole-table list, filtered in memory with the
    same condition as today's SQL. For example `ListByMonthAsync(month)` becomes "all bills where
    `Month == month`", and `ListReceivedUpToAsync(last)` becomes "all inputs received up to
    `last`". The filters only compare dates, months and ids, so the result is the same in memory
    and in SQL. `ListOccurrenceKeysAsync` and `ListSkippedAsync` are built from their lists the
    same way.
  - Exception for unsaved changes (FR-3): when the context holds added, changed or deleted rows of
    an entity type, its *filtered* list methods read from the database as today. A filter in
    memory would then use the changed values instead of the stored ones. Whole-table lists can
    still be reused, because a new read returns the same tracked objects anyway.
  - Single-row reads (`GetAsync`, `FindByNameAsync`) and checks (`AnyAsync`, `NameInUseAsync`,
    `IsUsedAsync`) stay database queries as today (FR-2, second bullet).
  - `MonthwiseDbContext.SaveChangesAsync` (and `SaveChanges`) clears `LoadedRows` after a
    successful save, so everything calculated after a save is read fresh (FR-3). If the save
    fails, the store is cleared too.
  - `HouseholdDataStore.ReplaceAllAsync` (restore) and any other raw-SQL write clear the store and
    the change tracker after they write (FR-4). Contexts are scoped to the request, so nothing ever
    carries over into the next request (FR-4, NFR-4).
  - The lists handed out are read-only copies, so a caller can't change what another caller sees.
- API changes (endpoints, DTOs): none. The OpenAPI document stays the same (NFR-1).
- Front-end changes (pages, components): none (NFR-2).
- Database migrations: none (NFR-1).

## Steps
1. Add `StatementCounter` to the API tests. It collects the SQL text of every command EF Core runs,
   through EF Core's `DiagnosticListener`. Add `RequestLoadingTests` with the seeded history from
   the measurements above.
2. Add `LoadedRows` and clear it in `MonthwiseDbContext` after every save.
3. Move the list methods of all repositories onto `LoadedRows`, one repository file at a time: cards
   and card lists, cash flow, expenses, loans, card purchases, revolving, shared costs.
4. Clear the store after a restore in `HouseholdDataStore`.
5. Run all back-end tests, then repeat the measurements and write the after numbers into this plan.
6. Update `docs/INIT.md` if it describes how data is loaded (one sentence on the per-request
   store).

## Tests
- Unit tests: none new in the domain; nothing changes there.
- API / integration tests (real PostgreSQL):
  - `RequestLoadingTests`: on the seeded history, `GET /api/dashboard`, the monthly report and the
    12-month chart each read no table more than once and stay within the statement limits of FR-5
    (FR-2, FR-6).
  - A changing request answers with the saved state, and the next request shows it: paying a bill
    (month list and Dashboard), changing a loan payment (the loan and its schedule), adding a card
    purchase (card payments), receiving a shared cost contribution (Shared costs) (FR-3, FR-4,
    acceptance criterion 4).
  - A restore followed by `GET /api/dashboard` shows the restored data (acceptance criterion 5).
  - All existing endpoint tests pass unchanged (FR-1, acceptance criterion 1).
- Manual checks: open the Dashboard, Month, Loans, Cards and Shared costs screens on the
  development database and compare them with `main`.

## Open questions
- None.
