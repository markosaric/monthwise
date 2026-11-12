# Plan: Pinned build inputs and a fixed Docker release

- **Branch:** `fix/pin-build-inputs`
- **Status:** In progress
- **Date:** 2026-11-12
- **Requirements:** [production-packaging.md, Appendix 2](../feature-requirements/production-packaging.md#appendix-2-pinned-build-inputs-and-a-fixed-docker-release)

## Goal
A release is built only from inputs that cannot change without a reviewed change in the
repository (code review finding L5), and a Docker one-click installation runs one exact release
instead of whatever `:latest` points to.

## Requirement changes
[Appendix 2](../feature-requirements/production-packaging.md#appendix-2-pinned-build-inputs-and-a-fixed-docker-release)
adds NFR-8 and acceptance criteria 11 and 12, and changes FR-8:

- **NFR-8:** every third-party automation step in CI and the release is taken at an exact version
  its publisher cannot change, with the version readable next to it. The PostgreSQL package for
  Windows is checked against a fingerprint recorded in the repository, and the release stops
  without publishing anything when it does not match. The versions in use today are kept.
- **FR-8:** a new Docker one-click installation runs the release of its package. It stays on that
  version until the user updates. An update with or without a version leaves it on one exact
  version and reports it. An older installation that follows the newest release keeps working and
  is fixed to a version on its next update.
- Out of scope: keeping the pins current (M4), base images and the database image, npm and NuGet
  packages.

## Scope
- In scope: `.github/workflows/ci.yml`, `.github/workflows/release.yml`,
  `deploy/docker/compose.yml`, `deploy/docker/monthwise.sh`, `deploy/docker/monthwise.ps1`, the
  Docker section of the user manual and the release section of `README.md`.
- Out of scope: the `:latest` tag that the release still publishes to GHCR (harmless once nothing
  uses it by default; dropping it is a separate change), the native and Azure scripts (they already
  install one exact version: native resolves the newest release to its version and checks it
  against `SHA256SUMS`, and Azure always takes a version), and the Dockerfile base images.

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes (pages, components): none.
- Database migrations: none.

**GitHub Actions pinned to commit SHAs (NFR-8).** Every `uses:` takes the commit SHA of the newest
patch release of the major version used today, with the tag as a comment, for example
`actions/checkout@fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09 # v5.1.0`. This is the form Dependabot
(M4) updates later.

| Action | Today | Pinned to |
|---|---|---|
| `actions/checkout` | `v5` | `v5.1.0` `fbc6f39…` |
| `actions/setup-dotnet` | `v5` | `v5.4.0` `26b0ec1…` |
| `actions/setup-node` | `v5` | `v5.0.0` `a0853c2…` |
| `actions/upload-artifact` | `v4` | `v4.6.2` `ea165f8…` |
| `actions/download-artifact` | `v4` | `v4.3.0` `d3f86a1…` |
| `docker/setup-qemu-action` | `v3` | `v3.7.0` `c7c5346…` |
| `docker/setup-buildx-action` | `v3` | `v3.12.0` `8d2750c…` |
| `docker/login-action` | `v3` | `v3.7.0` `c94ce9f…` |
| `docker/build-push-action` | `v6` | `v6.19.2` `10e90e3…` |

Each SHA is what the major tag points to today, so the code that runs does not change. A new CI
step in the `release` job fails when a workflow has a `uses:` that is not a 40-character SHA
followed by a version comment, so an unpinned step cannot come back unnoticed.

**PostgreSQL for Windows checked against a recorded SHA-256 (NFR-8).** `release.yml` gets a
`POSTGRES_WINDOWS_SHA256` value next to `POSTGRES_WINDOWS: 17.6-1`. The *PostgreSQL for Windows*
step checks the downloaded zip with `sha256sum --check` before it is repacked; a mismatch fails the
step, and with it the job, so `gh release create` never runs. The value is taken from the official
EDB download once, and the comment says that changing the version means changing the checksum in
the same commit.

**Docker one-click runs one exact version (FR-8).**
- `monthwise.sh` and `monthwise.ps1` get a `RELEASE_VERSION` value that is `dev` in the repository.
  The *Docker and native scripts* step of `release.yml` replaces it with the release version
  before zipping the package, and checks that the replacement happened.
- A new installation writes `MONTHWISE_IMAGE=ghcr.io/markosaric/monthwise:<RELEASE_VERSION>` into
  `compose.env` instead of `:latest`; the same value is the fallback when the line is missing.
- `update <version>` keeps working as it does today. `update` without a version asks GitHub for the
  newest release (`gh release view --json tagName`, gh is already required and signed in) and
  writes that exact version. Both report the running version from the health response, as today.
  An installation with `:latest` in `compose.env` gets an exact version on that first update.
- Run from the repository (version `dev`) without `MONTHWISE_IMAGE` in `compose.env`, the scripts
  stop with a message to use a release package or `update <version>`, instead of pulling an image
  that does not exist.
- `compose.yml` no longer falls back to `:latest`: `MONTHWISE_IMAGE` is required with a message,
  like `MONTHWISE_CONFIG_DIR`. The scripts and the CI smoke test always set it.

## Steps
1. Pin every `uses:` in `ci.yml` and `release.yml` to the SHAs above with version comments.
2. Add the CI check for unpinned actions to the `release` job of `ci.yml`.
3. Record the SHA-256 of `postgresql-17.6-1-windows-x64-binaries.zip` and check it in
   `release.yml`.
4. Add `RELEASE_VERSION` to both Docker scripts, use it for new installations and as the fallback,
   resolve the newest release in `update`, and stamp the version in `release.yml`.
5. Make `MONTHWISE_IMAGE` required in `compose.yml`.
6. Update the user manual (Docker `update` stays on the version it moved to) and `README.md`
   (pinned actions and the PostgreSQL checksum when changing versions).

## Tests
- Unit tests: none (no application code changes).
- API / integration tests: the CI `release` job still runs shellcheck, PSScriptAnalyzer, the image
  build and the Docker smoke test with `MONTHWISE_IMAGE=monthwise:ci`, plus the new pinning check.
- Manual checks:
  - The pinning check fails on a workflow with `uses: actions/checkout@v5` and passes on the pinned
    workflows.
  - The checksum step passes with the real zip and fails with a changed one (run locally).
  - The release stamping, run locally on a copy of `deploy/docker`, leaves no `dev` behind.
  - `monthwise.sh` with a stamped version: a new configuration has the exact version; `update` with
    a `:latest` configuration writes the newest release version; `update 1.2.0` writes `1.2.0`;
    the repository copy without a version stops with the message.

## Open questions
- None.
