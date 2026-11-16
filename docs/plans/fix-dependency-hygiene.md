# Plan: Dependency hygiene

- **Branch:** `fix/dependency-hygiene`
- **Status:** In progress
- **Date:** 2026-11-16
- **Requirements:** [production-packaging.md, Appendix 4](../feature-requirements/production-packaging.md#appendix-4-no-known-high-risk-vulnerabilities-kept-current-automatically)

## Goal
No front-end or back-end package has a known high or critical vulnerability, CI stops any change
that brings one in, and the repository proposes newer versions every week (code review finding
M4).

## Requirement changes
[Appendix 4](../feature-requirements/production-packaging.md#appendix-4-no-known-high-risk-vulnerabilities-kept-current-automatically)
adds NFR-9, NFR-10 and acceptance criteria 13 to 15, and extends NFR-6:

- **NFR-9:** a change merges only when no front-end or back-end package (direct or brought in)
  has a known high or critical vulnerability. CI checks every change and `main` and names the
  package, rating and advisory; low and moderate ones are reported but do not stop a change. The
  packages in use when this merges have no known high or critical vulnerability, and every low or
  moderate one with a fixed version available is fixed. Build-only tools are declared as
  development packages.
- **NFR-10:** weekly update proposals for front-end packages, back-end packages and CI automation
  steps, one pull request per kind, minor and patch grouped, majors on their own; security fixes
  proposed as soon as published; nothing merged automatically; NFR-8 pins kept; Conventional
  Commits titles (`build`, `ci`); no requirements document or plan needed, as `CONTRIBUTING.md`
  says.
- **NFR-6:** CI also runs the vulnerability check of NFR-9.
- Out of scope: base images and the database image, automatic merging, new major versions.

## Scope
- In scope: `frontend/package.json`, `frontend/package-lock.json`, `backend/Directory.Build.props`,
  `.github/workflows/ci.yml`, a new `.github/dependabot.yml`, `CONTRIBUTING.md`, and the README
  section on CI if it lists the checks.
- Out of scope: application code, `Dockerfile` base images, the release workflow (it builds from
  the same lock files CI checks), major version upgrades.

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes (pages, components): none. Only package versions and declarations change.
- Database migrations: none.

**Front-end packages (NFR-9).** Today `npm audit` reports 16 vulnerabilities (1 low, 8 moderate,
7 high: vite, zod, undici, systeminformation and others). `npm audit fix` moves them all to fixed
versions within the ranges in `package.json` (vite 8.3.8, zod 4.6.7, …); no major version changes.
`zod` gets its range raised to `^4.6.7` so a fresh install cannot fall back to a vulnerable version.

After that, 6 remain (2 low, 4 moderate), all inside the `shadcn` command-line tool, which is only
run by a developer to add a component and is never part of the release:
- `deepmerge` (low, via `@shadcn/registry`): no fixed version exists. Allowed by NFR-9.
- `minimatch` (moderate, via `ts-morph`): fixed only in a newer major version than `ts-morph`
  accepts. npm's only offer is going back to `shadcn@1.0.0`, which is wrong. I leave these until
  `shadcn` itself moves on (Dependabot will propose that) instead of forcing a major version
  underneath it with an override. Requirement wording "with a fixed version available" is read as
  "fixable without a major version change", matching the appendix's out-of-scope line on majors.

**Packages declared by purpose.** `shadcn`, `tailwindcss` and `@tailwindcss/vite` move from
`dependencies` to `devDependencies`. They are used by `vite.config.ts`, `src/index.css` and the
component generator only at build time; Vite bundles their CSS output. The release image runs
`npm ci` without `--omit=dev`, so the build is unchanged. Afterwards `npm audit --omit=dev` (the
packages whose code ends up in the browser) reports nothing.

**Back-end packages (NFR-9).** The .NET 10 SDK already checks every package, including the ones
brought in, against the GitHub advisory database on `dotnet restore` (NuGet audit, warnings
NU1901 to NU1904), and `TreatWarningsAsErrors` turns all four into errors. That stops a change for
low and moderate ones too, which NFR-9 does not want. In `backend/Directory.Build.props`:
- `NuGetAudit=true` and `NuGetAuditMode=all`, written out so the check does not depend on SDK
  defaults;
- `WarningsNotAsErrors` `NU1901;NU1902` (low, moderate): reported as warnings, not errors;
- NU1903 and NU1904 (high, critical) stay errors, so restore fails locally and in CI and the error
  names the package, rating and advisory link.

CI on `main` is green today, so no NuGet package currently has a known vulnerability of any
rating; the CI run on this branch confirms it (there is no .NET SDK in my environment).

**CI (NFR-6, NFR-9, criterion 14).** In `.github/workflows/ci.yml`:
- Front-end job, right after `npm ci`: `npm audit --audit-level=high`. It prints every finding
  with its advisory and fails only on high or critical.
- Back-end job, after `dotnet restore`: a step `dotnet list package --vulnerable
  --include-transitive` that lists every vulnerable package with rating and advisory in one place,
  so low and moderate findings are visible in the log and not only as build warnings. The failing
  check is the restore itself (above).

A new advisory against an unchanged package makes the next CI run fail; the Dependabot security
update for it (below) is the fix.

**Update proposals (NFR-10, criterion 15).** New `.github/dependabot.yml` (version 2):

| Ecosystem | Directory | Commit prefix | Groups |
|---|---|---|---|
| `npm` | `/frontend` | `build` | `minor-and-patch` (version updates) |
| `nuget` | `/backend` | `build` | `minor-and-patch` (version updates) |
| `github-actions` | `/` | `ci` | `minor-and-patch` (version updates) |

- `schedule.interval: weekly` (Monday, `Europe/Zagreb`), `commit-message.include: scope`, which
  gives titles such as `build(deps): bump vite from 8.3.8 to 8.4.0 in /frontend`.
- Majors are not in the group, so each arrives as its own pull request.
- Dependabot updates a SHA-pinned action together with its `# vX.Y.Z` comment, so the NFR-8 pin
  check keeps passing.
- `open-pull-requests-limit: 5` per ecosystem.
- Security fixes "as soon as published" come from Dependabot security updates, which use this
  same file for prefixes. They are a repository setting, not a file: **you need to turn on
  Dependabot alerts and Dependabot security updates** under Settings → Advanced Security after
  merging. I can't do that from here.

**CONTRIBUTING.md.** New short section "Dependency updates": Dependabot proposes updates weekly and
security fixes immediately; such a pull request needs no requirements document or plan; review the
release notes, let CI pass and merge with Squash and merge; a major version that needs code changes
is closed and redone as its own `build/` or `fix/` branch with a plan. Also mention that CI fails on
high or critical vulnerabilities and how to check locally (`npm audit`, `dotnet restore`).

## Steps
1. `npm audit fix` in `frontend`, raise `zod` to `^4.6.7`, move the three build tools to
   `devDependencies`, `npm install` to refresh the lock file.
2. Run lint, format check, type-check, tests and build in `frontend`; check `npm audit` and
   `npm audit --omit=dev`.
3. Set the NuGet audit properties in `backend/Directory.Build.props`.
4. Add the audit steps to `ci.yml` and add `.github/dependabot.yml`.
5. Update `CONTRIBUTING.md` (and README if it lists the CI checks).
6. Leave everything staged for review.

## Tests
- Unit tests: none new; the existing front-end tests and build must pass with the updated
  packages.
- API / integration tests: back-end tests run unchanged in CI.
- Manual checks:
  - `npm audit --audit-level=high` exits 0; the 6 remaining findings are the ones above.
  - Criterion 14 (front-end): temporarily install `zod@4.6.5` locally and confirm
    `npm audit --audit-level=high` exits non-zero and names zod and the advisory (not committed).
  - Criterion 14 (back-end): confirmed by the NuGet audit rules (NU1903/NU1904 errors); the CI run
    of this branch shows the new list step and a passing restore.
  - The release job in CI builds the image and passes the smoke checks.
  - `dependabot.yml` is validated by GitHub once merged (Insights → Dependency graph →
    Dependabot); first proposals appear the following Monday.

## Open questions
- None.
