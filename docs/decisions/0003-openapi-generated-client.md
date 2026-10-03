# 0003: OpenAPI as the contract, generated TypeScript client

- **Status:** Accepted
- **Date:** 2026-10-03

## Context
The front-end and back-end are written in different languages. Hand-written TypeScript types for
API requests and responses drift from the C# DTOs.

## Decision
- The API describes itself with the built-in `Microsoft.AspNetCore.OpenApi` document.
- On every build the document is written to `backend/openapi/monthwise.json`
  (`Microsoft.Extensions.ApiDescription.Server`) and committed.
- The front-end generates `src/api/schema.d.ts` from that file with `openapi-typescript`
  (`npm run api:generate`) and calls the API through the typed `openapi-fetch` client, wrapped in
  TanStack Query hooks per feature.
- CI fails when either generated file is out of date.

## Consequences
- A change to a DTO shows up as a TypeScript compile error in the front-end.
- The front-end builds without a running API.
- Endpoints need accurate metadata (`Produces<T>`, typed results) so the document is complete.
