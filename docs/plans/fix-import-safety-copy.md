# Plan: The safety copy survives a failed import

- **Branch:** `fix/import-safety-copy`
- **Status:** Done
- **Date:** 2026-11-13
- **Requirements:** [backup-and-restore.md, Appendix 2](../feature-requirements/backup-and-restore.md#appendix-2-the-safety-copy-survives-a-failed-import)

## Goal

A failed import, or a failed bringing back of the safety copy, leaves the previous safety copy as
it was, so an earlier wrong import can still be undone. Fixes finding M2 of the main branch code
review.

## Requirement changes

[Appendix 2](../feature-requirements/backup-and-restore.md#appendix-2-the-safety-copy-survives-a-failed-import)
changes FR-7, FR-9 and acceptance criterion 7 of the backup and restore requirements:

- The safety copy is replaced only when the import succeeds, and then holds the data as it was
  right before that import.
- A failed import leaves the previous safety copy exactly as it was (or still none).
- Bringing back the safety copy follows the same rule: on success the data it replaced becomes the
  new safety copy; on failure the safety copy stays as it was.
- The history lists a safety copy as made only when the import or bringing back that made it
  succeeded; a failed import is still listed as failed.

## Scope

- In scope: the order of the import steps in `ImportDataHandler.RunExclusiveAsync` (import and
  bringing back), and the transaction of `HouseholdDataStore.ReplaceAllAsync`.
- Out of scope: import size limits (M3); the export file format; the screens, which already show
  the safety copy and the history.

## Design

- Domain / data model changes: none.
- API changes (endpoints, DTOs): none. The answers stay the same; only what is kept after a
  failure changes.
- Application (`DataTransferHandlers.cs`):
  - `MakeSafetyCopyAsync` is split. Before the import, the handler only **reads and protects** the
    current data (`store.ReadAllAsync` and `codec.Protect`), as today, but saves nothing.
  - The safety copy, its "safety copy made" history record and the successful import record are
    saved in one step, `KeepSafetyCopyAsync`, which runs **inside the import's transaction**, after
    the new data is copied in and before the commit. So either the new data, the new safety copy
    and both history records are all saved, or none of them is.
  - A failed import (a failure result or an exception in the store) is recorded as failed, as
    today, and the previous safety copy is not touched.
- Port (`IHouseholdDataStore.ReplaceAllAsync`): gets a second argument,
  `Func<CancellationToken, Task> beforeCommit`, which the store calls inside its transaction after
  copying the data. The application stays free of database transactions.
- Infrastructure (`HouseholdDataStore.ReplaceAllAsync`):
  - Before calling `beforeCommit`, the store sets the `search_path` back to the household schema
    (the import schema it pointed to is dropped by then), so the regular saves land in the right
    tables.
  - On any failure, the transaction is rolled back and the change tracker is cleared, so the
    safety copy and history records staged by `beforeCommit` cannot be saved later by the
    failure record of the same request.
- Front-end changes: none.
- Database migrations: none.

## Steps

1. Add the `beforeCommit` argument to `IHouseholdDataStore.ReplaceAllAsync` and call it in
   `HouseholdDataStore` inside the transaction, with the `search_path` reset and the change tracker
   cleared on failure.
2. In `ImportDataHandler.RunExclusiveAsync`, read the current data before replacing it, and save the
   safety copy and the history records through `beforeCommit` only.
3. Update the existing data transfer tests and add the new ones below.
4. Run the full back-end test suite, `dotnet format` and the OpenAPI drift check.

## Tests

- API / integration tests (with the database), in `DataEndpointTests`:
  - A good import makes a safety copy; then an import that fails while the data is copied leaves
    that safety copy unchanged (same creation time), and bringing it back still restores the data
    from before the first import.
  - With no safety copy yet, a failing import leaves no safety copy.
  - A failing import adds a failed import record to the history and no "safety copy made" record.
  - Bringing back the safety copy that fails (a safety copy whose data breaks a database rule)
    leaves the safety copy unchanged.
  - The existing test that a successful import followed by bringing back the safety copy restores
    the original data still passes.
- Manual checks: none; the screens do not change.
