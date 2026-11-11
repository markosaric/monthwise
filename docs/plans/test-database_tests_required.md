# Plan: Database tests are never skipped silently

- **Branch:** `test/database_tests_required`
- **Status:** Done
- **Date:** 2026-11-11
- **Requirements:** [database-tests-required.md](../feature-requirements/database-tests-required.md)

## Goal
A back-end test run without `MONTHWISE_TEST_DB` says plainly, once, that the database tests were
skipped (local run) or fails them (CI run, `CI=true`), so a green run always means the database
tests ran or the developer was told they did not (code review finding L6).

## Scope
- In scope: `backend/tests/Monthwise.Api.Tests` (one place that decides run, skip or fail; the 12
  test classes that read `MONTHWISE_TEST_DB` today), the README section on running the tests.
- Out of scope: the application, the front-end tests, the CI workflow (it already sets
  `MONTHWISE_TEST_DB`, and GitHub Actions sets `CI=true`), starting a database automatically.

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes (pages, components): none.
- Database migrations: none.

**Test code (`Monthwise.Api.Tests`)**
- New `TestDatabase.cs`, the only place that reads `MONTHWISE_TEST_DB` and `CI` (FR-6):
  - `TestDatabase.Decide(string? connectionString, string? ci)` is a pure function that returns
    `Run`, `Skip` or `Fail`: a blank or missing connection string counts as not set (FR-4); `CI`
    equal to `true` (case-insensitive, as CI services set it) turns `Skip` into `Fail` (FR-2,
    FR-3).
  - `TestDatabase.ConnectionString` (property used by tests) returns the connection string, or
    calls `Assert.Skip` with the reason "MONTHWISE_TEST_DB is not set; database tests are skipped.
    Set it to a test database connection string to run them (see README)." (FR-1), or
    `Assert.Fail` with "MONTHWISE_TEST_DB is not set, but this is a CI run (CI=true). CI must
    provide a test database." (FR-2).
- `MonthwiseApiFactory.TestDatabaseConnectionString` is removed, so no test can read the variable
  past the rule (FR-6). The 12 classes replace their `Assert.SkipWhen(...)` line with
  `TestDatabase.ConnectionString`. Comments that say "skipped without MONTHWISE_TEST_DB" now
  point to `TestDatabase`.
- Unreachable database stays a failure: nothing catches connection errors (FR-4).

**One notice per run (FR-1)**
- A dedicated test, `TestDatabaseTests.Database_tests_notice`, is skipped with the notice as its
  reason on a local run without the setting, and passes otherwise. `dotnet test` shows the reason
  of every skipped test at the default verbosity, so the notice appears once per run:
  `NOTICE: MONTHWISE_TEST_DB is not set, so the database tests were skipped. Set it to a test
  database connection string to run them (see README, "Run the tests").`
- *Changed during implementation:* the first approach, an xUnit v3 assembly fixture writing the
  notice to the console, works when the test executable runs directly, but `dotnet test`
  (Microsoft Testing Platform) drops output written outside a test, even with `--output detailed`
  or `--show-stderr All`. An MSBuild message does not help either, because CI runs
  `dotnet test --no-build`.

**README** — "Run the tests" describes the notice, the failure with `CI=true` and how to use
`CI=true` locally (FR-7). The table of test commands is updated to match.

## Steps
1. Add `TestDatabase.cs` with `Decide`, `ConnectionString` and the messages.
2. Replace the skip in the 12 test classes, remove `TestDatabaseConnectionString`, update comments.
3. Add the notice test.
4. On the developer Mac (this cloud container cannot install .NET), run `dotnet format
   --verify-no-changes`, `dotnet build -c Release` and the acceptance runs below.
5. Update the README.
6. Leave everything staged for review.

## Tests
- Unit tests: `TestDatabaseTests` covers `Decide` for set / unset / blank connection string with
  `CI` unset, `true`, `TRUE` and `false` (FR-2 to FR-4), so the CI rule is tested without a CI run.
- API / integration tests: unchanged; they run against PostgreSQL in CI as before (acceptance
  criterion 6).
- Manual checks (acceptance criteria 1 to 5), from `backend`:
  - `dotnet test` without `MONTHWISE_TEST_DB`: passes, database tests skipped, notice once.
  - `CI=true dotnet test` without it: fails, every database test fails with the FR-2 message.
  - `MONTHWISE_TEST_DB=" " dotnet test` and with `CI=true`: same as the two runs above.
  - `MONTHWISE_TEST_DB=<local dev database> dotnet test`: all pass, none skipped.
  - The CI run of the pull request: back-end job green with zero skipped tests.

## Open questions
- None.
