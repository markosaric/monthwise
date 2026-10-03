# Monthwise

Personal home finance app for tracking monthly income and expenses, credit card and loan installments, card statement reconciliation and savings.

Starting document with the domain, data model and architecture: [docs/INIT.md](docs/INIT.md).
Architecture decisions: [docs/decisions](docs/decisions/README.md). Working practices: [CONTRIBUTING.md](CONTRIBUTING.md).

## Initial development setup

Do this once on a new machine. Never commit secrets: see [Secrets and sensitive data](CONTRIBUTING.md#secrets-and-sensitive-data).

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (see `backend/global.json`); check with `dotnet --list-sdks`
- [Node.js](https://nodejs.org/) 22.12 or newer (CI uses the version in `.nvmrc`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or OrbStack) for the local PostgreSQL database
- [Visual Studio Code](https://code.visualstudio.com/) (optional, recommended)

### 1. Start Docker

Start Docker Desktop and finish its first-run setup. When it is running, a new terminal should answer:

```bash
docker --version
```

### 2. Create `.env` and choose a database password

```bash
cp .env.example .env
```

Edit `.env` and replace `POSTGRES_PASSWORD=change-me` with your own password. `.env` is git-ignored; never commit it.

### 3. Start PostgreSQL

```bash
docker compose up -d
docker compose ps        # monthwise-db should be "healthy"
```

PostgreSQL keeps running in the background. Stop it with `docker compose stop`; data stays in the
`monthwise-db-data` Docker volume.

### 4. Give the API the database password

`appsettings.Development.json` contains the connection string without a password. Store the full
connection string once in .NET user secrets (kept outside the repository, in your user profile), using
the password from `.env`:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Monthwise" \
  "Host=localhost;Port=5432;Database=monthwise;Username=monthwise;Password=<POSTGRES_PASSWORD>" \
  --project src/Monthwise.Api
```

### 5. Install front-end packages

```bash
cd frontend
npm install
```

### 6. Run the app

**In VS Code:** open the `monthwise` folder (`code .`), install the recommended extensions when asked
(most importantly C# Dev Kit), open Run and Debug (Cmd+Shift+D / Ctrl+Shift+D), select
**Monthwise (API + front-end)** and press F5. This starts the API on http://localhost:5080, the Vite dev
server on http://localhost:5173 and opens Chrome, with breakpoints working in C# and React. The
configurations **API** and **Front-end (Chrome)** start each part on its own.

**From the terminal:** in two terminals run

```bash
cd backend && dotnet run --project src/Monthwise.Api
```

```bash
cd frontend && npm run dev
```

and open http://localhost:5173.

In Development the API applies database migrations on startup, and Vite forwards `/api` to the API.
The dashboard should show **API: Reachable** and **Database: Healthy**. Interactive API docs:
http://localhost:5080/scalar.

### 7. Run the tests (optional)

```bash
cd backend
MONTHWISE_TEST_DB="Host=localhost;Port=5432;Database=monthwise;Username=monthwise;Password=<POSTGRES_PASSWORD>" dotnet test
```

```bash
cd frontend && npm test
```

Without `MONTHWISE_TEST_DB` the one back-end test that needs a real database is skipped.

### Troubleshooting

- **`dotnet --list-sdks` is empty or `dotnet build` says .NET 10 is missing:** another `dotnet` comes
  first in your `PATH` (for example `~/.dotnet` added by an editor extension in `~/.zshrc`). Check
  `which dotnet`, remove the extra entry from your shell profile, then quit and reopen the terminal app.
- **Database: Unhealthy on the dashboard:** check `docker compose ps` and that the password in user
  secrets matches `.env`.

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
