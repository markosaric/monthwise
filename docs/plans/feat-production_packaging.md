# Plan: Production packaging

- **Branch:** `feat/production_packaging`
- **Status:** In progress
- **Date:** 2026-10-28
- **Requirements:** [production-packaging.md](../feature-requirements/production-packaging.md) (approved)

## Goal
Monthwise is built as one release in which the API also serves the built React front-end. The
same release runs in three ways: a Docker one-click start on a Mac or Windows PC, a native
background service on a Mac or Windows PC without Docker, and Azure Container Apps with a managed
PostgreSQL database. Settings and secrets live outside the code, and CI builds the release and
checks that it starts.

## Scope
- In scope:
  - API: serving the built front-end with a fallback for screen addresses, an API-only 404, the
    version and the migration state in the health response, a gate that stops the API while
    migrations failed, settings and secrets read from a configuration folder, a rolling log file,
    and forwarded headers for Azure.
  - Front-end: the version on the Settings screen.
  - Release build: a `Dockerfile` (multi-architecture image) and a native publish for macOS
    (arm64, x64), Windows (x64) and Linux (x64, arm64), versioned from a git tag.
  - Docker one-click: a compose file, start icons for macOS (`Monthwise.command`) and Windows
    (`Monthwise.cmd`), and a `monthwise` command for start, stop, update, status, logs and remove.
  - Native background service: install scripts for macOS (bash) and Windows (PowerShell) that set up
    the release and their own PostgreSQL cluster, start both at login, and add a `monthwise`
    command for start, stop, update, status, logs and uninstall.
  - Azure: a Bicep template and one deploy script for Container Apps, PostgreSQL Flexible Server,
    Key Vault and a budget alert.
  - CI: a release job on every change (build the image, start it with a database, run smoke
    checks) and a release workflow on a version tag (publish the image to GHCR and the native and
    Docker packages as a GitHub Release).
  - Docs: ADR 0006, `INIT.md` architecture, `README.md` "Building a release", and an installation
    chapter in the user manual.
