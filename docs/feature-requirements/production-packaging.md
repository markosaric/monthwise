# Requirements: Production packaging

- **Branch:** `feat/production_packaging`
- **Status:** Approved (2026-10-28)
- **Date:** 2026-10-28

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-production_packaging.md`) is written after these
> requirements are approved.

## 1. Goal

Today Monthwise only runs as a development setup: the front-end runs on the development server,
the API is started from the source code and the database is started by hand. Nothing can be
installed and used day to day.

This feature turns Monthwise into **one installable application**, with a production
configuration, that can be run in three ways:

1. **Docker one-click** on the user's own computer (the recommended start). The same package also
   runs unchanged on an always-on home machine or a small server later.
2. **Native background service** on the user's own computer, without Docker.
3. **Azure**, managed hosting that fits the .NET stack.

The user's own computer is a Mac or a Windows PC.

Login is its own feature. These requirements assume it is in place: every screen and every API
call of a release needs a signed-in user of the household (FR-11). Backup and restore are
described in their own requirements.

## 2. Terms

| Term | Meaning |
|---|---|
| Application | One deployable unit that serves both the Monthwise screens and the API from one address (one origin). |
| Release | One numbered version of the application, built from a commit on `main`. |
| Deployment target | One of the three ways of running Monthwise: Docker one-click, native background service, Azure. |
| Installation | Monthwise set up on one deployment target, with its own database, settings and secrets. |
| Secrets | The database password and the card number and IBAN encryption keys (see [ADR 0005](../decisions/0005-encrypted-sensitive-data.md)). |
| Update | Replacing the running release with a newer one, keeping all data. |

## 3. Functional requirements

### 3.1 The application

**FR-1 One application, one address**
- A release is one application that serves the Monthwise screens and the API together under one
  address, so the browser always calls the API on the same origin (see
  [ADR 0004](../decisions/0004-same-origin-dev-proxy.md)).
- Every screen can be opened directly by its address and after a browser refresh, not only by
  navigating from the Dashboard.
- A request for an API address that does not exist answers as an API error, never with a screen.
- The development tools that only make sense while developing (the API reference page and the
  OpenAPI document) are not available in a release.

**FR-2 Same release for every target**
- All three deployment targets run the **same** release, built once from the same commit.
- The release shows its version (release number and commit) on the Settings screen and in the
  health response, so the user can tell which version is running.

**FR-3 Database migrations**
- When a release starts against an older database, it brings the database up to date by itself,
  without manual steps.
- If the database cannot be brought up to date, the application does not start working on it: it
  reports the problem in the health response and in the log, and the data stays as it was before
  the update.

**FR-4 Health**
- The application reports whether it and its database are working, at one health address that the
  deployment targets and the start icon (FR-8) use to know when Monthwise is ready.

### 3.2 Production configuration

**FR-5 Settings outside the code**
- Every setting that differs between installations (address and port, database connection,
  secrets) is given to the application by the installation, never built into the release.
- On the user's own computer the default address is `http://localhost:8080`.
- The repository contains an example of every setting with placeholder values only, as required
  by `CONTRIBUTING.md`.
- Default values are chosen so that a new Docker one-click or native installation needs no
  settings changed except the ones created in FR-6.

**FR-6 Secrets**
- Secrets are never part of the release, the repository, the logs or error messages.
- On the **Docker one-click** and the **native background service**, the first start of a new
  installation creates the secrets when they do not exist yet, stores them only on that computer
  where only the user's account can read them, and tells the user where they are and that they
  must keep a copy in their password manager.
- On **Azure**, the secrets are kept in Azure's own secret store and given to the application from
  there.
- Monthwise refuses to start without valid encryption keys (as today) and says which setting is
  missing, without showing any secret value.

**FR-7 Logs**
- The application writes a log that the user can read on each target, without personal financial
  data (amounts, names, card numbers, IBANs) and without secrets.
- Logs on the user's own computer do not grow without limit: old log entries are removed
  automatically.

### 3.3 Deployment targets

**FR-8 Docker one-click**
- The user installs Docker once. After that, **one double-click** on a start icon (which can be put
  in the Dock on a Mac, or pinned to the taskbar or Start menu on Windows) starts Monthwise and its
  database if they are not running, waits until Monthwise is ready (FR-4) and opens it in the
  browser.
- If Docker itself is not running, the start icon starts it, or tells the user clearly that it
  must be started.
- Once started, Monthwise and its database come back by themselves whenever Docker starts, until
  the user stops them.
- The user can stop Monthwise, and update it to a new release, with one command each; an update
  keeps all data (FR-3).
- The data is kept in a place that survives stopping, updating and removing the application;
  removing the data is always a separate, explicit step.
- The same package runs unchanged on an always-on home machine or a small server with Docker.

**FR-9 Native background service**
- One install command sets Monthwise up on the user's computer without Docker: the database and
  the application.
