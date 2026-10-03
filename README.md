# Monthwise

Personal home finance app for tracking monthly income and expenses, credit card and loan installments, card statement reconciliation and savings.

Starting document with the domain, data model and architecture: [docs/INIT.md](docs/INIT.md).
Architecture decisions: [docs/decisions](docs/decisions/README.md). Working practices: [CONTRIBUTING.md](CONTRIBUTING.md).

## Getting started

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (see `backend/global.json`)
- [Node.js](https://nodejs.org/) 22.12 or newer (CI uses the version in `.nvmrc`)
- Docker (for the local PostgreSQL database)

### 1. Database

```bash
cp .env.example .env      # set POSTGRES_PASSWORD
docker compose up -d
```

### 2. API (http://localhost:5080)

The connection string has no password in `appsettings.Development.json`; store the full one in user secrets once:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Monthwise" \
  "Host=localhost;Port=5432;Database=monthwise;Username=monthwise;Password=<POSTGRES_PASSWORD>" \
  --project src/Monthwise.Api
dotnet run --project src/Monthwise.Api
```

In Development the API applies migrations on startup. Interactive API docs: http://localhost:5080/scalar.

### 3. Front-end (http://localhost:5173)

```bash
cd frontend
npm install
npm run dev
```

Vite forwards `/api` to the API. The dashboard shows whether the API and the database are reachable.

## Everyday commands

| Where | Command | What it does |
|---|---|---|
| `backend` | `dotnet test` | domain and API tests (set `MONTHWISE_TEST_DB` to a connection string to include tests against a real database) |
| `backend` | `dotnet format` | apply code style |
| `backend` | `dotnet tool restore && dotnet ef migrations add <Name> --project src/Monthwise.Infrastructure --output-dir Persistence/Migrations` | add a migration |
| `frontend` | `npm test` / `npm run lint` / `npm run typecheck` / `npm run format` | tests, lint, types, formatting |
| `frontend` | `npm run api:generate` | regenerate the TypeScript API types after building the API (`backend/openapi/monthwise.json`) |

## Repository layout

```
backend/    .NET 10 solution: Domain, Application, Infrastructure, Api (+ tests)
frontend/   React + TypeScript app (Vite)
docs/       INIT.md, feature plans, architecture decisions
```
