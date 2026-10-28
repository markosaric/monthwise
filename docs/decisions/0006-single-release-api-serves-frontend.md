# 0006: One release in which the API serves the front-end

- **Status:** Accepted
- **Date:** 2026-10-28

## Context
[ADR 0004](0004-same-origin-dev-proxy.md) keeps the front-end and the API on one origin in
development and left open how production does the same. Monthwise runs in three ways
([requirements](../feature-requirements/production-packaging.md)): Docker one-click and a native
background service on a Mac or Windows PC, and Azure Container Apps. Each needs its own settings
and secrets, which must never be part of the release.

## Decision
- **The API serves the built front-end.** A release contains the Vite build in `wwwroot`. Static
  files are served first; a request no endpoint matched answers `404` problem details under
  `/api` and `index.html` everywhere else, so screen addresses and refreshes work. This is a
  middleware, not `MapFallback`, because a catch-all fallback route won over some longer API
  routes. `index.html` is revalidated on every request; hashed files in `/assets` are cached for a
  year. No reverse proxy is needed on the user's computer.
- **One release for every target.** A version tag `vX.Y.Z` builds one image (GHCR, linux/amd64 and
  arm64) and self-contained native packages from the same commit. The version is shown as
  `X.Y.Z+<commit>` in the health response and on the Settings screen.
- **A configuration folder per installation.** `--config-dir` or `MONTHWISE_CONFIG_DIR` names a
  folder with `settings.json` (non-secret settings) and `secrets/`, one file per secret, read with
  key-per-file configuration (`secrets/SensitiveData__HashKey` → `SensitiveData:HashKey`). Docker
  mounts it, the native service passes it, and Azure mounts Key Vault secrets as files at the same
  place. Values in the folder win over appsettings and environment variables.
- **Migrations on startup, safely.** A release waits for its database (`Database:StartupWaitSeconds`)
  and stops when it never answers, so the service manager starts it again. When a migration fails,
  the migrations applied in that run are reverted and every API call except `/api/health` answers
  `503` until the problem is fixed.

## Consequences
- The front-end and the API are always deployed together, in the same version.
- Development is unchanged: without `wwwroot` the API serves no screens and Vite does (ADR 0004).
- The same secret file names are used everywhere, so a database and its secrets can move between
  targets.
- Login (cookie authentication) needs its Data Protection keys kept outside the container, so a
  restart does not sign the user out; this is part of the login feature.
