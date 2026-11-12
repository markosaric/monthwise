# Plan: Database error answers and trusted proxy

- **Branch:** `fix/error-mapping-proxy-trust`
- **Status:** In progress
- **Date:** 2026-11-12
- **Requirements:** [error-mapping-and-proxy-trust.md](../feature-requirements/error-mapping-and-proxy-trust.md)

## Goal

A save or delete that the database itself rejects answers 409 or 400 with a plain message instead
of 500 (FR-1 to FR-5), and in proxy mode Monthwise accepts forwarded headers only from trusted
proxy networks (FR-6).

## Scope

- In scope: translating PostgreSQL constraint errors in the unit of work, one exception handler
  for all of them, the trusted proxy networks setting with its default, tests, and the settings
  documentation.
- Out of scope: concurrency conflicts (M8 adds them to the same exception and handler), new
  database rules, changes to handler checks, front-end changes.

## Design

- **Domain / data model changes:** none.
- **Application (`Monthwise.Application/Common/Persistence.cs`):**
  - Replace `UniqueConstraintException` with one `DatabaseRuleException(DatabaseRule rule, string message, Exception? inner)`.
  - `enum DatabaseRule { Unique, MissingReference, StillReferenced, ValueNotAllowed }` (NFR-3: M8
    adds `ConcurrentChange` here).
  - The four messages live next to the enum as constants, so the infrastructure and the tests
    use the same text (FR-1 to FR-4).
  - `IUnitOfWork.SaveChangesAsync` documents the new exception.
- **Infrastructure (`MonthwiseDbContext`, `IUnitOfWork.SaveChangesAsync`):** catch
  `DbUpdateException` whose inner exception is a `PostgresException` and map its `SqlState`:
  - `23505` unique violation → `Unique` (FR-1, unchanged message).
  - `23503` foreign key violation → `MissingReference` (FR-2) or `StillReferenced` (FR-3).
    PostgreSQL reports the **referencing** table (`PostgresException.TableName`) in both cases,
    and its message text depends on the server language, so the text is not parsed. Instead: if
    the change tracker holds an added or modified entry whose table is that referencing table,
    the change pointed to a missing record (`MissingReference`); otherwise a delete of a
    referenced record was refused (`StillReferenced`). When a request does both at once, the
    answer is `MissingReference`; both are 409, only the message differs.
  - `23502` not null, `23514` check, `23P01` exclusion → `ValueNotAllowed` (FR-4).
  - Anything else is not caught and stays a 500 (FR-5).
  - The new exception never carries `PostgresException.Detail`, the row values or the constraint
    name in its message (FR-5). The inner exception is kept for the log.
  - The transaction is not committed when `SaveChanges` throws, so nothing is saved (FR-5).
- **API (`Endpoints/DatabaseRuleExceptionHandler.cs`, replaces `UniqueConstraintExceptionHandler`):**
  maps `Unique`, `MissingReference`, `StillReferenced` → 409 and `ValueNotAllowed` → 400 and writes
  problem details with `status` and `title` (the message) only, like today's unique handler. The
  front-end already shows `title` as the save error (`ApiError`), so it needs no change. The
  400 is a plain problem without field errors, since the database does not say which field.
  Registered in `Program.cs` in place of the old handler.
- **Proxy (`Hosting/ProxyHosting.cs`):**
  - New setting `Hosting:TrustedProxyNetworks`: a list of CIDR ranges, read with
    `System.Net.IPNetwork.Parse`. An invalid entry stops startup with a clear error naming the
    setting, not the value list.
  - Default when the setting is missing or empty: `10.0.0.0/8`, `172.16.0.0/12`,
    `192.168.0.0/16`, `100.64.0.0/10` (shared address space some platforms use for their internal
    proxy), `127.0.0.0/8`, `::1/128` and `fc00::/7`. These are general private ranges (NFR-4).
  - `KnownIPNetworks` gets these ranges, `KnownProxies` is cleared. IPv4 senders that reach
    Kestrel as IPv4-mapped IPv6 addresses also match (the middleware compares the mapped address;
    a test covers it).
  - At startup in proxy mode, one information log line names how many trusted networks are in use
    and whether they come from the setting or the default.
  - Proxy mode off: unchanged, the middleware is not added.
- **Configuration and docs:** `Hosting:BehindProxy` is today only set in `deploy/azure/main.bicep`
  and described in the `ProxyHosting` code comment, so `Hosting:TrustedProxyNetworks` is described
  there too: in the code comment, and in a one-line comment next to `Hosting__BehindProxy` in
  `main.bicep` saying the default ranges apply and how to override them. The bicep settings
  themselves stay unchanged (A9). `deploy/settings.example.json` is for Docker and native
  installs, where proxy mode is off, so it is not changed.
- **API changes (endpoints, DTOs):** none; only status codes and messages of the error cases (NFR-2).
- **Front-end changes:** none.
- **Database migrations:** none (NFR-1).

## Steps

1. Add `DatabaseRule`, `DatabaseRuleException` and the messages; remove `UniqueConstraintException`.
2. Map the PostgreSQL error codes in `MonthwiseDbContext` as above.
3. Replace the exception handler and its registration.
4. Add the trusted proxy networks setting, its default and the startup log line.
5. Document the setting in the code comment and `main.bicep`.
6. Tests (below), then `dotnet build`, `dotnet test` with a test database, and the front-end
   lint and tests.

## Tests

- **Unit tests (no database):**
  - `DatabaseRuleExceptionHandler`: each rule gives the right status and title, the body has no
    other text than status and title, and other exceptions are not handled.
  - Proxy hosting: the default networks are used without the setting; a configured list replaces
    them (A8); an invalid entry fails startup.
  - Proxy behaviour through the in-memory server with the sender address set per request
    (`TestServer.SendAsync`): with `BehindProxy=true` a request from `10.1.2.3` (and from
    `::ffff:10.1.2.3`) with `X-Forwarded-Proto: https` and `X-Forwarded-For` is seen as HTTPS
    with the forwarded client address (A6); the same request from `203.0.113.10` stays HTTP with
    its real sender (A7); with `BehindProxy=false` the headers are ignored.
- **Database tests (`TestDatabase.ConnectionString`):** through `IUnitOfWork` in a service scope
  of the API factory, with made-up data:
  - a duplicate bank name → `Unique` and the API answers 409 (A1);
  - a cash flow input for an account id that does not exist → `MissingReference` (A2), nothing
    saved;
  - deleting a bank that a card still uses, saved without the handler's check → `StillReferenced`
    (A3), nothing deleted;
  - a row that breaks a check constraint (for example a non-debit primary card) →
    `ValueNotAllowed` (A4);
  - for each, the exception message and the API body contain none of the row's values (A5).
  - One end-to-end API test per status (409 and 400) through an endpoint where the database rule
    can be reached, if one exists without changing handler checks; otherwise the handler unit test
    and the unit of work tests together cover the path.
- **Manual checks:** after the next Azure release, open Monthwise over HTTPS and check the startup
  log line and that the app still answers over HTTPS (A9). If the Azure ingress connects from an
  address outside the default ranges, the fix is to set `Hosting:TrustedProxyNetworks` in
  `main.bicep`, which is a follow-up that needs approval.

## Open questions

- None.
