# 0004: Same origin in development via the Vite proxy

- **Status:** Accepted
- **Date:** 2026-10-03

## Context
Authentication will use ASP.NET Core Identity with cookies. Cookies across origins need CORS with
credentials and `SameSite` exceptions, which add configuration and risk.

## Decision
- The front-end calls relative `/api/...` URLs.
- In development the Vite dev server (`:5173`) proxies `/api` to the API (`:5080`), so the browser
  sees one origin. `MONTHWISE_API_URL` overrides the target.
- No CORS policy is configured in the API.
- How production serves both from one origin (API serving the built SPA, or a reverse proxy) is
  decided together with hosting.

## Consequences
- Cookie auth works in development without CORS.
- Opening the API port directly in the browser is only for tools such as Scalar (`/scalar`).
