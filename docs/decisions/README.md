# Architecture decision records

Short records of decisions that shape the code base. Add a new numbered file for each decision;
when a decision changes, add a new record that supersedes the old one instead of rewriting it.

| # | Decision | Status |
|---|---|---|
| [0001](0001-layered-backend.md) | Layered back-end with minimal APIs, no MediatR | Accepted |
| [0002](0002-money-as-integer-cents.md) | Money as integer cents | Accepted |
| [0003](0003-openapi-generated-client.md) | OpenAPI as the contract, generated TypeScript client | Accepted |
| [0004](0004-same-origin-dev-proxy.md) | Same origin in development via the Vite proxy | Accepted |
| [0005](0005-encrypted-sensitive-data.md) | Card numbers and IBANs encrypted in the application | Accepted |