- Out of scope:
  - Everything in section 5 of the requirements: login itself, backup and restore, a desktop app,
    Linux desktops, other hosting providers, remote access setup, automatic deployment and
    updates, importing Numbers data.
  - **The login part of FR-11 and acceptance criterion 8:** this branch is merged before login.
    The login feature adds the `401` smoke check and confirms that every screen and API call needs
    a signed-in user on all three targets. Until then the own-computer targets accept only
    connections from the same computer by default, and the Azure deployment stays a documented
    option to use only after login.
  - PostgreSQL major version upgrades of an existing installation (documented as a manual step).
  - Code signing of the scripts (macOS Gatekeeper and Windows SmartScreen are handled by
    documented one-time steps).

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| FR-1 | The API serves `wwwroot` (the built front-end) with `UseDefaultFiles`/`UseStaticFiles`. `MapFallbackToFile("index.html")` answers every non-`/api` path, so deep links and refreshes work. `/api/{**rest}` that matches no endpoint answers `404` problem details. `index.html` is sent with `Cache-Control: no-cache`; hashed assets under `/assets` are cached for a year. Scalar and `/openapi` stay mapped only in Development (as today). |
| FR-2 | One release = one git tag `vX.Y.Z`. The build sets `Version` from the tag, and the .NET SDK adds the commit to `InformationalVersion` (`1.2.0+<sha>`). The same `dotnet publish` output goes into the Docker image and into the native packages. Builds outside a tag are `0.0.0-dev+<sha>`. |
| FR-2, FR-4 | `GET /api/health` gets `version` (the informational version) and `migrations` (`Applied`, `Pending` or `Failed`). The Settings screen shows the version in a small "About" line under the tiles, read from the health response. |
| FR-3 | Releases run with `Database:MigrateOnStartup=true`. EF Core applies all pending migrations in one transaction (EF Core 9+ default; this is checked by a test), so a failure leaves the database as it was. On failure the migration state is `Failed`, health answers `503`, and a small middleware answers `503` problem details for every `/api` call except `/api/health`, so the screens show the existing "API not reachable" state instead of working on an old schema. |
| FR-5 | A **configuration folder** per installation, given by `MONTHWISE_CONFIG_DIR` (or `--config-dir`). The API loads `<dir>/settings.json` (optional, non-secret settings) and every file in `<dir>/secrets/` as one setting through key-per-file configuration (file name `SensitiveData__HashKey` = setting `SensitiveData:HashKey`). The same mechanism is used on all three targets. Default address on the own computer: `http://localhost:8080` (Docker port mapping and native `ASPNETCORE_URLS`). `deploy/settings.example.json` and `deploy/secrets.example/` hold placeholders only. |
| FR-6 | Secrets: `postgres_password`, `ConnectionStrings__Monthwise`, `SensitiveData__ActiveKeyId`, `SensitiveData__EncryptionKeys__k1`, `SensitiveData__HashKey`. The start and install scripts create them only when the `secrets` folder is missing **and** no database exists yet; with an existing database and missing secrets they stop and tell the user to put the saved values back (new keys would make card numbers unreadable). Files are `chmod 600` in a `700` folder on macOS and limited to the current user with `icacls` on Windows. After creating them the script prints the folder and asks the user to copy the values into their password manager. On Azure the deploy script creates them once in Key Vault; Container Apps mounts them as secret files in `/config/secrets`. |
| FR-7 | Production logging: `Information`, EF commands at `Warning`, sensitive data logging stays off. Docker: the `json-file` driver with `max-size: 10m`, `max-file: 3`. Native: a daily rolling file in `<data>/logs`, 14 files kept (`Serilog.Extensions.Logging.File`, enabled only when `Logging:File:Path` is set). Azure: Container Apps console logs (Log Analytics, 30 days). |
| FR-8 | Docker data: the named volume `monthwise-pgdata`, never removed by stop or update; `monthwise remove --data` asks for confirmation before deleting it. The database container is not published to the host, so it never conflicts with the development database on port 5432. Both containers use `restart: unless-stopped`. |
| FR-9 | Native: an own PostgreSQL 17 cluster in `<data>/pgdata`, listening on `127.0.0.1:5442` only (no clash with the development database on 5432). macOS uses the binaries of Homebrew `postgresql@17`; Windows uses the official EDB binaries zip (pinned version and SHA-256), installed in the user folder without admin rights. Both PostgreSQL and the API start at login: two launchd agents on macOS, two Task Scheduler logon tasks on Windows (run hidden with `conhost --headless`). |
| FR-10 | Azure Container Apps (Consumption, minimum 0 replicas, so it costs nothing while unused and the first request after a quiet spell takes a few seconds; maximum 1 replica) with HTTPS-only ingress (`allowInsecure: false`), PostgreSQL Flexible Server B1ms in the chosen EU region (default West Europe) with SSL required, Key Vault for the secrets with the app's managed identity, and a monthly budget alert (`Microsoft.Consumption/budgets`) with the user's email. The image is pulled from GHCR with a read-only token stored as a Container Apps secret. `UseForwardedHeaders` (proto and for) is enabled when `Hosting:BehindProxy=true`, so redirects and secure cookies see HTTPS. |
| FR-11 | Login and its authorization come from the login feature: this plan only keeps `/api/health` anonymous. The smoke check that a protected endpoint answers `401` is added by the login feature, which is merged after this branch. Own computer: Docker publishes `127.0.0.1:8080:8080` and native binds `http://127.0.0.1:8080`. `monthwise network on` changes this to all addresses (`0.0.0.0`) and prints a warning; `monthwise network off` changes it back. Data Protection keys (used by the login cookie) are stored in the database, so a restart or a second Azure replica does not sign the user out, unless the login feature already does this. |
| NFR-5 | Image for `linux/amd64` and `linux/arm64`; native packages for `osx-arm64`, `osx-x64`, `win-x64`, `linux-x64` and `linux-arm64`, self-contained, so no .NET runtime is needed on the computer. |
| NFR-6 | CI job `release` on every pull request and push to `main`: builds the image, starts it with a throwaway database and made-up secrets, and runs the smoke checks (below). |

