# Plan: Refuse malformed export files with a plain message

- **Branch:** `fix/export-file-validation`
- **Status:** In progress
- **Date:** 2026-11-11
- **Requirements:** none (no behaviour change). The fix makes the code meet the existing
  [backup-and-restore.md](../feature-requirements/backup-and-restore.md) FR-4 and acceptance
  criterion 5: a damaged or incomplete file is refused with a plain message and no data changes.

## Goal
An export file whose password is right but whose content is missing or has wrongly typed fields
(for example no `createdAt`) is refused with the plain "The file is incomplete" message, instead
of failing with a 500 error. This is item L1 of the main branch code review.

## Scope
- In scope: reading the decrypted content of an export file in
  `backend/src/Monthwise.Infrastructure/DataTransfer/ExportFileCodec.cs` (`Read` and its
  `Deserialize`), and its unit tests.
- Out of scope:
  - Size limits on uploads and decompression (review item M3). It changes the same file and builds
    on this fix, so it gets its own branch after this one.
  - The table and column checks in `HouseholdDataStore.CheckCompleteAsync`, which already refuse
    incomplete tables.
  - Safety copies (`Unprotect`): they are written by Monthwise itself with the installation key,
    and a broken one should still fail loudly.

## Design
- Domain / data model changes: none. The existing `DataTransferErrors.FileIncomplete` error is
  used, the same one already returned when `format`, `tables`, `summary` or `migration` is missing.
- API changes (endpoints, DTOs): none. A malformed file now gets the same 400 problem details as
  other incomplete files.
- Back-end changes:
  - `Deserialize` checks every field it reads, explicitly, before using it, and returns `null`
    (FileIncomplete) when one is wrong:
    - `createdAt` must be present and a valid date and time (read with `TryGetValue`, no `!`).
    - `release` is optional (stays `"unknown"` when missing) but must be a string when present.
    - each `summary` count is optional (stays 0) but must be a whole number of 0 or more when
      present.
    - the existing checks for `format`, `tables`, `summary` and `migration` stay.
  - The `catch` in `Read` stays as it is, as a safety net for errors from parsing and
    decompression. `NullReferenceException` is not added to it: after the change nothing in
    `Deserialize` can throw it, and catching it would hide real bugs.
  - To test content that `Write` would never produce, the encryption part of `Write` moves into a
    private `Seal(byte[] plain, string password)`, and an `internal` `WritePayload(JsonNode
    payload, string password)` (tests only, like `Iterations`) seals any JSON. `Write` keeps its
    behaviour and output format.
- Front-end changes: none; the import page already shows the error message of a 400.
- Database migrations: none.

## Steps
1. Split `Write` into `Serialize` plus `Seal`, add the internal `WritePayload` for tests.
2. Make `Deserialize` validate `createdAt`, `release` and the summary counts explicitly.
3. Add the unit tests below.
4. Run `dotnet format --verify-no-changes`, the back-end build and all back-end tests.

## Tests
- Unit tests (`ExportFileCodecTests`), each expecting `FileIncomplete` from `Read` with the right
  password:
  - `createdAt` missing, `null`, a number, or a string that is not a date;
  - `release` a number;
  - a summary count that is a string, a decimal or negative;
  - content that is valid JSON but not an object (for example `[]`);
  - content that is not gzip, and gzip that is not JSON.
  - A file without `release` and without summary counts still reads, with `"unknown"` and 0.
  - The existing tests stay green (round trip, wrong password, not an export file, safety copy).
- API / integration tests: none new; the import check endpoint already maps `FileIncomplete` to
  400 and has no other path for this case.
- Manual checks: none needed beyond the tests.
