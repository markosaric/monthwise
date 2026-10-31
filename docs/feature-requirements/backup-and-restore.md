# Requirements: Backup and restore

- **Branch:** `feat/backup_and_restore`
- **Status:** Approved (2026-10-31)
- **Date:** 2026-10-31

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-backup_and_restore.md`) is written after these
> requirements are approved.

## 1. Goal

Monthwise now runs as an installed application (see
[production-packaging.md](production-packaging.md)), and the household's financial history lives
only in its database. If the computer breaks, the database is damaged, a wrong change is made or
the user moves Monthwise to another target (for example from the Docker one-click on a laptop to
Azure), that history must not be lost.

This feature lets the user, **from the Settings screen**:

1. **export** all Monthwise data to a password-protected file on their own device,
2. **import** that data back into Monthwise from such a file,
3. see **when** the last export was made and **when** data was last imported, so they always know
   how old their newest copy is.

Login is its own feature and is assumed to be in place (as in production packaging FR-11).

## 2. Terms

| Term | Meaning |
|---|---|
| Household data | Everything the user has entered in Monthwise: cards and accounts, banks and issuers, payers, participants, categories, cash flow inputs and recurring inputs, balances, bills and recurring bills, expenses, card purchases and card payments, loans and loan payments, months. |
| Export | Writing all household data to one Monthwise data file and downloading it to the user's device. |
| Export file | The file made by an export, protected with a password the user chooses. |
| Import | Replacing all household data in Monthwise with the data from an export file. |
| Safety copy | An export that Monthwise makes by itself right before an import, kept on the installation (FR-7). |
| Data and backups history | The list of every export, import and safety copy done on this installation (FR-9). |
| Household admin | A user of the household who may export and import data (FR-1). Which users this is comes from the login feature. |
| Installation | As in production packaging: Monthwise on one deployment target, with its own database, settings and secrets. |

## 3. Options considered

Two ways of saving and bringing back data were compared before these requirements were written.

**Option A: database backup and restore.** A complete copy of the database, downloaded as one
file; a restore replaces the whole database.
- Always complete, with no extra work in later features.
- The file cannot be checked by the user and cannot go into an older release.
- Card numbers and IBANs stay encrypted with the installation's keys
  ([ADR 0005](../decisions/0005-encrypted-sensitive-data.md)), so the file only works together
  with the same keys.
- It includes user accounts and logins.
- On Azure it mostly repeats the managed database's own automatic backups.

**Option B: data export and import.** All household data written to a Monthwise data file and
read back from it.
- Independent of the database: the file moves between all three targets and between releases.
- Every new feature that stores data must add it to the export and import, which is a lasting
  cost and needs a check that nothing is missed (NFR-7).
- Card numbers and IBANs are handled on purpose.
- User accounts are not part of the file.
- It leaves room for partial exports or imports later.

| | A: Database backup | B: Data export |
|---|---|---|
| Completeness | Always complete | Complete while every feature keeps it up to date |
| Move between Docker, native and Azure | Only with the same encryption keys | Yes |
| Bring back into a newer release | Yes | Yes |
| Bring back into an older release | No | No |
| Card numbers and IBANs | Need the installation's keys | Protected by the file password |
| User accounts and logins | Included | Not included |
| Work for every later feature | None | Add new data to export and import |
| On Azure | Overlaps with Azure's own backups | Adds a portable copy the user holds |

**Decision (2026-10-31):** Option B only. The whole export file is protected by a password the
user chooses. On Azure, the managed database's own automatic backups are documented as a second
safety net (FR-12).

## 4. Functional requirements

### 4.1 Settings section

**FR-1 Data and backups section**
- The Settings screen gets a **Data and backups** tile in its own group **Data**, next to the
  existing groups (see [design-refresh.md](design-refresh.md) FR-20). Like the other tiles it has
  a coloured icon, a title and a one-line description; instead of a count it shows the age of the
  last export (for example "Last export 3 days ago" or "No export yet").
- The section shows the last export and the last import (FR-9) at the top, the **Export data** and
  **Import data** actions, the reminder setting (FR-11), and the history below them.
- Only a household admin can export and import. Other users of the household see the section and
  its history, without the actions.

### 4.2 Export

**FR-2 Export all data**
- **Export data** exports all household data at that moment and downloads it to the user's device
  as one file, through the browser, on every deployment target.
- The file name says it is Monthwise data and when it was made, for example
  `monthwise-export-2026-10-31-0815`, so several files can be told apart in the Downloads folder.
- The file also carries, inside, the date and time it was made, the Monthwise release that made it
  and a short summary of what it holds (for example the number of cards, loans, inputs and months).
- Exporting does not change any data and can be done while Monthwise is in normal use; the file
  holds the data as it was at one single moment, never half of a change.
- If the export fails, the user is told so in plain words and nothing is downloaded that could
  later be mistaken for a good file.

**FR-3 File password**
- Before every export the user chooses a password for the file and enters it twice. The password
  must be at least 6 characters long.
- The whole file is protected with that password: without it nothing in the file can be read,
  including full card numbers and IBANs, which are included so that an import brings them back.
- Monthwise never stores, logs or shows the file password. The section tells the user that a
  file whose password is lost cannot be imported by anyone, and that the password belongs in
  their password manager.
- Secrets (database password, encryption keys) are never written to the file.
- The section reminds the user that the file holds their personal financial data and should be
  kept somewhere safe.

### 4.3 Import

**FR-4 Choose a file and check it first**
- The household admin picks an export file from their device and enters its password. Before
  anything changes, Monthwise checks the file and shows what it is: when it was made, by which
  release, and its summary (FR-2).
- A wrong password, a file that is not a Monthwise export file, or a damaged or incomplete file is
  refused with a plain message and no data changes.

**FR-5 Confirm before replacing**
- An import **replaces all household data** in Monthwise with the data in the file; it never
  merges the file's data with what is already there.
- The confirmation says this clearly, shows what is in Monthwise now next to what is in the file,
  and asks the user to confirm by a deliberate step (for example typing a word), not by a single
  click.

**FR-6 Releases**
- A file exported by the same or an older release can always be imported; its data is brought up
  to the current release by itself.
- A file exported by a newer release is refused, with a message that says which release made it
  and that Monthwise must be updated first.

**FR-7 All or nothing, with a safety copy**
- An import either completes fully or changes nothing: if anything fails halfway, Monthwise keeps
  exactly the data it had before and says what went wrong.
- Right before replacing the data, Monthwise makes a safety copy of its current data, so a wrong
  import can be undone. The safety copy is kept on the installation (not downloaded, no file
  password needed), is protected like the database itself, and is listed in the history (FR-9)
  with an action for the household admin to bring it back. Only the newest safety copy is kept.
- While an import runs, no user of the household can change data; after it is done, every open
  screen shows the new data.

**FR-8 After importing**
- The user stays signed in, and the users who can sign in are the same as before: user accounts
  and logins are never part of the export file.
- The user sees a confirmation with the summary of what was imported.

### 4.4 History

**FR-9 Data and backups history**
- Every export, import and safety copy (made or brought back) is recorded with: date and time,
  what was done, who did it, whether it succeeded or failed, the file name, and the release.
- For an import the record also shows when the file itself was made, so the user can see both
  "imported on 31 Oct" and "data from 12 Oct".
- The top of the section shows the **last successful export** and the **last successful import**
  with their date and how long ago that was. Failed attempts are listed in the history but never
  count as the last export or import.
- The history belongs to the installation, not to the household data: it is **not** in the export
  file and it is **not** replaced by an import, so it keeps showing everything done on this
  installation.
- The history can be read but not edited or deleted.

**FR-10 Export reminder**
- When the reminder is on and no export has been made yet, or the last successful export is older
  than the reminder interval, the Dashboard shows a short reminder with a link to the Data and
  backups section.
- The reminder can be dismissed; it comes back after the next interval without an export.

**FR-11 Reminder setting**
- The Data and backups section has a reminder setting: whether the reminder is shown, and its
  interval, chosen from 7, 14, 30, 60 or 90 days.
- By default the reminder is on with an interval of 30 days.
- The setting applies to the whole household; only a household admin can change it, other users
  see it.
- A change applies at once: turning the reminder off removes it from the Dashboard, and a new
  interval is counted from the last successful export.

### 4.5 Documentation

**FR-12 User manual**
- The user manual gets a Data and backups chapter: how to export and import data, where the file
  goes, how to keep the file and its password safe, and how to move Monthwise to another target
  with an export and an import.
- For Azure, the chapter explains the managed database's own automatic backups and how to return
  to an earlier point in time with them.
- The installation chapters (production packaging FR-12) point to this chapter.

## 5. Non-functional requirements

- **NFR-1 Data protection:** the export file holds personal financial data and nothing in it is
  readable without its password (FR-3); the file contents and its password never appear in the
  logs, error messages or the repository. Test files use made-up data only, as required by
  `CONTRIBUTING.md`.
- **NFR-2 No data loss:** a failed or cancelled export or import never changes or loses data
  (FR-7).
- **NFR-3 Exact amounts:** amounts come back exactly as they were exported, to the cent.
- **NFR-4 Round trip:** exporting and then importing the file into an empty installation gives
  exactly the same screens, totals and calculations as before, on every deployment target and
  with other encryption keys than the installation that made the file.
- **NFR-5 Speed:** with ten years of household data, export and import each take less than one
  minute on the user's own computer.
- **NFR-6 Works on phone and desktop:** the section and the history are usable on both; an export
  downloads the file on a phone as on a desktop.
- **NFR-7 Nothing left out:** a check fails when household data stored by Monthwise is not in the
  export file, so later features cannot forget to add their data.

## 6. Out of scope

- A database backup and restore from Monthwise (Option A, section 3).
- Automatic or scheduled exports to a folder, a cloud drive or Azure storage (a possible later
  feature).
- Exporting or importing only part of the data (one year, one card), or merging a file into the
  existing data.
- Importing from the Numbers workbooks or from bank statements.
- Spreadsheet (CSV, Excel) exports for reporting.
- Exporting user accounts and logins.
- Backing up the secrets; the user keeps them in their password manager (production packaging
  FR-6).
- Changing Azure's own database backup settings from Monthwise.

## 7. Acceptance criteria

1. Settings shows a Data and backups tile in a Data group, with the age of the last export or "No
   export yet".
2. Only a household admin sees Export data and Import data; other users see the history only.
3. Export asks for a password (twice, at least 6 characters) and downloads one file with all
   household data, named with its date and time and carrying the date, release and a summary
   inside; no data changes while exporting.
4. Nothing in the file, including card numbers and IBANs, can be read without its password; the
   file holds no secrets; the password is never stored or logged.
5. A wrong password, a file that is not a Monthwise export file, a damaged file, or a file from a
   newer release is refused before anything changes, with a plain message.
6. Before importing, the user sees what is in the file next to what is in Monthwise now and
   confirms by a deliberate step.
7. An import replaces all household data or, on any failure, changes nothing; a safety copy is made
   first and can be brought back from the history.
8. After an import, the user stays signed in and the same users can sign in.
9. The history lists every export, import and safety copy with date and time, kind, user, result,
   file name and release; the top of the section shows the last successful export and import; an
   import does not erase the history.
10. When the reminder is on, the Dashboard reminds the user when no export was made within the
    chosen interval, and the reminder can be dismissed; a household admin can turn the reminder off
    or choose 7, 14, 30, 60 or 90 days in the Data and backups section (default on, 30 days).
11. Exporting and importing into an empty installation with other encryption keys gives the same
    screens and totals, to the cent, on Docker one-click, the native background service and
    Azure.
12. The user manual has a Data and backups chapter, including Azure's own backups.
