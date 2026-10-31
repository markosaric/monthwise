# Plan: Backup and restore

- **Branch:** `feat/backup_and_restore`
- **Status:** In progress
- **Date:** 2026-10-31
- **Requirements:** [backup-and-restore.md](../feature-requirements/backup-and-restore.md) (approved)

## Goal
A household admin can export all household data from Settings to a password-protected file and
import it again, on every deployment target. An import replaces all data or changes nothing, a
safety copy is made first, and a history and a Dashboard reminder show how old the newest export
is.

## Scope
- In scope:
  - API: export, import check, import, safety copy restore, history, reminder setting and
    dismissal; the import gate; `dataImportedAt` in the health response.
  - Infrastructure: the export file format, reading and writing all household tables, the import
    through the EF Core migrations, the history, setting and safety copy tables.
  - Front-end: the Data and backups tile and section in Settings, the export and import dialogs,
    the history, the reminder setting and the Dashboard reminder.
  - Docs: ADR 0007, `INIT.md` data model, the Data and backups chapter of the user manual.
- Out of scope:
  - Everything in section 6 of the requirements.
  - **The login parts of FR-1, FR-8 and FR-9** (only a household admin can export and import,
    and who did what): login does not exist yet. This branch adds a `HouseholdAdmin`
    authorization policy that allows every request until login exists, and a nullable user in the
    history, shown as "—". The login feature makes the policy require the admin role, fills in the
    user and keeps the importing user signed in (FR-8). Decided 2026-10-31: login comes in a
    later phase; until then whoever uses Monthwise can export and import.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| FR-2, NFR-7 | The export reads **every table of the database**, except an explicit list of installation tables (`data_transfer_log`, `data_backup_settings`, `data_safety_copy`, `__ef_migrations_history`, and later the login tables). No per-feature export code is written, so a new feature's tables are exported without any work. A test fails when a table of the EF Core model is neither exported nor on the exclude list, and a round-trip test compares every table. |
| FR-2 | The export runs in one `REPEATABLE READ`, read-only transaction, so all tables come from one moment while normal use goes on. Rows are written as JSON objects of column name to value, exactly as stored (cents as integers, dates as ISO strings, Guids as strings). |
| FR-2 | The file holds a header (format, format version) and an encrypted body with `createdAt`, `release` (the informational version), `migration` (the id of the newest migration applied when it was made), `summary` (row counts for cards, accounts, loans, cash flow inputs, card purchases, bills, expenses and months with data) and `tables`. File name `monthwise-export-YYYY-MM-DD-HHmm.monthwise` in the installation's local time. |
| FR-3, NFR-1 | Password at least 6 characters (checked in the front-end and the API). The key is derived with PBKDF2-HMAC-SHA256 (`Rfc2898DeriveBytes.Pbkdf2`, 600,000 iterations, 16-byte random salt); the gzip-compressed body is encrypted with AES-256-GCM (12-byte random nonce), with the header as associated data. Built into .NET, no new package. The password is never logged; requests that carry it are excluded from request logging. |
| FR-3, NFR-4 | Card numbers and IBANs are **decrypted** with the installation's keys when exported (they are protected by the file password) and the `card_number_hash` and `iban_hash` columns are left out. On import they are encrypted again with the importing installation's active key and the hashes recomputed with its hash key ([ADR 0005](../decisions/0005-encrypted-sensitive-data.md)), so a file works with other keys. The columns are found through the EF model (the properties with the encrypted converters), not by name. |
| FR-4, FR-5 | Two calls: **check** decrypts and validates the file and answers its summary next to the current summary, without changing anything; **import** repeats the checks and needs the confirmation word `REPLACE`. Both take the file as `multipart/form-data` (up to 100 MB). The file is not kept on the server between the two calls. AES-GCM cannot tell a wrong password from a changed file, so both answer one message: "The password is wrong or the file is damaged." A file without the header is "not a Monthwise export file"; a file whose tables do not match its migration is "incomplete". |
| FR-6 | A file is accepted when its `migration` is one of the release's migrations (same or older). A newer one, or an unknown one, is refused with the release that made it. Old data is brought up to date by **the same EF Core migrations** that update a database (see "Import" below), so no extra upgrade code is written per release. Migrations are therefore never removed or squashed (written into ADR 0007). |
| FR-7 | The import is one database transaction: any failure rolls back everything. Before it starts, the current data is exported (same format) and stored in `data_safety_copy`, encrypted with the installation's active encryption key instead of a password; only the newest row is kept. Bringing the safety copy back uses the same import (and first makes a new safety copy, so it can be undone too). |
| FR-7 | During an import an in-process **import gate** answers `503` "An import is running" for every `/api` call except `GET /api/health` and `GET /api/data`; the transaction also locks the household tables (`ACCESS EXCLUSIVE`). One replica runs on every target, so an in-process gate is enough. |
| FR-7 | The health response gets `dataImportedAt` (the last successful import). The front-end already reads health every 30 seconds; when the value changes it invalidates all cached queries, so every open screen shows the new data. The importing screen does this at once. |
| FR-9 | `data_transfer_log`: `id`, `occurred_at`, `kind` (`Export`, `Import`, `SafetyCopy`, `SafetyCopyRestore`), `user_name?` (the login feature adds `user_id`), `succeeded`, `file_name?`, `release`, `file_created_at?`, `message?` (a plain reason for a failure, never data). Written in its own context after the work, so a failed import is still recorded. Not exported and not touched by an import. No API to edit or delete it. Shown newest first, 50 per page. |
| FR-10, FR-11 | `data_backup_settings` (one row): `reminder_enabled` (default true), `reminder_interval_days` (7, 14, 30, 60 or 90; default 30), `reminder_dismissed_at?`. The reminder is due when it is on, no successful export is newer than the interval, and it was not dismissed within the last interval. Dismissal is for the household (not per browser), so it works across devices. Not exported. |
| FR-1 | Tile **Data and backups** in a new **Data** group after Money out, green (savings) icon, subtitle from the last successful export ("Last export 3 days ago" / "No export yet"). |

