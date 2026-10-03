# 0002: Money as integer cents

- **Status:** Accepted
- **Date:** 2026-10-03

## Context
Budgets, installments and card statements must add up exactly. Floating point (`double`, JavaScript
`number` with fractions) introduces rounding errors; `decimal` is exact in .NET but not in TypeScript.

## Decision
- All amounts are EUR and stored as whole cents.
- Domain: the `Money` value object wraps `long Cents`; arithmetic is checked for overflow and the type
  has no floating point API.
- Database: `bigint` columns named `*_cents`, via an EF Core convention for `Money`.
- API and front-end: amounts travel as integer cents (`amountCents`); conversion to a display string
  happens only in `formatCents()` in the UI.
- Shares (for example 50 %) and installment splits round explicitly in Domain code, with tests,
  when those features are built.

## Consequences
- Sums are exact everywhere, including in TypeScript, where cents stay safe integers.
- Input fields convert typed euros to cents at the edge of the UI.
- Multi-currency would need a currency on `Money`; it is a non-goal for now.
