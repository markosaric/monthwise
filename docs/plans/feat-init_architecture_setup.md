# Plan: Initial architecture setup

- **Branch:** `feat/init_architecture_setup`
- **Status:** Draft
- **Date:** 2026-10-03

## Goal
A developer can clone the repository, start PostgreSQL with one command, run the .NET 10 Web API
and the React + TypeScript front-end locally, and see the front-end call the API, which talks to the
database. CI builds, lints and tests both halves on every push. No finance features yet: this is the
skeleton the features from [INIT.md](../INIT.md) section 6 are built on.

## Scope
- In scope:
  - Repository layout as described in INIT.md section 4 (`/frontend`, `/backend`, `docker-compose.yml`).
  - .NET solution with `Domain`, `Application`, `Infrastructure` and `Api` projects and their
    dependency rules, plus `Domain.Tests` and `Api.Tests`.
  - EF Core + Npgsql wiring with an empty `DbContext` and a first (empty) migration.
  - Health endpoint that checks the database connection.
  - OpenAPI document from the API and a generated TypeScript client in the front-end.
  - React app shell: Vite, React Router, TanStack Query, Tailwind CSS, shadcn/ui, a layout with
    navigation to the six MVP pages as empty placeholders, and a status widget calling the health endpoint.
  - Shared conventions: money as integer cents (`Money` value object), nullable reference types,
    warnings as errors, formatting/linting on both sides.
  - GitHub Actions CI: build, lint and test for back-end and front-end.
  - First ADRs in `docs/decisions/` recording the main architecture choices.
- Out of scope:
  - Domain entities and calculation logic (installments, monthly summary): next plan, INIT.md step 2.
  - Authentication (ASP.NET Core Identity): separate plan; the API pipeline only reserves the place for it.
  - Real feature endpoints and screens (INIT.md steps 3 to 5).
  - Hosting, deployment, containerising the API/front-end, importing Numbers history.

## Design

### Overall
```
Browser ──► React SPA (Vite dev server :5173)
               │  /api/* proxied by Vite in development
               ▼
            Monthwise.Api (ASP.NET Core :5080)
               │
               ▼
            PostgreSQL (Docker :5432)
```
In development Vite proxies `/api` to the API, so the browser sees a single origin and cookie auth
later needs no CORS setup. Production hosting is decided in a later plan.

### Back-end layering
| Project | Contains | May reference |
|---|---|---|
| `Monthwise.Domain` | entities, value objects (`Money`, `YearMonth`), pure calculation logic | nothing (no NuGet packages except BCL) |
| `Monthwise.Application` | use cases (one handler class per use case), DTOs, FluentValidation validators, interfaces for persistence (`IMonthwiseDbContext` or repositories) | Domain |
| `Monthwise.Infrastructure` | `MonthwiseDbContext`, EF Core configurations, migrations, implementations of Application interfaces | Application, Domain |
| `Monthwise.Api` | minimal API endpoint groups, DI composition root, auth, OpenAPI, ProblemDetails error handling | Application, Infrastructure (only for DI registration) |

- Endpoints use **minimal APIs** grouped per feature (`MapGroup("/api/periods")` etc.) in
  `Api/Endpoints/<Feature>Endpoints.cs`. Endpoints stay thin: bind, call the Application handler, map the result.
- No MediatR: Application handlers are plain classes registered in DI. Keeps the stack small for a 1 to 2 user app.
- Each layer exposes an `AddDomain/AddApplication/AddInfrastructure(...)` extension for DI registration.
- Errors are returned as RFC 9457 ProblemDetails; validation failures as `ValidationProblem`.
- Layer rules are enforced by a test (NetArchTest or a reflection test in `Api.Tests`) so Domain never
  gains a dependency on EF Core or ASP.NET.

### Cross-cutting conventions
- **Money:** `Money` value object in Domain wrapping `long Cents` (EUR only for now). Stored as `bigint`
  `*_cents` columns via an EF Core value converter. No `decimal`/`double` for amounts anywhere,
  including DTOs (`amountCents: number` in TypeScript; formatting to "1.234,56 €" happens only in the UI).
- **Dates:** `DateOnly` for due/paid dates, `YearMonth` value object for periods, `DateTimeOffset` (UTC)
  for timestamps such as `closed_at`.
- **Naming in PostgreSQL:** snake_case tables and columns (`EFCore.NamingConventions`), matching INIT.md section 3.
- **IDs:** `Guid` (v7, time-ordered) primary keys.
- **Build settings:** `Directory.Build.props` with `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`,
  `ImplicitUsings=enable`; NuGet versions in `Directory.Packages.props` (central package management).
