# Plan: Login

- **Branch:** `feat/login`
- **Status:** In progress
- **Date:** 2026-11-27
- **Requirements:** [login.md](../feature-requirements/login.md)

## Goal
Every screen and API call of Monthwise needs a signed-in user of the household, with built-in
accounts (username and password) that work the same on local development, Docker one-click, the
native background service and Azure. Household admins manage users and are the only ones who
export and import.

## Scope
- In scope: FR-1 to FR-11 and NFR-1 to NFR-8 of [login.md](../feature-requirements/login.md): the
  setup code and first admin, sign-in, lockout and rate limiting, sessions that survive restarts,
  sign-out, the Users screens, temporary and own passwords, the forgotten-password command on
  every target, the household admin rule for export and import, the history's "who did it", and
  the user manual chapter in English and Croatian.
- Out of scope: everything in the requirements' Out of scope (passkeys, outside sign-in services,
  two-step codes, email, self sign-up, several households, a list of open sessions, per-user
  settings).

## Design

The decision for built-in accounts, and against outside sign-in services and Azure's own sign-in,
is recorded as **ADR 0008: built-in accounts with ASP.NET Core Identity** in `docs/decisions/`,
linked from `docs/INIT.md` (Tech stack: "Authentication" is no longer "planned").

### Domain / data model changes
- **Domain:** `UserRole` (`HouseholdAdmin`, `Member`) and the user rules that are not Identity's:
  display name length (100), username pattern (3 to 50 characters, letters, digits, `.`, `-`,
  `_`), and "at least one active household admin" as a pure check over the users
  (`HouseholdAdmins.EnsureOneRemains(...)`), so it is unit tested without a database.
- **Infrastructure:** `AppUser : IdentityUser<Guid>` with `DisplayName`, `Role`, `IsActive` and
  `MustChangePassword`. `MonthwiseDbContext` becomes an `IdentityUserContext<AppUser, Guid>` (users,
  claims, logins and tokens only; no Identity role tables, the role is a column), with the tables
  renamed to the snake_case style: `app_user`, `app_user_claim`, `app_user_login`, `app_user_token`.
- **`data_protection_key`** (ASP.NET Core Data Protection keys, via
  `PersistKeysToDbContext`): the keys that protect the session cookie live in the database, so a
  session survives a restart, an update and an Azure start after scaling to zero (FR-4), and the
  same keys are used by every replica.
- **`setup_code_use`**: the keyed hash (with the installation's `SensitiveData:HashKey`, as for card
  numbers) of each setup code that has been used, and when. A setup code works only while it is
  configured and its hash is not in this table (FR-1, FR-9). The code itself is never stored in
  the database.
- All six new tables are **installation tables**: added to
  `HouseholdDataStore.InstallationTables`, so they are never exported and an import never replaces
  them (FR-10, [backup-and-restore.md](../feature-requirements/backup-and-restore.md) FR-8).

### Setup code (FR-1, FR-9)
- A new secret `Setup:Code` (file `Setup__Code` in the configuration folder's `secrets/`, Key
  Vault secret `setup-code` on Azure, .NET user secrets in development), like the other secrets
  ([ADR 0005](../decisions/0005-encrypted-sensitive-data.md), production packaging FR-6). It is
  24 random characters from an unambiguous alphabet, shown in groups of four
  (`ABCD-EFGH-...`); the comparison ignores dashes, spaces and case and takes constant time.
- Monthwise starts without it (the CI release check and older installations keep working); then
  the setup form refuses every code with "Setup code is not configured, see the user manual".
- **Docker one-click and native background service** (`deploy/docker/monthwise.sh` and `.ps1`,
  `deploy/native/monthwise.sh` and `.ps1`): `ensure_secrets` also creates `Setup__Code` when it is
  missing, which covers new installations and updates of existing ones, and prints it with "Open
  Monthwise and enter this setup code to create the first user". A new command
  `new-setup-code` writes a new code, restarts Monthwise and prints it (FR-9).
- **Azure** (`deploy/azure/deploy.sh`, `main.bicep`): the deployment creates `setup-code` in Key
  Vault when it is missing and passes it to the container as `Setup__Code` (versionless secret
  reference, like the other secrets). `deploy.sh --new-setup-code` writes a new version and
  restarts the active revision so it is read at once; the script prints the `az keyvault secret
  show` command to read it, as it already does for the other secrets.
- `deploy/secrets.example/` gets a `Setup__Code` example file with a made-up value.

### Authentication and authorization (FR-2 to FR-5, NFR-2, NFR-4, NFR-5)
- `AddIdentityCore<AppUser>()` with the EF stores and `SignInManager`, cookie authentication with
  the Identity application scheme. Password options: length 12, no other character rules (the
  maximum of 128 is a validator); usernames are unique and compared through Identity's normalised
  name, which makes them case-insensitive. Password hashes use Identity's default hasher (PBKDF2,
  salted, many iterations).
