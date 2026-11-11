# Requirements: Database tests are never skipped silently

- **Branch:** `test/database_tests_required`
- **Status:** Approved (2026-11-11)
- **Date:** 2026-11-11

> This document describes **what** the change must do, not **how** it is built.
> The implementation plan (`docs/plans/test-database_tests_required.md`) is written after these
> requirements are approved.

## 1. Goal

Most back-end API tests need a real PostgreSQL database, given by the `MONTHWISE_TEST_DB`
connection string. When the variable is not set, those tests are skipped. A local `dotnet test`
then ends without errors even though none of the database tests ran, and nothing in the output
says why. In CI the variable is set by the workflow, but if it ever went missing (a renamed
variable, an edited workflow, a new job), CI would turn green the same way, without testing the
database at all.

After this change a run without a test database is **visible** locally and **fails** in CI, so a
green result always means the database tests really ran (or the developer was told plainly that
they did not).

This is code review finding L6. Fixes that depend on database tests to prove them (L3, L12, M8)
build on it.

## 2. Terms

| Term | Meaning |
|---|---|
| Database test | A back-end test that needs a real PostgreSQL database to run. Today these are all API endpoint tests, the migration tests and the healthy-database health check test. |
| Test database setting | The `MONTHWISE_TEST_DB` environment variable with the connection string of the test database. |
| CI run | A test run in a continuous integration environment, recognised by the environment variable `CI` set to `true` (GitHub Actions and other CI services set it on every job). |
| Local run | Any test run that is not a CI run. |

## 3. Functional requirements

**FR-1 Local run without a test database: skipped with a clear notice**
- Database tests are still skipped, so a developer without PostgreSQL can run the other tests.
- The test run output shows a notice that is easy to spot without changing the output verbosity.
  It says that the database tests were skipped because `MONTHWISE_TEST_DB` is not set, and how to
  run them (set `MONTHWISE_TEST_DB` to a test database connection string, see the README).
- The notice is shown **once per test run**, not once per test.
- Each skipped test keeps a skip reason that names `MONTHWISE_TEST_DB`.

**FR-2 CI run without a test database: the run fails**
- In a CI run without the test database setting, every database test **fails** instead of being
  skipped, with a message that names `MONTHWISE_TEST_DB` and says that CI must provide a test
  database.
- The test run, and so the CI job, ends with an error.

**FR-3 Running the database tests locally on purpose**
- A developer can make a local run behave like a CI run (FR-2) by setting `CI=true`, to be sure the
  database tests ran before opening a pull request.

**FR-4 Runs with a test database are unchanged**
- When the test database setting is present, database tests run as today, locally and in CI.
- A set but empty or blank value counts as not set.
- A database that is set but cannot be reached makes the database tests fail, as today; it is
  never treated as a reason to skip.

**FR-5 Tests that need no database are unaffected**
- Domain tests and API tests that need no database (for example the unreachable-database health
  check, architecture, configuration folder, export file and cipher tests) run and pass with or
  without the test database setting, locally and in CI.

**FR-6 One rule for every database test**
- The same rule (FR-1 to FR-4) applies to every existing database test and to every database test
  added later, without each test class having to repeat or remember it.

**FR-7 Documentation**
- The README section on running the tests describes the local notice, the failure in CI and
  `CI=true` for local runs.

## 4. Non-functional requirements

- **NFR-1** No secret is added to the code or the documentation. The CI workflow keeps its
  throwaway connection string for the PostgreSQL service container.
- **NFR-2** No new test framework or service is required to run the tests.
- **NFR-3** The application itself (API, front-end, database) does not change.

## 5. Out of scope

- Starting a test database automatically for local runs (for example with containers).
- Front-end tests (they need no database).
- Changing what the database tests check.

## 6. Acceptance criteria

1. Locally, without `MONTHWISE_TEST_DB`: `dotnet test` in `backend` ends without errors, the
   database tests are reported as skipped, and the output shows the notice of FR-1 once.
2. Locally, with `CI=true` and without `MONTHWISE_TEST_DB`: `dotnet test` ends with an error and
   every database test fails with the message of FR-2.
3. Locally or in CI, with `MONTHWISE_TEST_DB` set to a working database: all tests run and pass,
   none is skipped.
4. With `MONTHWISE_TEST_DB` set to a blank value: behaves as in criterion 1 (locally) or 2 (CI).
5. With and without `MONTHWISE_TEST_DB`, the tests that need no database run and pass.
6. The CI workflow on the pull request runs all back-end tests with the test database and none is
   skipped.
7. The README describes the behaviour.