- **Formatting:** `.editorconfig` + `dotnet format --verify-no-changes` in CI; Prettier + ESLint on the front-end.

### API changes (endpoints, DTOs)
- `GET /api/health` → `{ status, database }` (ASP.NET Core health checks with an Npgsql check).
- OpenAPI document at `/openapi/v1.json` (built-in `Microsoft.AspNetCore.OpenApi`), Scalar UI in Development only.

### Front-end changes (pages, components)
- Vite + React + TypeScript (strict), React Router, TanStack Query, Tailwind CSS v4, shadcn/ui.
- Folder layout per INIT.md: `src/pages`, `src/components`, `src/api` (generated client, not edited by hand),
  plus `src/lib` for helpers such as `formatCents()`.
- API client generated from the OpenAPI document with **openapi-typescript + openapi-fetch**
  (`npm run api:generate`), wrapped in TanStack Query hooks per feature. The generated file is committed
  so the front-end builds without a running API.
- App shell: responsive layout (bottom navigation on phone, sidebar on desktop) with routes for
  Dashboard, Month, Cards, Loans, Savings and Settings as placeholders. Dashboard shows API/database status.
- Tests with Vitest + React Testing Library.

### Database migrations
- `docker-compose.yml` with `postgres:17` (named volume, healthcheck, credentials from `.env`, `.env.example` committed).
- Connection string read from configuration (`ConnectionStrings:Monthwise`), user secrets / environment variables locally.
- Initial empty migration `InitialCreate` to prove the migration pipeline; the API applies migrations
  automatically on startup in Development only. Real tables come with INIT.md step 2.

### CI (GitHub Actions)
- `.github/workflows/ci.yml`, two jobs on push and pull request:
  - **backend:** `dotnet restore`, `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`
    (API tests use a PostgreSQL service container).
  - **frontend:** `npm ci`, `npm run lint`, `npm run typecheck`, `npm run test`, `npm run build`.

### Decisions to record (ADRs in `docs/decisions/`)
1. `0001-layered-backend.md`: Domain/Application/Infrastructure/Api, minimal APIs, no MediatR.
2. `0002-money-as-integer-cents.md`: `Money` value object, `bigint` cents, no floating point.
3. `0003-openapi-generated-client.md`: OpenAPI as the contract, generated TypeScript client.
4. `0004-same-origin-dev-proxy.md`: Vite proxy in development, cookie auth planned without CORS.

## Steps
1. Add this plan (first commit).
2. Add root files: `docker-compose.yml`, `.env.example`, `.editorconfig`, extended `.gitignore` (bin/obj, dist).
3. Create the .NET solution: `Directory.Build.props`, `Directory.Packages.props`, the four `src` projects
   with project references per the layering table, and the two test projects.
4. Add `Money` and `YearMonth` value objects in Domain with unit tests (the only domain code in this plan,
   because every later feature depends on them).
5. Wire Infrastructure: `MonthwiseDbContext`, Npgsql, snake_case naming, `InitialCreate` migration.
6. Wire Api: DI composition, ProblemDetails, health checks, OpenAPI + Scalar, `/api` route group, dev-only migrate on startup.
7. Add the layer dependency test and an API integration test for `/api/health` (WebApplicationFactory + PostgreSQL).
8. Create the front-end with Vite, add Tailwind, shadcn/ui, React Router, TanStack Query, ESLint, Prettier, Vitest.
9. Add the app shell, placeholder pages, the Vite `/api` proxy and the generated API client with the health status widget.
10. Add the GitHub Actions CI workflow.
11. Write the four ADRs and update README.md with "Getting started" (prerequisites and run commands).
12. Set this plan's status to `Done`.

## Tests
- Unit tests: `Money` (addition, subtraction, negation, no implicit float conversions, formatting not in Domain),
  `YearMonth` (next/previous month, months between two periods, which the installment logic will need);
  Vitest test for `formatCents()` and a render test of the app shell.
- API / integration tests: `GET /api/health` returns healthy against a real PostgreSQL; layer dependency rules test.
- Manual checks: `docker compose up -d`, `dotnet run --project backend/src/Monthwise.Api`, `npm run dev` in
  `/frontend`; open the app on desktop and on a phone-width viewport and see the database status as healthy;
  stop PostgreSQL and see the status turn unhealthy.

## Open questions
- PostgreSQL in API integration tests: CI service container (planned default) or Testcontainers (self-contained, but needs Docker locally)?
- Node.js version to pin for the front-end (planned default: current LTS, Node 24, via `.nvmrc`).
- Should the first CI also run on a schedule or only on push and pull request (planned default: push and pull request only)?
