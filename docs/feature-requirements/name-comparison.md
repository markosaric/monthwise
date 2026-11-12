# Requirements: Name comparison ignoring upper/lower case

- **Branch:** `fix/name-comparison`
- **Status:** Approved (2026-11-11)
- **Date:** 2026-11-11

> This document describes **what** the fix must do, not **how** it is built.
> The implementation plan (`docs/plans/fix-name_comparison.md`) is written after these
> requirements are approved.

## 1. Goal

Many lists in Monthwise require names to be **unique, ignoring upper/lower case**: cards, card
issuers, banks, payers, input categories, loan categories and subcategories, loans that are not
closed, purchase categories, expense categories, active recurring bills and shared-cost
participants (see the requirements listed in section 5).

Today the app checks this rule in two places that can disagree, especially for letters outside
plain A to Z, such as **č, ć, ž, š, đ**. Depending on the database setup, the app can then:

- accept two names that the user sees as the same (for example *Čevapi* and *čevapi*), or
- refuse a name with a general error instead of the usual *name already in use* message.

This fix makes the rule mean exactly one thing everywhere, so the user always gets the same,
predictable answer.

## 2. Terms

| Term | Meaning |
|---|---|
| Same name | Two names that are equal when upper and lower case are ignored, as defined in FR-1. |
| Name list | One of the lists in section 1 in which names must be unique. |

## 3. Functional requirements

- **FR-1 Same name.** Two names are the same when they differ only in upper/lower case, for
  every letter, including č/Č, ć/Ć, ž/Ž, š/Š, đ/Đ and other non-English letters.
  - *Čevapi*, *čevapi* and *ČEVAPI* are the same name.
  - Accents and other marks still count: *Čaj* and *Caj*, or *Đak* and *Dak*, are different
    names.
  - Spaces count as typed: *Ana* and *Ana M.* are different names (trimming of leading and
    trailing spaces stays as it is today).
- **FR-2 One answer everywhere.** In every name list, adding or renaming an entry to a name that
  is the same (FR-1) as another entry of that list is refused with the list's usual *name already
  in use* message. It never ends in a general error, and it never saves both names.
- **FR-3 Same scope as today.** Where a list limits uniqueness today, the limit stays: loans only
  among loans that are not closed, recurring bills only among active ones, loan subcategories only
  within their category. Only the meaning of "ignoring upper/lower case" changes.
- **FR-4 Names entered on the fly.** When a name is typed into a form that adds it to a list on
  saving (for example a new payer typed into an input), and it is the same name (FR-1) as an
  existing entry, the existing entry is used and no second entry is added.
- **FR-5 Display unchanged.** Names are stored and shown exactly as the user typed them; the
  comparison never changes how a name is written. Sorting of lists stays as it is.
- **FR-6 Existing data.** Updating an existing installation keeps all its data. If, in a list,
  existing entries are already the same name under FR-1, the update does not merge, rename or
  delete them; it stops before changing anything and says which list and which names to rename,
  so the user can rename one and run the update again.
- **FR-7 Restore.** Restoring a backup follows the same rule: a backup whose list holds two
  entries that are the same name (FR-1) is refused with a clear message, and the current data is
  left as it was.

## 4. Non-functional requirements

- **NFR-1** The rule behaves the same on every supported installation (Docker, the native
  Windows/Linux package and Azure), whatever language settings the database was created with.
- **NFR-2** Checking for a duplicate name does not slow down adding or renaming entries in a way
  the user notices.
- **NFR-3** The fix is covered by automated tests that run against the real database, including
  names with č, ć, ž, š and đ.

## 5. Affected requirements

This fix gives one meaning to "unique, ignoring upper/lower case" in these approved requirements,
without changing their wording or scope:

- [credit-card-settings.md](credit-card-settings.md) A1 (card name), card issuers and banks
- [input-cash-flow.md](input-cash-flow.md) payers and input categories
- [loans-and-leases.md](loans-and-leases.md) A1 (loan name), loan categories and subcategories
- [credit-card-installments.md](credit-card-installments.md) purchase categories
- [monthly-bills-and-expenses.md](monthly-bills-and-expenses.md) R1 (recurring bill name) and
  expense categories
- [shared-bills.md](shared-bills.md) P1 (participant name)

## 6. Out of scope

- Treating names with and without accents as the same (*Čaj* and *Caj* stay different).
- Ignoring extra spaces inside names.
- Changing the maximum name lengths, the messages' wording, or how lists are sorted or searched.
- Login, users and households (names stay unique per installation, as today).

## 7. Acceptance criteria

1. In each name list, with *Čevapi* saved, adding *čevapi* or *ČEVAPI* is refused with the
   list's *name already in use* message; renaming another entry to *ČEVAPI* is refused the same
   way.
2. With *Čaj* saved, *Caj* can be added to the same list.
3. Typing *ana* as a new payer while *Ana* exists uses *Ana*; no second payer is added.
4. A closed loan named *Đak* does not stop a new loan named *đak*; an open one does.
5. The same results hold on a database created with any supported language setting.
6. Updating an installation whose data has no same-name entries keeps all data unchanged.
7. Updating an installation (or restoring a backup) with two same-name entries in one list stops
   with a message naming the list and the names, and changes nothing.
8. The database tests that cover these cases run in CI and fail the build if they fail.