### Locations
| Target | Configuration folder | Data | Logs |
|---|---|---|---|
| Docker, macOS | `~/Library/Application Support/Monthwise/docker` | Docker volume `monthwise-pgdata` | `monthwise logs` |
| Docker, Windows | `%LOCALAPPDATA%\Monthwise\docker` | Docker volume `monthwise-pgdata` | `monthwise logs` |
| Native, macOS | `~/Library/Application Support/Monthwise/native` | `…/native/pgdata` | `…/native/logs` |
| Native, Windows | `%LOCALAPPDATA%\Monthwise\native` | `…\native\pgdata` | `…\native\logs` |
| Azure | `/config` (secret volume from Key Vault) | PostgreSQL Flexible Server | Log Analytics |

### Getting a release onto the computer
The repository is private, so the packages are not public downloads. The user signs in once with
the GitHub CLI (`gh auth login`); the scripts use `gh release download` for the packages and
`gh auth token` for `docker login ghcr.io`. Updates use the same sign-in.

- Domain / data model changes: none.
- API changes (endpoints, DTOs):
  - `HealthResponse` gets `Version` (string) and `Migrations` (`Applied` | `Pending` | `Failed`);
    the OpenAPI document and the generated client are updated.
  - Static files, SPA fallback, API 404 fallback, the migration gate, the configuration folder,
    the file log, forwarded headers and Data Protection key storage in `Program.cs` and
    Infrastructure `DependencyInjection`.
- Front-end changes (pages, components):
  - Settings screen: an "About" line with the version from `useHealth`.
  - No change to API calls: they already use relative `/api` URLs (ADR 0004).
- Database migrations:
  - `AddDataProtectionKeys`: the `data_protection_keys` table (only if the login feature has not
    added it).

### Repository layout
```
Dockerfile                       multi-stage: front-end build → dotnet publish → aspnet runtime image
.dockerignore
deploy/
  settings.example.json          non-secret settings with defaults
  secrets.example/               one placeholder file per secret
  docker/
    compose.yml                  db + app, volume, localhost port, log rotation
    Monthwise.command            macOS start icon (calls monthwise.sh start --open)
    Monthwise.cmd                Windows start icon (calls monthwise.ps1 start -Open)
    monthwise.sh, monthwise.ps1  start | stop | update | status | logs | network | remove
  native/
    install.sh, install.ps1      one-step install (PostgreSQL cluster, release, logon start)
    monthwise.sh, monthwise.ps1  start | stop | update | status | logs | network | uninstall
    macos/*.plist.template       launchd agents for PostgreSQL and the API
  azure/
    main.bicep                   Container Apps, PostgreSQL, Key Vault, Log Analytics, budget
    deploy.sh                    one step: secrets (first time), Bicep deployment, wait for health
.github/workflows/
  ci.yml                         + release smoke job
  release.yml                    on tag v*: GHCR image, native and Docker packages, GitHub Release
docs/decisions/0006-single-release-api-serves-frontend.md
```

## Steps
1. **ADR 0006:** the API serves the built front-end in production (one origin, follows up ADR 0004),
   one release for all targets, configuration folder with key-per-file secrets. Update the ADR index
   and the hosting line in `INIT.md`.
2. **API serving the front-end (FR-1):** static files, cache headers, SPA fallback, API 404 fallback.
   The Api project copies a built front-end into `wwwroot` on publish when `FrontendDist` is set;
   `wwwroot` is git-ignored.
3. **Configuration folder and secrets (FR-5, FR-6):** `MONTHWISE_CONFIG_DIR` / `--config-dir`,
   `settings.json`, key-per-file `secrets`, the error message without values (as today).
4. **Migrations and health (FR-2, FR-3, FR-4):** migration state service, migration gate
   middleware, `Version` and `Migrations` in the health response, OpenAPI document and client
   regenerated, version on the Settings screen.
