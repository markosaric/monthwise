# Plan: Size limits for imported files

- **Branch:** `fix/import-size-limit`
- **Status:** Done
- **Date:** 2026-11-13
- **Requirements:** [backup-and-restore.md, Appendix 3](../feature-requirements/backup-and-restore.md#appendix-3-size-limits-for-imported-files)

## Goal

An import file larger than 10 MB, or one whose content unpacks to more than 50 MB, is refused
with a plain message before it can use up the memory of the installation. The same unpacked limit
applies when the safety copy is brought back. Fixes finding M3 of the main branch code review.

## Requirement changes

[Appendix 3](../feature-requirements/backup-and-restore.md#appendix-3-size-limits-for-imported-files)
changes FR-6, FR-7 and acceptance criterion 5 of the backup and restore requirements:

- A file larger than 10 MB is refused before it is read: *This file is too large to be a
  Monthwise export file (more than 10 MB).*
- A file whose content unpacks to more than 50 MB is refused once that size is reached: *This
  file is too large to be a Monthwise export file.*
- Both are listed in the history as failed imports; a refused check is not listed.
- The 50 MB limit also applies when the safety copy is brought back; a safety copy that is too
  large is refused and stays as it was.
- The user manual names the 10 MB limit.

## Scope

- In scope: the upload limit of the import endpoints, a capped unpacking in `ExportFileCodec`,
  the two new refusals, the import dialog, the user manual.
- Out of scope: streaming the whole import instead of holding the file in memory (the limits make
  that unnecessary); export, which has no limit (Appendix 3).

## Design

- Domain / data model changes:
  - New `DataTransferLimits` with `MaxFileBytes = 10 MB` and `MaxContentBytes = 50 MB`, the one
    place for both limits. It lives in the domain, next to the errors whose messages name the
    limit, so the API, the application and the codec all use it.
  - Two new errors in `DataTransferErrors`: `FileTooLarge` (the 10 MB message, field `file`) and
    `ContentTooLarge` (the unpacked message, field `file`), both validation errors like the other
    file refusals.
- Application:
  - `ImportDataHandler.ReadFile` refuses a file longer than `MaxFileBytes` with `FileTooLarge`
    before the codec reads it. Because it runs inside `RunExclusiveAsync`, an import refused this
    way is recorded as failed (FR-6), and a check is not, as today.
  - `IExportFileCodec.Unprotect` returns `Result<HouseholdData>` instead of throwing, so a safety
    copy that unpacks past the limit is refused with `ContentTooLarge` through the existing
    `load` failure path and stays as it was.
- Infrastructure (`ExportFileCodec`):
  - `Decompress` copies in blocks and stops with `ContentTooLarge` as soon as more than
    `MaxContentBytes` would be written, so the rest is never unpacked. `Read` and `Unprotect` return
    that error.
- API (`DataEndpoints`):
  - `MaxFileBytes` moves to `DataTransferLimits`. The request size limit of the two import
    endpoints becomes `MaxFileBytes` plus 1 MB for the form fields, so a file just over 10 MB still
    reaches the handler and gets the plain message.
  - `ReadAsync` copies at most `MaxFileBytes + 1` bytes, which is enough for the handler to see
    that the file is too large without holding all of it.
- Front-end (`DataDialogs.tsx`):
  - The import dialog checks the size of the chosen file and shows the 10 MB message at once,
    without uploading it.
  - A 413 answer (a request above the server's limit) shows the same message.
- Docs: the Data and backups chapter of the user manual (`docs/user-manual/index.html`) says that
  an export file can be at most 10 MB.
- Database migrations: none.

## Steps

1. Add `DataTransferLimits` and the two errors in the domain.
2. Cap `Decompress`; make `Unprotect` return a result and update the restore path.
3. Check the file length in `ReadFile`; lower the endpoint limits and cap `ReadAsync`.
4. Import dialog: size check before upload and the 413 message.
5. User manual line.
6. Tests below, full back-end and front-end test runs, `dotnet format`, OpenAPI and client drift.

## Tests

- Unit tests (`ExportFileCodecTests`):
  - A file whose content unpacks to just over 50 MB (a small, highly compressible file) is refused
    with `ContentTooLarge`; one just under is read.
  - `Unprotect` of a safety copy over the limit returns `ContentTooLarge`.
- API / integration tests (`DataEndpointTests`):
  - Checking a file of 10 MB plus one byte gives 400 with the 10 MB message on `file`, and adds no
    history record.
  - Importing that file gives the same message, changes nothing, and adds one failed import record.
  - Importing a small file that unpacks past 50 MB gives the unpacked message and changes nothing.
  - A safety copy that unpacks past 50 MB is refused when brought back and stays as it was.
- Front-end tests (`dataTransfer.test.tsx`):
  - Choosing a file over 10 MB shows the message and sends no request.
  - A 413 answer shows the same message.
- Manual checks: in the running app, pick a file over 10 MB in the import dialog and see the
  message.