### Import
1. Decrypt and validate the file (format, password, `migration` known, every table of that
   migration present with the expected columns).
2. Make the safety copy (FR-7) and write it in its own transaction.
3. In one transaction, with the household tables locked:
   - create a temporary schema `monthwise_import` and set the transaction's `search_path` to it;
   - apply the migrations **up to the file's migration** into it (the migration script from
     `IMigrator.GenerateScript`; the migrations use unqualified table names), empty every table
     (removing the predefined rows the migrations insert) and insert the file's rows, with card
     numbers and IBANs encrypted again and hashes recomputed;
   - apply the **remaining migrations** in the same schema, which brings the data up to the
     current release exactly as a database update would;
   - reset the `search_path`, delete all rows of the household tables in `public` and copy every
     table from `monthwise_import` with explicit column lists, in foreign key order;
   - drop `monthwise_import` and commit.
4. Write the history row (success or failure) and set `dataImportedAt`.

All keys are Guids generated by the application, so no sequences need resetting.

- Domain / data model changes:
  - Application: `DataTransfer/` with the handlers (export, check, import, safety copy restore,
    history, settings, reminder) and their contracts; ports `IHouseholdDataStore` (read all
    tables, replace all tables) and `IExportFileCodec` (write and read the file).
  - Infrastructure: `DataTransfer/` with the EF-model-driven table reader and writer, the
    migration-based import, the file codec, and the three new entities and configurations.
- API changes (endpoints, DTOs), all under the `HouseholdAdmin` policy except the reads:
  - `GET /api/data` → last export, last import, safety copy (date) and reminder setting.
  - `GET /api/data/history?page=` → history rows.
  - `POST /api/data/export` `{ password }` → the file (`application/octet-stream`,
    `Content-Disposition` with the file name, `Cache-Control: no-store`).
  - `POST /api/data/import/check` (file, password) → file summary, file date and release, current
    summary.
  - `POST /api/data/import` (file, password, confirmation) → summary of what was imported.
  - `POST /api/data/safety-copy/restore` `{ confirmation }`.
  - `PUT /api/data/reminder` `{ enabled, intervalDays }`; `POST /api/data/reminder/dismiss`.
  - `HealthResponse` gets `dataImportedAt`. OpenAPI document and generated client regenerated.
