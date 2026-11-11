# Plan: Private Docker secret files

- **Branch:** `fix/secret_file_permissions`
- **Status:** Done
- **Date:** 2026-11-11
- **Requirements:** [production-packaging.md, Appendix 1](../feature-requirements/production-packaging.md#appendix-1-each-secret-file-is-private-on-its-own)
  (code review finding L4)

## Goal
With the Docker one-click on macOS, every secret file can be read only by the user's account,
on new installations and on existing ones after their next start, and Monthwise keeps working.

## Requirement changes
[Appendix 1](../feature-requirements/production-packaging.md#appendix-1-each-secret-file-is-private-on-its-own)
extends FR-6 and acceptance criterion 4:

- On the Docker one-click on macOS, only the user's account can read each secret file itself,
  not only through the private `Monthwise` folder around it.
- New installations create the secret files private; existing installations are made private on
  their next start, without changing any secret value and without the user doing anything.
- Monthwise starts, stops, updates and keeps its data as before.
- The native background service and Windows are unchanged.

This replaces the line "Docker's are `644` below that folder so the containers (other user ids)
can read them" in FR-6 of [feat-production_packaging.md](feat-production_packaging.md).

## Scope
- In scope: `deploy/docker/monthwise.sh` (secret files `600` on macOS, new and existing).
- Out of scope: the folder modes (`Monthwise` stays `700`; `docker` and `secrets` stay `755`),
  the native service script (already `600`), `monthwise.ps1` (Windows ACLs), Azure, and Linux
  hosts (Linux desktops are out of scope, see section 5 of the requirements).

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes (pages, components): none.
- Database migrations: none.
- Script:
  - New function `protect_secrets`: on macOS, `chmod 600` every file in `secrets/`. It runs on
    every start (from `ensure_config`, after `ensure_secrets`), so new and existing
    installations are covered and it is safe to repeat. It never reads or rewrites a value.
  - `ensure_secrets` drops its own `chmod 644` and relies on `protect_secrets`.
  - On Linux the files stay `644`: a Linux bind mount keeps the host's file owner, so the
    `postgres` (uid 999) and `app` (`$APP_UID`) containers could not read a `600` file owned by
    the user, and the database would not start. The `700` `Monthwise` folder still protects them
    there. The comment in the script says why.
- Why `600` works on macOS: Docker Desktop (VirtioFS) and OrbStack share files from the Mac to
  the containers as the Mac user, so a container process can read any file that user can read,
  whatever its uid in the container. Step 1 checks this on the user's Mac before the script
  changes.
- Step 1 result (2026-11-11, Docker Desktop 4.98.0): a `600` file owned by the Mac user was read
  as uid 999 in `postgres:17`, as uid 1654 in `mcr.microsoft.com/dotnet/aspnet:10.0`, and as a
  compose file secret; inside the containers the file shows as owned by the reading uid.

## Steps
1. Check on the user's Mac (Docker Desktop or OrbStack, whichever is installed), with a throwaway
   folder and the same images, nothing from the Monthwise installation:
   `chmod 600` a made-up file, then read it as uid 999 in `postgres:17` and as `$APP_UID` in the
   Monthwise image. If either read fails, stop and come back with another design.
2. Add `protect_secrets` to `deploy/docker/monthwise.sh`, call it from `ensure_config`, remove the
   `chmod 644` line, and update the comment above `ensure_config`.
3. Run `shellcheck` on the script (as CI does).
4. Leave the changes staged for review.

## Tests
- Unit tests: none (shell script only).
- API / integration tests: none; CI's `shellcheck` covers the script.
- Manual checks (on the user's Mac):
  - Existing installation: `monthwise.sh start`; `ls -l` on `secrets/` shows `-rw-------` for every
    file; Monthwise opens on `http://localhost:8080` with the existing data; the database
    container is healthy (`monthwise.sh status`).
  - `monthwise.sh stop` and `start` again: still `600`, still starts.

## Open questions
- None.
