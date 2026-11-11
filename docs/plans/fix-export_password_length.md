# Plan: Longer export file password

- **Branch:** `fix/export_password_length`
- **Status:** Draft
- **Date:** 2026-11-11
- **Requirements:** [backup-and-restore.md, Appendix 1](../feature-requirements/backup-and-restore.md#appendix-1-longer-export-file-password)

## Goal
Every new export file is protected with a password of at least 12 characters, and the export
dialog tells the user how long the password must be while they type. Files exported earlier with a
shorter password can still be imported.

## Requirement changes
[Appendix 1](../feature-requirements/backup-and-restore.md#appendix-1-longer-export-file-password)
changes FR-3 and acceptance criterion 3:

- The export password must be at least 12 characters long (was 6). A shorter one blocks the export
  with *The password must be at least 12 characters long.*
- The export dialog shows a hint under the password field: *At least 12 characters. A phrase of a
  few unrelated words is easy to remember and hard to guess.* While typing it shows how many
  characters are still missing (*4 more characters needed*) and *Long enough* once the minimum is
  reached.
- Import accepts the password of any existing export file, whatever its length.
- The user manual says *at least 12 characters*.

**Why:** code review finding L2. The file holds all household data, including full card numbers
and IBANs, and the key derivation (PBKDF2, 600k rounds) cannot make up for a 6 character password.

## Scope
- In scope: the export password rule (back end and export dialog), the hint, the API summary, the
  user manual sentence, tests.
- Out of scope: import (it keeps checking only that a password is entered), the file format and
  key derivation, a strength meter or dictionary checks.

## Design
- Domain / data model changes: `DataTransferErrors.PasswordMinLength` goes from 6 to 12; the
  `PasswordTooShort` message follows the constant.
- API changes (endpoints, DTOs): none in shape. `POST /api/data/export` returns 400 with
  `data.password_too_short` for passwords under 12 characters. The endpoint summary in
  `DataEndpoints.cs` says *at least 12 characters*. Import endpoints are unchanged.
- Front-end changes (pages, components):
  - `DataDialogs.tsx`: `minPasswordLength = 12`; a hint line under the password field
    (`aria-describedby` on the input) that shows the missing count or *Long enough*.
  - `texts.ts`: `tooShort` built from the minimum; new `hint`, `missing(n)` and `longEnough`
    texts.
- Database migrations: none.

## Steps
1. Raise `PasswordMinLength` to 12 and update the export endpoint summary.
2. Update the export dialog and its texts with the new minimum and the hint.
3. Update the user manual (`docs/user-manual/index.html`) to *at least 12 characters*.
4. Update and add tests (below), run back-end and front-end tests, lint and build.
5. Set this plan's status to `Done`.

## Tests
- Unit tests: none needed beyond the API test; the rule is a single constant comparison.
- API / integration tests (`DataEndpointTests`):
  - test password constant becomes a 12+ character value;
  - export with an 11 character password returns 400, with 12 characters succeeds;
  - a file exported with a 6 character password (written directly with the codec) can still be
    checked and imported.
- Front-end tests (`dataTransfer.test.tsx`): an 11 character password shows the too-short error
  and sends nothing; the hint shows the missing count while typing and *Long enough* at 12; a 12
  character password that matches is sent.
- Manual checks: open Settings → Data and backups → Export data, type a password and watch the
  hint; export with 12 characters and import the file again.

## Open questions
- None.