- **Lockout:** Identity lockout for every user, 5 failed attempts, 15 minutes (FR-3).
- **Rate limiting:** the built-in rate limiter, a fixed window per client address (10 attempts per
  minute) on sign-in and setup. The client address is the real one behind Azure's ingress through
  the existing `ProxyHosting` forwarded headers.
- **Cookie:** `HttpOnly`, `SameSite=Strict`, `Secure` when the request is HTTPS (so it also works
  over plain HTTP on the home network, NFR-5, and is HTTPS-only on Azure, where every request is
  HTTPS). Sign-in sets `IsPersistent` from **Stay signed in**: persistent sessions last 30 days,
  renewed on use; others are browser-session cookies whose ticket ends 12 hours after sign-in (FR-4).
- **Changes only from Monthwise's own screens (NFR-4):** besides `SameSite=Strict`, every
  `POST`/`PUT`/`PATCH`/`DELETE` under `/api` must carry the header `X-Monthwise-Request: 1`,
  which other web sites cannot send without a CORS permission Monthwise never gives. The front-end
  client adds it to every request, including the two direct `fetch` calls in
  `frontend/src/api/dataTransfer.ts`.
- **Immediate effect (FR-6, FR-7, FR-8):** the security stamp is checked on every request
  (`ValidationInterval = 0`; one small query, fine for a household), and the check also refuses an
  inactive user. Role changes, deactivation, deletion and password resets change the stamp, so
  open sessions end or pick up the new role on their next request. Changing one's own password
  refreshes the current session after changing the stamp.
- **Policies:** a fallback policy "signed-in user" for every endpoint, plus a requirement that the
  user has no temporary password. Endpoints that are anonymous: `GET /api/health`,
  `GET /api/auth/state`, `POST /api/auth/sign-in`, `POST /api/auth/setup`. Endpoints that allow a
  temporary password: `POST /api/auth/password`, `POST /api/auth/sign-out`. The existing
  `HouseholdAdmin` policy (`DataEndpoints.AddDataPolicies`) becomes "role is household admin" and
  also protects the user management endpoints. An unknown `/api/...` path still answers 404
  problem details (no endpoint, so no authorization), as the smoke test expects.
- Not signed in answers **401**, a member on an admin endpoint **403**, both as problem details,
  never with household data.
- **Logging (NFR-3):** sign-in succeeded, failed and locked out, setup used, user added, changed,
  deleted and password reset are logged with username and time only. No request body of the auth
  and user endpoints is ever logged.

### Application layer
- `ICurrentUser` (`Application/Common`): the signed-in user's id, display name and role, from the
  request. The data transfer handlers use it for the history's `UserName` (FR-10, currently always
  empty) and the overview's "may export and import".
- `IUserAccounts` (Application interface, implemented in Infrastructure over `UserManager`) and
  handlers in `Application/Users`: list, get, add, edit, delete, reset password, change own
  password, setup. Validation messages in English and Croatian in `ServerTexts`.

### API changes (endpoints, DTOs)
- `GET /api/auth/state` → `{ setupNeeded, user?: { id, displayName, username, role, mustChangePassword } }`
  (`setupNeeded` is true while there are no users).
- `POST /api/auth/setup` `{ setupCode, displayName, username, password }` → 204 and signed in.
  With no users it creates the first household admin. With users (FR-9): when the username is an
  existing household admin, it sets that password and reactivates the user; otherwise it creates
  a new household admin. Either way the code's hash goes into `setup_code_use`. No household data
  is touched.
- `POST /api/auth/sign-in` `{ username, password, staySignedIn }` → 204; 400 with one message for
  wrong username, wrong password and inactive user; 400 with "locked for a while" for a locked
  username; 429 when the rate limit is reached.
- `POST /api/auth/sign-out` → 204.
- `POST /api/auth/password` `{ currentPassword, newPassword }` → 204 (also clears a temporary
  password).
- `GET /api/users` → all users for a household admin, only oneself for a member.
- `POST /api/users`, `PUT /api/users/{id}`, `DELETE /api/users/{id}`,
  `POST /api/users/{id}/password` `{ temporaryPassword }`: household admin only; the last-admin
  rule answers 400 with a clear message.
- `GET /api/data` keeps its "may export and import" field, now from the role.
- The OpenAPI document and `frontend/src/api/schema.d.ts` are regenerated.

### Front-end changes (pages, components)
- `frontend/src/api/auth.ts` and `users.ts` with TanStack Query hooks.
- **`AuthGate`** around the routes in `main.tsx`: it loads `/api/auth/state` and shows
  `SetupPage` (no users), `SignInPage` (not signed in), `ChangePasswordPage` (temporary password),
  or the app. After sign-in it returns to the screen that was asked for. A 401 from any call
  (session ended, user deactivated) clears the query cache and shows the sign-in screen.
