# Plan: "Today" is the household's date, not the server's

- **Branch:** `fix/household-time-zone`
- **Status:** Draft
- **Date:** 2026-11-17
- **Requirements:** [production-packaging.md, Appendix 6](../feature-requirements/production-packaging.md#appendix-6-today-is-the-households-date-not-the-servers)

## Goal

Monthwise uses the household's time zone (default `Europe/Zagreb`) for "today", the current month
and every other local date or time, on every deployment target, even when the computer or Azure
runs on UTC (finding H3 of the code review).

## Requirement changes

Implements [Appendix 6](../feature-requirements/production-packaging.md#appendix-6-today-is-the-households-date-not-the-servers)
of the production packaging requirements (2026-11-17, finding H3):

- **FR-5 Settings outside the code** (extended): one household time zone per installation,
  default `Europe/Zagreb`; everything that depends on the current date or time (current month,
  Dashboard, due and overdue bills, dates filled in for the user, backup reminder, export file
  name) uses it on every target; the date changes at local midnight, also on daylight saving
  days; bookkeeping timestamps keep meaning the same moment and no saved data changes; an unknown
  time zone stops Monthwise from starting with a message that names the setting.
- **FR-2 Same release for every target** (extended): the Settings screen shows the household
  time zone next to the version.
- **New acceptance criteria 19 to 21:** at 00:30 Croatian time on the first of a month, with the
  server on UTC, the screens open on the new month and dates filled in are the new day, in winter
  and summer time; another valid zone is followed, an unknown one stops the start and names the
  setting; Settings shows the zone, and the example settings and the user manual describe it.

## Scope

- In scope: the time provider registration, `Today()`, the startup check, the health response,
  the Settings screen, `appsettings.json`, `deploy/settings.example.json`, the user manual and
  `docs/INIT.md`, and tests.
- Out of scope: everything in the appendix's "Out of scope" (dates the screens fill in on the
  device, several zones, editing the zone on the Settings screen). No `TZ` variable in Docker or
  Azure: the setting replaces the review's interim suggestion, and the server's own clock and log
  time stamps stay as they are.

## Design

- **Domain / data model changes:** none. Created and updated timestamps keep using
  `GetUtcNow()`.
- **Database migrations:** none.

### One clock in the household time zone (Application)

Every server-side "today" already goes through `TimeProviderExtensions.Today()`, which reads
`GetLocalNow()`, and the export file name is the only other `GetLocalNow()` caller. So the fix
changes which time zone counts as "local", not the call sites:

- New `Application/Common/HouseholdTimeProvider.cs`: a `TimeProvider` that wraps another one
  (`TimeProvider.System` in production, a fixed clock in tests) and overrides `LocalTimeZone` with
  the household zone. `GetUtcNow()`, timers and timestamps come from the wrapped provider
  unchanged, and .NET's `GetLocalNow()` then converts with the household zone, including daylight
  saving.
- New `Application/Common/HouseholdOptions.cs`: the `Household` settings section with
  `TimeZone` (default `Europe/Zagreb`), and `HouseholdTimeZone.Resolve(string?)`, which uses
  `TimeZoneInfo.TryFindSystemTimeZoneById` (IANA ids work on Linux, macOS and Windows) and
  otherwise throws an `InvalidOperationException` such as
  "The setting Household:TimeZone has the unknown time zone 'Europe/Zagrb'. Use an IANA time
  zone such as Europe/Zagreb." The message names the setting and the given value only (no
  secret is involved).
- `AddApplication(IConfiguration)` reads `Household:TimeZone` and registers
  `new HouseholdTimeProvider(TimeProvider.System, zone)` as the singleton `TimeProvider`, instead
  of `TimeProvider.System`. The zone is resolved when the provider is first needed.
- `TimeProviderExtensions.Today()` keeps its code; its comment says "the household's time zone"
  and no longer mentions the server.

### Startup and health (Api)

- `Program.cs`: next to `EnsureSensitiveDataKeys()`, resolve the `TimeProvider` once so an
  unknown zone stops the start with the message above (logged as critical, as for missing keys).
  It also runs when the OpenAPI document is generated, since the default needs no secret.
- `appsettings.json`: `"Household": { "TimeZone": "Europe/Zagreb" }`.
- `HealthResponse` gets `TimeZone` (the zone id, for example `Europe/Zagreb`). It is a
  setting, not household data, so it is fine on the health address (FR-11 acceptance 8).

### Front-end

- Regenerate `src/api/schema.d.ts` from the OpenAPI document.
- `SettingsPage.tsx`: the version line becomes
  "Monthwise version 1.2.0+3f2c1ab · time zone Europe/Zagreb".

### Deployment and documentation

- `deploy/settings.example.json`: add `"Household": { "TimeZone": "Europe/Zagreb" }`.
- Docker one-click and native: the setting is read from `config/settings.json` like the others
  (no script change). Azure: no bicep change for the default; the user manual says how to set
  `Household__TimeZone` for another zone.
- User manual: one short paragraph in the Settings chapter (where the zone is shown) and in the
  settings part of each installation chapter.
- `docs/INIT.md`: one line under configuration that dates follow `Household:TimeZone`.
- The `aspnet:10.0` runtime image ships the time zone database (`tzdata`); the release check in
  CI (health on the built image) proves `Europe/Zagreb` resolves there, since the app does not
  start otherwise.

## Steps

1. `HouseholdOptions`, `HouseholdTimeZone.Resolve` and `HouseholdTimeProvider` in Application,
   with `AddApplication(IConfiguration)` and the call in `Program.cs`.
2. Startup check in `Program.cs`; default in `appsettings.json`.
3. `TimeZone` on the health response; regenerate the front-end client; show it on Settings.
4. Example settings, user manual, `docs/INIT.md`.
5. Tests (below); run the back-end and front-end suites, lint and the type check.

## Tests

- **Unit tests** (Api test project, which already references Application; no database):
  - With a fixed UTC clock at 2026-11-30 23:30 UTC (winter, UTC+1), `Today()` is 2026-12-01.
  - At 2026-06-30 22:30 UTC (summer, UTC+2), `Today()` is 2026-07-01.
  - At 2026-11-30 22:59 UTC, `Today()` is still 2026-11-30 (just before local midnight).
  - On the spring and autumn clock change days (2026-03-29, 2026-10-25) the date changes once, at
    local midnight.
  - `GetUtcNow()` of the household provider equals the wrapped clock (timestamps unchanged).
  - `Resolve` accepts `Europe/Zagreb` and `America/New_York`, rejects an unknown id, and the
    message names `Household:TimeZone`.
  - The fixed clock is a small `TimeProvider` subclass in the test project, so no new package.
- **API / integration tests:**
  - With the factory's clock fixed at 2026-11-30 23:30 UTC, marking an input received and a bill
    paid without a date stores 2026-12-01, and the Dashboard opens on December 2026. The factory
    gets an optional clock, replacing the registered `TimeProvider` with the household provider
    around it.
  - With `Household:TimeZone` set to `America/New_York` at the same instant, the date is
    2026-11-30.
  - With `Household:TimeZone` set to `Europe/Zagrb`, the factory fails to start and the error
    names the setting.
  - The health response includes `timeZone` = `Europe/Zagreb` by default.
- **Front-end:** the Settings page test shows the time zone next to the version.
- **Manual checks:** start the release image with Docker (container on UTC) and see the time
  zone on Settings and in `/api/health`; start it with `Household__TimeZone=Nowhere/City` and see
  it stop with the message.

## Open questions

- None.