- After installing, Monthwise starts by itself when the user logs in to the computer and keeps
  running in the background; the user only opens its address in the browser (or an app icon made
  from it in the browser).
- The user can stop, start, update and uninstall Monthwise with one command each. Uninstalling
  keeps the data unless the user explicitly asks to remove it.
- An update keeps all data (FR-3).

**FR-10 Azure**
- Monthwise can be deployed to Azure as the same release (FR-2), with a managed PostgreSQL
  database in an EU region, the secrets in Azure's secret store (FR-6) and HTTPS.
- Deploying a new release to Azure is one documented step, and keeps all data (FR-3).
- The documentation lists every Azure resource Monthwise needs, its expected monthly cost, and
  says to set a budget alert before deploying, because Azure has no hard spending cap.

**FR-11 Access**
- On every target, every screen and every API call except the health address (FR-4) and the
  login itself needs a signed-in user of the household.
- On the user's own computer (Docker one-click and native background service), Monthwise accepts
  connections only from the same computer by default. Access from other devices, for example the
  phone on the home network or through Tailscale, is a setting the user turns on.
- On Azure, Monthwise is reachable only over HTTPS.
- The health address shows only whether Monthwise and its database work and which version runs,
  never any household data.

**FR-12 Documentation**
- The user manual gets an installation chapter for each target, on macOS and Windows where it
  applies: what to install first, how to start, stop, update and uninstall, where the data, logs
  and secrets are, and how to keep a copy of the secrets.
- `README.md` describes how to build a release from the repository.

## 4. Non-functional requirements

- **NFR-1 Data protection:** secrets and personal financial data never appear in the release, the
  repository, logs, error messages or build output; real data is never committed, as required by
  `CONTRIBUTING.md`.
- **NFR-2 No data loss:** starting, stopping, updating and restarting the computer never lose
  data that was saved.
- **NFR-3 Light on the user's computer:** when idle, Monthwise and its database together use little
  memory and almost no CPU, so they can run in the background all day on a laptop.
- **NFR-4 Start time:** on the user's own computer Monthwise is ready within 30 seconds of the
  double-click or the login, once Docker or the computer has started.
- **NFR-5 Computers:** the Docker one-click and the native background service work on current
  macOS (Apple silicon and Intel) and on 64-bit Windows 11. The release also runs on Linux on
  Intel/AMD and Arm, so it fits an always-on home machine, a small server and Azure.
- **NFR-6 Built and checked by CI:** every change on `main` builds the release and checks that it
  starts and reports healthy, so a broken package is noticed before it is installed.
- **NFR-7 Same behaviour:** the screens and calculations behave the same in a release as in
  development; only the way Monthwise is started and configured changes.

## 5. Out of scope

- Login and user accounts (their own feature; assumed to be in place, FR-11).
- Backup and restore (their own requirements).
- A desktop app with its own window and installer.
- Linux desktops as the user's own computer.
- Hetzner, other cloud servers and other platforms (Fly.io, Render, Railway) as documented
  targets; the Docker one-click package runs on them, but they are not documented or tested here.
- Remote access setup (Tailscale, Cloudflare Tunnel, own domain).
- Automatic deployment from GitHub to Azure or to the user's computer on every merge.
- Automatic updates; the user starts each update.
- Importing data from the Numbers workbooks.

## 6. Acceptance criteria

1. A release serves the Monthwise screens and the API from one address; every screen opens
   directly by its address and after a refresh; unknown API addresses answer as API errors; the
   API reference page is not available.
2. All three targets run the same release; the Settings screen and the health response show its
   version.
3. Starting a newer release on an older database brings it up to date without manual steps; when
   that fails, the data is unchanged and the problem is reported.
4. No secret or personal financial data is in the repository, the release, the logs or error
   messages; on the user's own computer the first start creates missing secrets and tells the user
   where they are; without valid encryption keys Monthwise refuses to start and names the missing
   setting.
5. On a Mac and on a Windows PC with Docker installed, one double-click starts Monthwise and opens
   `http://localhost:8080` in the browser once it is ready; it comes back by itself when Docker
   starts; stop and update are one command each and keep the data.
6. On a Mac and on a Windows PC, one install command sets up the native background service;
   Monthwise then starts at login; stop, start, update and uninstall are one command each, and
   uninstall keeps the data.
7. Monthwise can be deployed to Azure as the same release with a managed database in an EU region,
   secrets in Azure's secret store and HTTPS.
8. On every target, screens and API calls need a signed-in user; the health address shows no
   household data; on the user's own computer only the same computer can connect unless the user
   turns on access from other devices.
9. The user manual has an installation chapter for each target on macOS and Windows, and
   `README.md` describes how to build a release.
10. CI builds the release on every change to `main` and checks that it starts and reports healthy.
