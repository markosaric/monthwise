# Plan: Backup before an update, no automatic undo of migrations

- **Branch:** `fix/migration_backup`
- **Status:** In progress
- **Date:** 2026-11-12
- **Requirements:** [production-packaging.md](../feature-requirements/production-packaging.md),
  [Appendix 3](../feature-requirements/production-packaging.md#appendix-3-backup-before-an-update-no-automatic-undo-of-migrations)
  (approved 2026-11-12); code review of `main`, finding L10

## Goal
A failed update never removes data. Before every Docker or native update, the update command saves
a full copy of the database. When the new release cannot bring the database up to date, the update
command restores that copy and starts the previous release again.

## Requirement changes
[Appendix 3: Backup before an update, no automatic undo of migrations](../feature-requirements/production-packaging.md#appendix-3-backup-before-an-update-no-automatic-undo-of-migrations):
- **FR-3:** when bringing the database up to date fails, the application reports it in the health
  response and the log, and never undoes the changes it has already made. The update backup is how
  the data is brought back.
- **FR-3a (new):**
  - Docker and native updates first save an **update backup**. When the backup fails, nothing is
    updated.
  - The last three backups are kept, named with their date, time and the version they were taken
    from, and only the user's account can read them.
  - When the new release fails, the update command restores the backup exactly and starts the
    previous release again. On Docker the installation stays fixed to that version (Appendix 2).
  - When the restore fails too, Monthwise stays stopped, and the update command says where the
    backup is and points to the manual's steps for restoring it by hand.
- **FR-8, FR-9:** the update commands make the update backup and restore it on failure.
- **FR-10:** on Azure the managed database's own backups are the update backup. The deploy step
  says how to restore to the time just before the deploy.
- **Acceptance criteria 3, 5, 6:** these are extended to match. The user manual says where the
  update backups are and how to restore one by hand.

## Scope
- In scope:
  - `MigrateDatabaseAsync` without the automatic revert, and its test;
  - update backup, keep-three cleanup and automatic restore in `deploy/docker/monthwise.sh`,
    `deploy/docker/monthwise.ps1`, `deploy/native/monthwise.sh` and `deploy/native/monthwise.ps1`;
  - the restore hint in `deploy/azure/deploy.sh`;
  - the user manual's installation chapter, and a note in ADR 0006.
- Out of scope:
  - a separate `restore` command. The manual describes restoring by hand with the PostgreSQL tools
    that come with each installation;
  - backups on `start`, or on a schedule. Export files stay the main backup (backup and restore
    feature);
  - automatic restore on Azure.

## Design
- Domain / data model changes: none.
- API changes: none. The health response already reports `migrations: failed` with `503`.
- Front-end changes: none.
- Database migrations: none.
- Back-end, `Infrastructure/DependencyInjection.cs`:
  - `MigrateDatabaseAsync` keeps the migration status (`Applied` / `Failed`), logs the failure and
    rethrows. `RevertMigrationsAsync` and the `IMigrator` / `Migration` usings it needs are removed.
  - EF Core runs each migration in its own transaction, so after a failure the database stays at
    the last migration that succeeded. The migration gate keeps every call except `/api/health` on
    `503`, as today.
- Update backup, the same behaviour in all four update commands:
  1. Make sure the database is running (Docker: `compose up --detach db` and wait for
     `pg_isready`; native: the PostgreSQL agent or task is already running).
  2. Take the version the backup is from: Docker uses the tag in `MONTHWISE_IMAGE` (`latest` for
     older installations); native uses the version in the health response, or `unknown` when
     Monthwise does not answer.
  3. Run `pg_dump --format=custom` into
     `<backup folder>/monthwise-<yyyyMMdd-HHmmss>-<version>.dump`, writing to a `.partial` file
     first and renaming it once `pg_dump` succeeds, so a half-written file never looks like a
     backup. When `pg_dump` fails, the update stops with "The update backup failed, nothing was
     updated" and the reason, and the running release is not touched.
  4. Delete all but the newest three `monthwise-*.dump` files. The names start with the time, so
     sorting by name gives the order.
  5. Update as today.
  6. When the new release does not become ready (the existing `wait_until_ready` / `Wait-Ready`,
     which already fails on the `503` of a failed migration), restore:
     - stop the app (Docker: `compose stop app`; native: the existing agent or task stop);
     - drop and recreate the `monthwise` database (`dropdb --force`, `createdb`), then
       `pg_restore --no-owner --exit-on-error` from the update backup;
     - start the previous release: Docker sets `MONTHWISE_IMAGE` back to its earlier value and runs
       `compose up --detach`; native moves `app.previous` back, as today;
     - when it is ready, say: "The update failed. Your data is as it was before the update. Update
       backup: <file>. Log: monthwise.sh logs".
  7. When any restore step fails, stop the app, keep it stopped, and say where the update backup is
     and that the manual's "Restoring an update backup by hand" section explains the rest.
- Where the update backups are:
  - Docker on Mac and Linux: `<Monthwise folder>/docker-update-backups`, next to the `docker`
    configuration folder and not inside it, because the app container mounts that folder. On
    Windows: `%LOCALAPPDATA%\Monthwise\docker-update-backups`.
  - Native: `<native folder>/update-backups`. `uninstall` keeps it, and `uninstall --data` deletes
    it with the rest of the native folder.
  - Docker `remove --data` also deletes the update backups, because they are a copy of the data.
- Access:
  - On Mac and Linux, the backup folder is created with `chmod 700` and each file with
    `umask 077`.
  - On Windows, the folders sit under `%LOCALAPPDATA%\Monthwise`, which Appendix 1 already limits
    to the user's account, and they inherit that access.
- How the commands reach the database:
  - Docker runs `pg_dump`, `pg_restore`, `dropdb` and `createdb` inside the `db` container
    (`compose exec -T db … -U monthwise`). The file is streamed over stdin and stdout, so the
    database stays unpublished on the host.
  - Native uses the PostgreSQL tools of the installation (Homebrew `postgresql@17`, or
    `pgsql\bin` on Windows) on `127.0.0.1:5442`, with the password from
    `secrets/postgres_password` in `PGPASSWORD` for that one command only.
- Azure, `deploy.sh`: before deploying, the script notes the UTC time. When Monthwise does not
  become ready, or its health reports `migrations: failed`, it prints the
  `az postgres flexible-server restore … --restore-time <that time>` command and points to the
  manual.
- Docs:
  - The user manual's chapter 10 gets a short "Update backups" part with where they are, that
    three are kept, and what happens when an update fails. It also gets "Restoring an update backup
    by hand", with the commands for Docker and native on Mac and Windows. The Azure part says how
    to restore to the time before a deploy.
  - ADR 0006 gets a dated note that a failed migration is no longer reverted, and that the update
    backup replaces the revert.

## Steps
1. Back-end: remove the automatic revert from `MigrateDatabaseAsync`, and update its comment and
   the migration test.
2. Docker `monthwise.sh`: backup, cleanup, restore, the previous image on failure, and
   `remove --data`.
3. Docker `monthwise.ps1`: the same.
4. Native `monthwise.sh` and `monthwise.ps1`: backup, cleanup and restore around the existing
   switch back to `app.previous`.
5. Azure `deploy.sh`: the restore hint.
6. User manual and ADR 0006.
7. Run shellcheck, PSScriptAnalyzer, the back-end tests and the manual checks below; leave
   everything staged for review.

## Tests
- Unit tests: none (no domain change).
- API / integration tests:
  - `MigrationTests`: the test that checked the revert becomes "a failing migration keeps the
    migrations applied before it and reports the failure". It asserts that health answers `503`,
    that the tables of the earlier migrations still exist, and that the history ends at the
    migration before the failing one.
  - The case-collation test is unchanged. Its explicit `MigrateAsync(beforeNameCollation)` checks
    the Down migration itself, not the startup path.
- Manual checks (in a container with Docker, using locally built images):
  - Docker update to a good image: a backup file exists, mode `600`, folder `700`, and the update
    goes through.
  - Four updates in a row: three backups are left.
  - Update to an image whose migration fails (made by adding a table the next migration creates,
    as the test does): the data afterwards matches the backup, the previous image runs, the health
    response is `200`, and the message names the backup.
  - Database not reachable for `pg_dump`: the update stops and the running release is unchanged.
  - shellcheck and PSScriptAnalyzer pass, as in CI.
  - Native scripts on macOS and Windows can't be run here. Their backup and restore commands are
    the same PostgreSQL commands as for Docker, run with the local tools. They are checked by
    review and, if you can, by one update on your Mac.

## Open questions
- none