5. **Logging and proxy (FR-7, FR-10):** production log levels, optional rolling file log,
   forwarded headers behind `Hosting:BehindProxy`, Data Protection keys in the database with
   migration (if needed).
6. **Release build (FR-2, NFR-5):** `Dockerfile` and `.dockerignore` (non-root user, port 8080,
   `HEALTHCHECK` on `/api/health`), version from the tag, native `dotnet publish` per runtime with
   the front-end included.
7. **Docker one-click (FR-8, FR-11):** `compose.yml`, `monthwise.sh`/`.ps1`, the start icons, first-start
   secret creation, the readiness wait and opening the browser, and the network switch.
8. **Native background service (FR-9, FR-11):** install scripts for macOS and Windows, PostgreSQL
   cluster setup, logon start, the `monthwise` command, update with the previous version kept for
   going back, uninstall that keeps the data.
9. **Azure (FR-10):** `main.bicep` and `deploy.sh` (works in Azure Cloud Shell), first-time secrets
   in Key Vault, budget alert, a wait for a healthy revision.
10. **CI (NFR-6):** the `release` smoke job in `ci.yml` (with `shellcheck` on the bash scripts and
    PSScriptAnalyzer on the PowerShell scripts) and `release.yml` for tags.
11. **Docs (FR-12):** `README.md` "Building a release", a user manual chapter "Installing Monthwise"
    with one part per target on macOS and Windows (install, start, stop, update, uninstall, where
    data, logs and secrets are, how to save the secrets, turning on network access, Azure resources
    and cost).
12. Leave all changes staged for review.

## Tests
- Unit tests:
  - Key-per-file and `settings.json` loading from the configuration folder; missing keys name the
    setting and never print a value.
  - Migration state: `Applied`, `Pending`, `Failed`.
- API / integration tests (`Monthwise.Api.Tests`, with a temporary `wwwroot`):
  - `/`, `/month` and `/settings/cards` answer `index.html`; `/assets/x.js` answers the file with a
    long cache header; `index.html` has `no-cache`.
  - `/api/does-not-exist` answers `404` problem details, never HTML.
  - Health contains the version and the migration state; with the state `Failed` health answers
    `503` and `/api/cards` answers `503` problem details.
  - Applying migrations is one transaction: a failing last migration leaves the earlier pending
    ones unapplied (test database).
  - `/scalar` and `/openapi` are not mapped outside Development.
- Front-end tests: the Settings screen shows the version from the health response.
- CI smoke checks (Docker image with a throwaway database): health becomes `Healthy` within 30
  seconds; `/` and `/month` return the front-end; `/api/does-not-exist` returns `404` JSON; a
  the logs contain no made-up secret value.
- Manual checks:
  - On a Mac (Apple silicon) and a Windows 11 PC: Docker one-click first start creates the secrets,
    opens `http://localhost:8080`, comes back after a Docker restart, update keeps the data, and
    another device cannot connect until `monthwise network on`.
  - Native on both: install, start at login, ready within 30 seconds (NFR-4), update and going
    back, uninstall keeps the data, idle memory and CPU (NFR-3).
  - Starting with the database present and the secrets folder missing stops with the message to
    restore the secrets, and creates no new keys.
  - Azure: deploy from Cloud Shell, HTTP is refused or redirected, HTTPS works, a second deploy
    with a new tag keeps the data, the budget alert exists, the app scales to zero when unused and
    wakes on the next request; the deployment is removed again after the check, as it has no login
    yet.
  - An Intel Mac is checked if one is available; otherwise only the `osx-x64` build is checked by
    CI.

## Decisions
- **Order with login (2026-10-28):** this branch is merged first; the login feature adds the `401`
  smoke check and the login part of FR-11 later.
- **Azure idle cost (2026-10-28):** the Container App scales to zero replicas when unused.
- **Getting releases (2026-10-28):** each computer signs in once with `gh auth login` to download
  releases and images from the private repository.
