# 0001: Layered back-end with minimal APIs, no MediatR

- **Status:** Accepted
- **Date:** 2026-10-03

## Context
Monthwise has real calculation rules (installments, monthly summary, card reconciliation) that must
be easy to test, but it is a small app for one household with one or two users.

## Decision
- Four projects with a one-way dependency direction:
  `Domain` ← `Application` ← `Infrastructure` ← `Api`.
  - **Domain:** entities, value objects and pure calculation logic; no package references.
  - **Application:** use cases as plain handler classes, DTOs, FluentValidation validators,
    persistence interfaces.
  - **Infrastructure:** EF Core `DbContext`, configurations, migrations, implementations of
    Application interfaces.
  - **Api:** minimal API endpoint groups per feature, DI composition, auth, OpenAPI, ProblemDetails.
- Endpoints stay thin: bind the request, call a handler, map the result.
- No MediatR or other in-process messaging library; handlers are registered in DI and injected directly.
- The dependency direction is enforced by `ArchitectureTests` in `Monthwise.Api.Tests`.

## Consequences
- Calculation logic is testable without a database or web host.
- Less indirection than a mediator pipeline; cross-cutting behaviour (validation, logging) is added
  explicitly in handlers or endpoint filters when needed.
- `Api` references `Infrastructure` only for DI registration.
