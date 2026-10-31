# 0007: Data export and import through the migrations

- **Status:** Accepted
- **Date:** 2026-10-31

## Context
The user must be able to save all household data to a file from Settings and bring it back, on
Docker one-click, the native background service and Azure, also into a newer release and into an
installation with other encryption keys
([requirements](../feature-requirements/backup-and-restore.md)). A database backup was rejected
there: it is tied to the database and the installation's keys. Writing export and import code per
feature would be forgotten sooner or later.

## Decision
- **Every table is household data** except a short list of installation tables
  (`__ef_migrations_history`, `data_transfer_log`, `data_backup_settings`, `data_safety_copy`;
  later the login tables). The export reads every other table of the database as JSON rows
  (`to_jsonb`), in one `REPEATABLE READ` read-only transaction, so a new feature's tables are
  exported without extra code. A test fails when a table of the EF Core model is neither exported
  nor listed as an installation table.
- **The file:** the header `MWEXPORT`, a format version, the PBKDF2 iteration count, salt and
  nonce, then the gzip-compressed JSON (`createdAt`, `release`, `migration`, `summary`, `tables`)
  encrypted with AES-256-GCM under a key derived from the user's password
  (PBKDF2-HMAC-SHA256, 600,000 iterations), with the header as associated data. Built into .NET.
- **Card numbers and IBANs are readable inside the file** (the password protects them) and their
  hashes are left out. An import encrypts them with the importing installation's active key and
  hashes them with its hash key ([ADR 0005](0005-encrypted-sensitive-data.md)), so a file works on
  an installation with other keys.
- **Import through the migrations.** In one transaction, with the household tables locked: create
  a temporary schema, run the migration script up to the file's migration in it (the migrations use
  unqualified names), replace the rows the migrations added with the file's rows
  (`jsonb_populate_recordset`), run the remaining migrations, then empty the household tables and
  copy every table over in foreign key order. Any failure rolls everything back. Old data is
  brought up to date by the same code that updates a database, so no upgrade code per release.
- **Safety copy:** right before an import the current data is exported in the same format,
  protected with the installation's active encryption key, and kept in `data_safety_copy` (only the
  newest).
- **One import at a time:** a process-wide gate refuses other API calls with `503` while an import
  runs; every target runs one instance.

## Consequences
- **Migrations are never removed or squashed**, because an older file needs them to be imported.
  If that ever has to change, a release must name the oldest file format it still imports.
- Migrations must keep using unqualified table names and must not depend on data outside the
  household tables.
- A file is as complete as the database: there is no per-feature list to keep up to date, but an
  installation table must be added to the exclude list on purpose.
- AES-GCM cannot tell a wrong password from a damaged file; both get the same message.
- The file is not readable outside Monthwise; a later feature could add a way to decrypt it.
