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
account              id, household_id, name, type (current|fx|savings|loan), issuer
card                 id, name, issuer_id, bank_id, card_number (encrypted)?, iban (encrypted)?,
                     card_number_last4, iban_country, iban_last4, valid_from?, valid_through,
                     type (Debit|Charge|Credit|Prepaid), is_primary, allows_installments (Debit only),
                     debt_limit_cents?,
                     blocked_on?, expired_on?, archived_on?, replaced_by_card_id?,
                     paying_account_id → card?, pays_from_account_directly, max_installments? (12|24|36)
                     (payment cards and current accounts; see docs/feature-requirements/credit-card-settings.md;
                     billing details only for cards that allow installments)
card_billing_terms   id, card_id, from_month, statement_day (1–30), payment_day (2–31, > statement_day)
                     (every version of a card's billing days; a change applies from the next month)
card_issuer          id, name, is_predefined   (Visa, Mastercard, … extendable by the user)
bank                 id, name, is_predefined   (ZaBa, PBZ, … extendable by the user)
payer                id, name, removed_on?   (saved automatically when an input names it)
input_category       id, name, is_predefined, hidden_on?   (Salary, Salary add-ons, … extendable)
cash_flow_input      id, payer_id, category_id, account_id → card, amount_cents, issue_date,
                     expected_date, received_date?, description?, recurring_input_id?, occurrence_date?
                     (incoming cash flow; see docs/feature-requirements/input-cash-flow.md)
recurring_input      id, payer_id, category_id, account_id → card, amount_cents, description?,
                     frequency (Weekly|Monthly|Quarterly|Yearly), issue_anchor, expected_anchor, end_date?
                     (occurrences are CALCULATED; one is stored as cash_flow_input only when changed or received)
skipped_occurrence   recurring_input_id, occurrence_date   (a deleted occurrence)
balance_entry        id, account_id → card, kind (Starting|Correction), date, balance_cents, note?
                     (balance at the end of that date; account balance = latest entry + inputs received
                     − loan and card payments, bills and expenses paid from it after it)
period               id, household_id, year, month, opening_carryover_cents, closed_at
expense_category     id, name, is_predefined, is_fixed, hidden_on?
                     (one list for card purchases, bills and expenses: Electricity, Groceries, … extendable;
                     Cash withdrawal is fixed; see docs/feature-requirements/monthly-bills-and-expenses.md)
recurring_bill       id, name, category_id → expense_category, payee?, paid_from_id → card?, description?,
                     stopped_on?   (a monthly bill without dates or amount)
bill                 id, recurring_bill_id?, month, name, category_id → expense_category, payee?, amount_cents
                     (0 while not issued), issue_date?, due_date?, is_paid, paid_date?, paid_from_id → card?,
                     description?   (paid with a Credit or Charge card it is a line of that card's card payment)
expense              id, date, amount_cents, category_id → expense_category, paid_from_id → card, description?
                     (one-time and always paid; Cash withdrawal from a primary account; paid with a Credit or
                     Charge card it is a line of that card's card payment)
card_purchase        id, card_id, description, purchase_date, amount_cents, installment_count (1 = paid in full),
                     postpone_first, category_id → expense_category?, reversed_on?, reversal_reason?
                     (installments, card payments and remaining amounts are CALCULATED, not stored;
                     see docs/feature-requirements/credit-card-installments.md)
card_purchase_reversal id, purchase_id, installment_number, reversed_on, reason?   (one reversed installment)
card_payment         id, card_id, month, is_paid? (null = automatic), paid_date?, paid_account_id → card?
                     (card payments are CALCULATED per billing period from purchases, installments, credits and
                     bills and expenses paid with the card; one is stored only when ticked or unticked)
loan                 id, name, category_id → loan_category, subcategory_id → loan_subcategory,
                     lender_id → bank, payment_method (StandingOrder|Bill), account_id → card (standing order only),
                     start_month, payment_day (1–31), monthly_payment_cents, payment_count, residual_value_cents?,
                     description?, closed_on?
                     (bank loans and leases; see docs/feature-requirements/loans-and-leases.md)
loan_payment_change  id, loan_id, from_month, monthly_payment_cents?, payment_day?, note?
                     (the newest change that applies to a month decides its amount and payment day)
loan_payment         id, loan_id, month, kind (Regular|Residual), amount_cents?, due_date?, is_changed,
                     is_paid? (null = automatic), paid_date?, paid_account_id → card?
                     (payments are CALCULATED; one is stored only when changed or paid)
loan_category        id, name, allows_residual_value, is_predefined, hidden_on?   (Bank loan, Lease, … extendable)
loan_subcategory     id, category_id → loan_category, name, is_predefined, hidden_on?
                     (Mortgage, Cash loan, Car lease, … extendable)
balance_snapshot     id, account_id, date, balance_cents   (savings balances; card and current account
                     balances are balance_entry)
user                 id, email, household_id, role
```

Key rules:
- **A month is generated** from `recurring_item`, `card_purchase` and `loan` records; the user only confirms or corrects.
- A card payment is the sum of the installments and purchases paid in full in the card's billing period (from the day
  after the previous statement date up to and including the statement date), minus credits from reversed installments
  that were already charged.
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