- Front-end changes (pages, components):
  - Settings: the Data group and tile; the Data and backups section (`pages/settings/data/`) with
    the last export and import, Export data and Import data buttons (hidden when the policy
    denies them), the reminder setting, the safety copy line and the history list.
  - Export dialog: password twice, a note on keeping the file and password safe; downloads the
    response as a blob with the file name from `Content-Disposition`.
  - Import dialog: file and password → check → side-by-side summaries and the word `REPLACE` →
    import → confirmation; all cached queries invalidated afterwards.
  - Dashboard: a dismissible reminder card linking to the section, shown when `GET /api/data`
    says the reminder is due (the Dashboard response stays as it is).
  - `useReloadAfterImport` in the app layout: invalidate all queries when `dataImportedAt` in the
    health response changes.
- Database migrations: `AddDataTransfer` (the three tables and the default settings row).

## Steps
1. **ADR 0007:** export format (EF-model-driven tables, password encryption, decrypted card
   numbers inside), import through the migrations in a temporary schema, migrations never
   removed. Update the ADR index and the data model in `INIT.md`.
2. **Tables and migration:** `data_transfer_log`, `data_backup_settings`, `data_safety_copy`, the
   exclude list and the architecture test (NFR-7).
3. **File codec:** header, PBKDF2 key, AES-GCM body, gzip, read errors as plain messages (FR-3,
   FR-4).
4. **Export:** table reader in one snapshot, sensitive columns decrypted, summary, history row,
   endpoint (FR-2, FR-9).
5. **Import:** validation, safety copy, migration-based import in a temporary schema, sensitive
   columns re-encrypted, import gate, `dataImportedAt`, history rows, endpoints (FR-4 to FR-8).
6. **Settings and reminder:** reminder setting, dismissal, `isDue` in the overview for the
   Dashboard (FR-10, FR-11).
7. **OpenAPI and client:** regenerate the document and the TypeScript client.
8. **Front-end:** Settings tile and section, export and import dialogs, history, reminder setting,
   Dashboard reminder, refresh after an import (FR-1 to FR-11, NFR-6).
9. **User manual:** the Data and backups chapter with Azure's own backups (point-in-time restore of
   PostgreSQL Flexible Server, 7 days by default) and links from the installation chapters
   (FR-12).

## Tests
- Unit tests:
  - File codec: round trip; wrong password and a changed byte both refused with the same message;
    a file without the header refused; password shorter than 6 characters refused.
  - Reminder: due, not due, turned off, dismissed and coming back after the interval.
- API / integration tests (PostgreSQL, as the existing tests):
  - Round trip (NFR-3, NFR-4): made-up cards (with numbers and IBANs), participants and payers,
    export, empty the data, import, and compare every household table row by row (card numbers and
    IBANs compared decrypted). The EF Core model caches one cipher per process, so the test checks
    that the numbers are encrypted again and hashed again on import instead of using a second set
    of keys in the same process.
  - Older file (FR-6): a file made at an earlier migration (built in the test by migrating a
    database only that far) imports and comes out at the current migration; a file with an unknown
    migration is refused.
  - All or nothing (FR-7): a failure injected during the copy leaves every table unchanged, writes
    a failed history row and keeps the safety copy; the safety copy restores the data from before.
  - Gate: a write during an import answers `503`; health answers.
  - History is not changed by an import; failed attempts do not count as the last export.
  - Architecture test: every entity type is exported or on the exclude list.
- Front-end tests (Vitest): password rules in the export dialog, the import confirmation needs
  `REPLACE`, the Dashboard reminder shows and hides, the tile subtitle.
- Manual checks:
  - Export on Docker one-click on a Mac and Windows, import into a native installation and into
    Azure, and the other way round.
  - Export and download on a phone.
  - Ten years of made-up data: export and import each under one minute (NFR-5).

