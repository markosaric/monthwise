# Requirements: Database error answers and trusted proxy

- **Branch:** `fix/error-mapping-proxy-trust`
- **Status:** Approved (2026-11-12)
- **Date:** 2026-11-11
- **Source:** code review of `main`, finding L12 (low priority)

> This document describes **what** the fix must do, not **how** it is built.
> The implementation plan (`docs/plans/fix-error-mapping-proxy-trust.md`) is written after these
> requirements are approved.

## 1. Goal

Two small robustness gaps found in the code review are closed:

1. When the database itself rejects a save, the user gets a clear answer instead of a general
   server error. Today only a duplicate value (a broken "must be unique" rule) is answered as a
   conflict; a save that points to something that no longer exists, a delete of something that is
   still used, or a value the database does not allow ends as **500 Internal Server Error** with
   "Something went wrong".
2. When Monthwise runs behind a proxy (the Azure release), it accepts the forwarded client address
   and protocol only from the proxy's own networks, not from any sender.

The screens and the data model do not change.

## 2. Terms

| Term | Meaning |
|---|---|
| Database rule | A rule the database enforces on every save, as a last line of defence behind the checks Monthwise makes first: a value must be unique, a reference must point to an existing record, a record that is still referenced cannot be deleted, a value must be filled in or lie in its allowed range. |
| Race | Two requests that change related data at the same time, so the checks of one pass before the other is saved. This is the usual way a database rule is reached. |
| Forwarded headers | The information a proxy adds to a request about the original client: its address and whether it used HTTPS. |
| Trusted proxy network | An address range from which Monthwise accepts forwarded headers. |

## 3. Functional requirements

**FR-1 Duplicate value (unchanged)**
- A save that breaks a "must be unique" rule keeps answering **409 Conflict** with the message
  "A value that must be unique is already in use."

**FR-2 Reference to a missing record**
- A save that refers to a record that does not exist (any more), for example an input saved for an
  account that was deleted a moment earlier, answers **409 Conflict**.
- Message: "A record this change refers to no longer exists. Reload and try again."

**FR-3 Delete of a record that is still used**
- A delete that the database refuses because other records still refer to the record answers
  **409 Conflict**.
- Message: "This record is still used by other data and cannot be deleted."

**FR-4 Value the database does not allow**
- A save that breaks a database rule on a single value or row (a required value is missing, an
  amount or number lies outside its allowed range, a combination of values is not allowed) answers
  **400 Bad Request**.
- Message: "Some of the values are not allowed. Check the entries and try again."

**FR-5 What the answers contain**
- The answers of FR-1 to FR-4 use the same error format as the other error answers of the API,
  so the screens show the message like any other save error.
- They never contain the values of the rejected record, card numbers, IBANs or the database's own
  error text. They contain only the status and the message.
- Nothing is saved when one of these answers is given: the whole change of the request is
  rejected, as today.
- Any other database error keeps answering **500 Internal Server Error** and is logged as today.

**FR-6 Forwarded headers only from trusted proxy networks**
- When Monthwise runs behind a proxy (the setting that turns proxy mode on, used by the Azure
  release), it accepts forwarded headers only from requests that come from a trusted proxy network.
- A request from any other address is treated as a direct request: its forwarded headers are
  ignored, so it cannot make Monthwise believe it came over HTTPS or from another client address.
- The trusted proxy networks can be set in the configuration as a list of address ranges.
- Without such a setting, the default covers the private address ranges the hosting platform uses
  for its internal proxy, so the Azure release keeps working without any change to its settings.
- When proxy mode is off (Docker, native install, development), forwarded headers stay ignored, as
  today.

## 4. Non-functional requirements

- **NFR-1** No database migration and no change to stored data.
- **NFR-2** No change to the screens, to the API's endpoints or to the shape of their requests and
  answers, apart from the status codes and messages of FR-2 to FR-4.
- **NFR-3** The error answers of FR-2 to FR-4 are built so that later fixes can add further
  database conflicts (for example two people editing the same record at the same time) in the same
  place.
- **NFR-4** No secrets or real addresses of the user's installation are written into the code or
  the documentation; the default trusted networks are general private address ranges.

## 5. Out of scope

- Detecting two people editing the same record at the same time (a separate review finding, M8,
  that builds on this fix).
- Changing the checks Monthwise makes before saving, or adding new database rules.
- Naming the exact field or rule in the message.
- Authentication, rate limiting, or any other hardening of the Azure release.

## 6. Acceptance criteria

- **A1** A save that breaks a unique rule still answers 409 with the message of FR-1.
- **A2** A save whose referenced record is missing at the moment of saving answers 409 with the
  message of FR-2, and nothing is saved.
- **A3** A delete that the database refuses because the record is still referenced answers 409
  with the message of FR-3, and nothing is deleted.
- **A4** A save that breaks a single-value or row rule of the database answers 400 with the message
  of FR-4, and nothing is saved.
- **A5** None of the answers of A1 to A4 contains record values or database error text.
- **A6** In proxy mode, a request from a trusted proxy network with `X-Forwarded-Proto: https` is
  seen as HTTPS, and its forwarded client address is used.
- **A7** In proxy mode, the same request from an address outside the trusted proxy networks is
  seen as plain HTTP with its real sender address.
- **A8** A configured list of trusted proxy networks replaces the default.
- **A9** The Azure release keeps answering over HTTPS after the change, with no change to its
  settings.
- **A10** All existing back-end and front-end tests pass.