- `/setup` route reachable from a "Have a setup code?" link on the sign-in screen, for FR-9.
- The menu (`components/layout`) shows the display name and **Sign out**; Settings gets a
  **Users** tile in the Accounts group with the count; `pages/settings/UsersPage` and user form
  pages for add and edit, reset password, and **Change password** for one's own user.
- The Data and backups screen already hides the actions when the overview says the user may not
  export and import; it is checked, not rebuilt.
- All texts in `texts.ts` and `texts.hr.ts` of the area, with the Croatian terms of NFR-7.

### Database migrations
- One migration `AddLogin`: `app_user`, `app_user_claim`, `app_user_login`, `app_user_token`,
  `data_protection_key`, `setup_code_use`. No change to existing tables; existing installations
  start with no users and show the setup form (FR-1).

### Documentation
- README: development setup gets the `Setup:Code` user secret; Azure and packaging sections the
  new secret and the `new-setup-code` command; the database tests note signed-in test clients.
- User manual (English and Croatian): a **Sign in and users** chapter (first start with the setup
  code on each target, signing in, users and roles, passwords, forgotten password), screenshots
  with made-up names.
- `docs/INIT.md`: the `user` table is no longer planned (it is `app_user`), Tech stack
  Authentication links ADR 0008.

## Steps
1. ADR 0008 and the `docs/INIT.md` update.
2. Domain: `UserRole`, user rules and the last-admin check, with unit tests.
3. Infrastructure: `AppUser`, `IdentityUserContext`, Data Protection keys in the database,
   `setup_code_use`, the `AddLogin` migration, installation tables, `IUserAccounts`.
4. API: Identity and cookie setup, fallback and admin policies, the request header check, rate
   limiting, `ICurrentUser`, auth and user endpoints, data transfer history and overview with the
   current user; logging.
5. Test helpers: `MonthwiseApiFactory` gets a signed-in client (creates a household admin or member
   through `IUserAccounts`, signs in, keeps the cookie and adds the request header); the existing
   API tests switch to it.
6. Regenerate the OpenAPI client; front-end `AuthGate`, sign-in, setup, change password, menu,
   Users screens, request header, 401 handling, texts in both languages.
7. Deploy scripts: `Setup__Code` creation and printing, `new-setup-code` (Docker and native, shell
   and PowerShell), Azure secret, bicep and `--new-setup-code`, secrets example, smoke test check
   that a data endpoint answers 401.
8. README and user manual chapter in English and Croatian, with screenshots.
9. Full back-end and front-end test runs, lint, and the manual checks below; changes left staged
   for review.

## Tests
- Unit tests: username and display name rules; the last-admin check (making the last active admin
  a member, inactive or deleted is refused; with two admins it is allowed); setup code
  normalisation and comparison.
- API / integration tests:
  - Every endpoint in the endpoint list, except the anonymous ones, answers 401 without a session
    (generated from the app's endpoint data source, so a new endpoint is covered automatically).
  - Setup: refused without a configured code and with a wrong one; creates the first admin and
    signs in; the same code then fails; with users, a new code resets an existing admin's password
    or creates a new admin, and household data is unchanged.
  - Sign-in: same message for unknown user, wrong password and inactive user; lockout after 5
    attempts; rate limit; sign-out ends the session.
  - Sessions survive a restart: a cookie from one factory works with a second factory on the same
    database (Data Protection keys in the database).
  - Temporary password: every endpoint except password change and sign-out answers 403 until it is
    changed.
  - Users: admin can add, edit, deactivate, delete, reset; member gets 403 and sees only themself;
    deactivating, deleting or changing a role takes effect on the user's next request; reset ends
    that user's sessions; own password change ends other sessions only.
  - Unsafe requests without `X-Monthwise-Request` are refused.
  - Members get 403 on export and import; the history records the display name.
  - Export does not contain the new tables, and an import keeps users and sessions.
  - Logs: no password, temporary password, setup code or cookie value in `LogMessages` after the
    flows above.
  - Croatian messages for the new validation errors.
- Front-end tests: `AuthGate` states (setup, sign-in, change password, app), return to the asked
  screen, 401 handling, sign-in error and lockout messages, Users screens for admin and member,
  every new text in both languages (`noHardCodedTexts` and the Croatian app test).
- Manual checks:
  - Docker one-click on macOS and Windows: a new installation prints the setup code; an update of
    an existing installation prints it and keeps the data; `new-setup-code` works.
  - Native background service on macOS: the same.
  - Sign-in from a phone on the home network over HTTP with access from other devices turned on.
  - Azure: setup with the code from Key Vault, sign-in over HTTPS, still signed in after the app
    scaled to zero, `deploy.sh --new-setup-code`.
  - Phone and desktop layout of the new screens, in English and Croatian.

## Open questions
- None.
