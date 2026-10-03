# Monthwise — home finance (INIT)

> App name: **Monthwise** (chosen 2026-10-03). Repository: `monthwise` (private).
> This document is the starting point of the repository: goal, domain, data model, architecture and plan for the first steps.
> It is based on an analysis of the existing Numbers workbook `Budget_MMYYYY.numbers` (one file = one month).

## 1. Goal

Replace the manual monthly budget kept in Numbers with an app that:

- records **incoming** and **outgoing** cash flows per month,
- automatically calculates **installments** for card purchases and loans (how much is paid, how much remains),
- **reconciles** the calculated card statement amount with the actual bank statement,
- shows the **available budget** (calculated and actual) and the **carryover** into the next month,
- gives an overview of **debt** (short-term: cards; long-term: loans, leasing),
- tracks **savings** (account balances over time),
- works on both phone and desktop.

Non-goals for the MVP: automatic bank statement import, multiple households, investments.

## 2. Domain (how it is organized in Numbers)

### Income (IN)
| Category | Examples | Notes |
|---|---|---|
| Salary | base salary | fixed, monthly |
| Salary add-ons | meal allowance, commuting, work expenses | fixed or variable |
| Agreed contributions (cost sharing participants) | other people's share of shared costs | tied to shared expenses |
| Other income | occasional inflows | ad hoc |

Each item has a **"received"** checkbox (Check).

### Expenses (OUT)
| Category | Examples | Characteristics |
|---|---|---|
| Utilities – shared | internet/TV, water/waste, electricity, building reserve, gas | due day of month, payment date, **50 % share** |
| Utilities – private | bank account fee, pension fund | due day, payment date |
| Variable expenses | mobile phone, haircut, groceries, kindergarten | date + amount, list |
| Loan payments | mortgage + insurance, loan I, loan II, leasing | due day, can be shared (50 %) |
| Card expenses (short-term debt) | installment purchases (Diners, Mastercard, VISA) | installment X of N, installment amount, remaining amount |
| Card payments | monthly card statement | calculated vs. actual amount, difference |
| Other expenses | subscriptions, one-off card purchases | |

### Monthly summary
- Total income, total expenses
- Carryover from the previous month (ADDITIONAL FUND)
- Available budget: **calculated** and **actual** (Left Over)
- Break-even income (minimum income needed to cover obligations)
- Debt: remaining amount per card and loan

## 3. Data model (first draft)

Amounts are stored as **integer cents** (`amount_cents`), currency EUR.

```
household            id, name, currency
account              id, household_id, name, type (current|fx|savings|card|loan), issuer
period               id, household_id, year, month, opening_carryover_cents, closed_at
category             id, household_id, direction (in|out), group, name
                     group ∈ salary | salary_addon | shared_contribution | other_in |
                             utility_shared | utility_private | variable | loan | card_installment |
                             card_payment | other_out
recurring_item       id, category_id, name, default_amount_cents, due_day, share_pct (e.g. 50), account_id, active
transaction          id, period_id, category_id, recurring_item_id?, account_id?, description,
                     amount_cents, due_date?, paid_date?, received/paid flag, share_pct
installment_plan     id, account_id (card), merchant, purchase_date, total_amount_cents,
                     installments_total, installment_amount_cents, first_period
                     (current installment and remaining amount are CALCULATED, not entered)
loan                 id, account_id, name, principal_cents?, monthly_payment_cents, due_day, share_pct, end_date?
card_statement       id, account_id, period_id, calculated_cents, actual_cents  (difference = actual - calculated)
balance_snapshot     id, account_id, date, balance_cents   (savings / account balances)
user                 id, email, household_id, role
```

Key rules:
- **A month is generated** from `recurring_item` + active `installment_plan` and `loan` records; the user only confirms or corrects.
- Installment `k` of `N` = number of months since `first_period` + 1; the plan is finished when `k > N`.
- Calculated card statement = sum of active installments + one-off charges on that card in the period.
- Available (calculated) = carryover + Σ income − Σ expenses; actual = entered account balance.
- Closing a month carries the actual balance over as the next month's `opening_carryover`.

## 4. Architecture

| Layer | Choice | Why |
|---|---|---|
| Front-end | **React + TypeScript** (Vite), React Router, TanStack Query, Tailwind CSS + shadcn/ui | fast dev setup, typed API calls with caching, ready-made components for tables and forms; responsive so it works on phone and desktop |
| Back-end | **.NET 10 (LTS) – ASP.NET Core Web API**, minimal APIs or controllers, FluentValidation, OpenAPI | strongly typed, mature, good performance; OpenAPI spec is used to generate the TypeScript client for the front-end |
| Data access | **Entity Framework Core** + Npgsql, code-first migrations | schema versioned in code; `decimal`/cents mapping kept exact |
| Database | **PostgreSQL** | relational data (months, installments, accounts) and exact calculations |
| Authentication | ASP.NET Core Identity (cookie auth) | one household, 1–2 users, no external provider needed |
| Tests | xUnit (domain + API), Vitest (front-end) | calculation logic (installments, monthly summary, card reconciliation) is covered by unit tests |
| Charts | Recharts | income vs. expense trend, debt over time |
| Local dev | Docker Compose for PostgreSQL | one command to start the database |

Back-end layering: **Domain** (entities and pure calculation logic, no dependencies) → **Application** (use cases, DTOs, validation) → **Infrastructure** (EF Core, PostgreSQL) → **Api** (endpoints, auth).

Hosting and importing history from Numbers are out of scope for now.

### Repository structure

```
/frontend                        React app (Vite)
  /src/pages                     dashboard, month, cards, loans, savings, settings
  /src/components                UI components
  /src/api                       generated API client
/backend
  /src/Monthwise.Domain          entities, installment and summary logic
  /src/Monthwise.Application     use cases, DTOs, validation
  /src/Monthwise.Infrastructure  EF Core DbContext, migrations, repositories
  /src/Monthwise.Api             ASP.NET Core Web API
  /tests/Monthwise.Domain.Tests
  /tests/Monthwise.Api.Tests
  Monthwise.sln
/docs                            INIT.md, decisions (ADR)
docker-compose.yml               PostgreSQL for local development
```

## 5. MVP screens

1. **Dashboard** – available budget (calculated/actual), income vs. expenses, upcoming due dates, total debt.
2. **Month** – Income and Expenses tables by category, paid/received checkboxes, carryover, month closing.
3. **Cards** – installments per card (X/N, remaining), one-off charges, statement reconciliation.
4. **Loans and leasing** – monthly payment, due date, share.
5. **Savings** – account balances over time.
6. **Settings** – categories, recurring items, accounts/cards, shares.

## 6. Plan

1. Initial setup: name, private repo, this document, solution skeleton (React + .NET + PostgreSQL in Docker) + CI (build, lint, test).
2. Database schema (EF Core) + domain logic (installments, summary) with tests.
3. API endpoints for periods, transactions, cards and installments.
4. Month and Cards screens.
5. Dashboard, loans, savings.
