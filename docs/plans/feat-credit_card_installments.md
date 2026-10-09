# Plan: Credit card installments

- **Branch:** `feat/credit_card_installments`
- **Status:** In progress
- **Date:** 2026-10-08
- **Requirements:** [credit-card-installments.md](../feature-requirements/credit-card-installments.md) (approved), and
  [credit-card-settings.md, Appendix 2](../feature-requirements/credit-card-settings.md#appendix-2-billing-dates-and-paying-account)

## Goal
Every card that allows monthly installments gets a statement date, a payment date, a paying account,
a **Connected to the paying account** check box and a maximum of 12, 24 or 36 installments. The user records card purchases, paid in full
or in installments, on the **Cards** screen, and can reverse, edit or delete them. Each month's card
payment is calculated from the purchases in its billing period. It appears on the card, on the Month
screen, on the Cash flow dashboard and in the monthly report, with a drill-down to its purchases. It
is paid automatically when the card is connected to the paying account, and otherwise when the user
ticks it. Card payments lower the account balances the same way loan payments do.

## Requirement changes
- [credit-card-settings.md, Appendix 2](../feature-requirements/credit-card-settings.md#appendix-2-billing-dates-and-paying-account)
  (2026-10-08, changes section 3, FR-1 to FR-4 of the card settings):
  - Cards that allow monthly installments (A11) get five new attributes: **Statement date**,
    **Payment date**, **Paying account** (a primary account, prefilled when exactly one is
    primary), **Connected to the paying account** and **Max installments** (12, 24 or 36).
  - They are set in the Add and Edit card forms and shown in the Settings list and on the card
    details. Their rules are in credit-card-installments.md FR-1 and FR-2.
  - Implementation: see "Billing details on the card" and the card form below.

## Scope
- In scope:
  - Domain: billing details on the card with their history, billing periods, card purchases with
    installments, reversals, card payments, purchase categories, and card payments in the balance.
  - Database tables and one migration.
  - API endpoints for purchases, reversals, card payments, purchase categories, the extended card
    form, and the extended balances, chart and report.
  - Settings: billing details in the card form, list and details; **Purchase categories** section.
  - **Cards** screen (today a placeholder): cards with their card payments, purchases, the purchase
    form, reversal, and the card payment drill-down.
  - **Month** screen: card payments in the **Expenses** part, with the **Paid** check box.
  - Cash flow dashboard and monthly report with card payments as outputs.
  - Tests at every layer.
- Out of scope:
  - Everything in section 5 of the requirements: fees and interest, partial reversals, undoing a
    reversal, purchases on cards without installments, other outputs, card limits, bank imports,
    multiple currencies.
  - The Dashboard **Cards** widget does not show card usage yet (card settings FR-12).
  - **Access control** (NFR-3): there is still no authentication, as for cards, inputs and loans.
  - Households: one implicit household.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| FR-1, B1, B2 | `StatementDay` 1 to 30, `PaymentDay` 2 to 31, `PaymentDay > StatementDay`. Both are clamped to the last day of a shorter month. The payment date is in the same month as the statement. |
| FR-1 weekend rule | The payment date uses `DueDateRule.DueDate(month, paymentDay, PaymentMethod.Bill)` from loans: Saturday or Sunday moves to Monday unless that Monday is in the next month. The statement date never moves. |
| FR-1, B3 | `PayingAccountId` must be a usable primary account (`LoanAccount.CanBeChosen`) when the card is saved. The API prefills nothing; the form prefills the only primary account. |
| FR-1, B4 | `PaysFromAccountDirectly` (bool, default false). |
| FR-1, B5 | `MaxInstallments` is 12, 24 or 36. Lowering it does not change purchases already recorded. |
| FR-1 | The five attributes are required when `Card.AllowsInstallments`, and must be empty otherwise. Unticking *Allows monthly installments* on a Debit card clears them. A Debit card with installments may be its own paying account when it is primary. |
| FR-2 | Billing days are kept as a history of **billing terms** (`FromMonth`, `StatementDay`, `PaymentDay`). Changing the days in the card form adds or replaces the terms from **the next month** (today's month + 1). The current and earlier months keep theirs. |
| Billing period | The billing period of payment month *m* runs from the day after the statement date of *m − 1* (under the terms of *m − 1*) up to and including the statement date of *m* (under the terms of *m*). So a change of days never leaves a gap or an overlap. |
| FR-3, P1 | A purchase needs a card with `AllowsInstallments`, billing terms and a paying account, usable (`CanBeUsedOn`) on the purchase date and today. |
| FR-3, P2 to P8 | Description 1 to 100 characters; amount > 0 (`Money`); `InstallmentCount` is 1 for a purchase paid in full, otherwise 2 to the card's `MaxInstallments` (checked when the purchase is saved); `PostponeFirst` only with installments; optional `CategoryId`. |
| FR-4 | First installment month = the payment month whose billing period contains the purchase date, plus one with `PostponeFirst`. Installment = amount / count, rounded down to the cent; the last installment gets the remaining cents. |
| FR-5 | A reversal has a date (today), an optional reason (max 200) and covers the whole purchase or one installment. An installment whose card payment date is on or after the reversal date is **removed**. One whose payment date is before the reversal date is **credited**: a negative line in the card payment of the billing period that contains the reversal date. A reversed purchase or installment cannot be reversed again; a reversed purchase cannot be edited. |
| FR-6 | Edit recalculates (installments are not stored). Delete removes the purchase, its reversals and so its credits. The API returns `hasChargedInstallments` so the form can warn first. |
| FR-8 | Card payment amount = installments + purchases in full − credits in its billing period. A card payment with no lines is not shown. |
| FR-8, B4 | Connected: paid automatically at the end of its payment date, from the paying account, unless the user changed its paid state, or the paying account stopped being usable before (`LoanAccount.ChargesStopOn`). Not connected: paid only when the user ticks it, with today and the account (prefilled with the paying account). |
| FR-9 | Paid card payments subtract from the account they were paid from; expected card payments due by the end of the month subtract from the paying account's expected balance. A card payment of a card whose paying account is no longer usable carries `needsAccount` and is not counted until the user chooses an account. |
| FR-10 | Purchase categories: name 1 to 50, unique case-insensitive, no predefined entries. A used category can only be hidden; an unused one can be deleted. |
| Card settings, ongoing transaction | A card with installments left is an ongoing transaction of that card, so a blocked or expired card stays visible while installments are still to be charged. |

### Billing details on the card (Appendix 2)
- `Card` gets `PayingAccountId?`, `PaysFromAccountDirectly`, `MaxInstallments?` and a list of
  `CardBillingTerms`. `CardDetails` gets `StatementDay?`, `PaymentDay?`, `PayingAccountId?`,
  `PaysFromAccountDirectly` and `MaxInstallments?`.
- `Card.Validate` adds the FR-1 rules. Whether the paying account is a usable primary account is
  checked in the handler, which loads it, like the loan's account.
- **Existing cards:** Credit, Charge and Debit-with-installments cards saved before this feature
  have no billing details. They keep working. The card details and the Cards screen show a
  **Set billing details** prompt, and purchases cannot be recorded on them until it is done. Their
  next edit requires the fields.
- `CardBillingTerms` (`FromMonth`, `StatementDay`, `PaymentDay`): the first one gets
  `FromMonth = YearMonth.MinValue`, so it covers every earlier purchase. `Card.ChangeBillingDays`
  adds or replaces the terms from next month, as in the table above.

### Billing calendar and card payments
`CardBillingCalendar` (Domain, pure): built from the card's billing terms.
- `StatementDate(m)`, `PaymentDate(m)`, `PeriodOf(m)` (first and last day) and
  `PaymentMonthOf(date)`, the payment month whose period contains the date.

`CardPurchase.Installments(calendar)` returns each installment's number, total, amount and payment
month. `CardLedgerCalculator` (Domain, pure) takes the card, the calendar, the purchases, the
reversals and the stored card payments, and returns:
- **lines** per payment month: `Installment` and `FullPurchase` lines, and `Credit` lines from
  reversals;
- **card payments** (`ScheduledCardPayment`): card, month, statement date, payment date, period,
  amount, paid state, paid date, account, `IsOverdue`, `NeedsAccount`, `PaidAutomatically`;
- per purchase: charged installments, remaining amount, `IsReversed`.

Card payments are **calculated**. One is stored (`card_payment`) only when the user ticks, unticks
or corrects its paid state, the same pattern as `loan_payment`. Its `is_paid` is nullable: `null`
means automatic.

A `CardBook` and `CardPaymentLedger` (Application) load all of this once per request. They are the
card equivalents of `LoanBook` and `LoanLedger`. `CardActivityQuery` combines the loans' ongoing
standing orders with cards that have installments left.

### Balance, chart and report (FR-8, FR-9)
- `AccountBalanceQuery` adds paid card payments as negative `BalanceMovement`s. It also adds
  expected card payments due by the end of the month (overdue ones included) as negative expected
  movements on the paying account.
- Chart: `ChartBucketResponse` output series include card payments. The loan category filter
  applies to loan payments only. A new `cardId` filter applies to card payments only.
- Report: `ReportOutputsResponse` gets `ByCard` (card payments per card, linking to the drill-down).
  Card payments count in the output totals, in `ByAccount` and in the month result. They appear in
  `ByCategory` as one **Card payments** group. Purchase categories show in the drill-down, not in
  the report.
- `docs/INIT.md` section 3: replace the draft `installment_plan` and `card_statement` lines with the
  tables below, and add the new card columns.

### Domain / data model changes
- `Monthwise.Domain/Cards`:
  - `Card`: the new attributes and `ChangeBillingDays`;
  - `CardBillingTerms`;
  - `CardBillingCalendar`;
  - new `CardErrors` with field keys.
- `Monthwise.Domain/CardPurchases`:
  - `CardPurchase`: `Create`, `Update`, `Reverse`, `ReverseInstallment`, `Installments`;
  - `CardPurchaseReversal`;
  - `PurchaseCategory`: `Rename`, `Hide`, `Show`;
  - `CardPayment` (the stored payment), `CardPaymentLine` and `ScheduledCardPayment`;
  - `CardLedgerCalculator`;
  - `CardPurchaseErrors`.
- Entities use Guid v7 ids, `CreatedAt` and `UpdatedAt`. Rules return `Result` errors with field
  keys, as for cards, inputs and loans.

### Database migrations
- Migration `AddCardPurchases` (snake_case):
  - `card`: add `paying_account_id` → `card` (nullable), `pays_from_account_directly` (bool,
    default false), `max_installments` (nullable).
  - `card_billing_terms` (`card_id`, `from_month`, `statement_day`, `payment_day`), unique on
    (`card_id`, `from_month`).
  - `purchase_category` (`name`, `hidden_on`), unique index on `lower(name)`.
  - `card_purchase` (`card_id`, `description`, `purchase_date`, `amount_cents`,
    `installment_count`, `postpone_first`, `category_id` → `purchase_category`, `reversed_on`,
    `reversal_reason`), index on (`card_id`, `purchase_date`).
  - `card_purchase_reversal` (`purchase_id`, `installment_number`, `reversed_on`, `reason`), unique
    on (`purchase_id`, `installment_number`), for single installments only. A whole-purchase
    reversal is on `card_purchase`.
  - `card_payment` (`card_id`, `month`, `is_paid` nullable, `paid_date`, `paid_account_id` →
    `card`), unique on (`card_id`, `month`).
- Check constraints:
  - `statement_day between 1 and 30` and `payment_day between 2 and 31` and
    `payment_day > statement_day`;
  - `max_installments in (12, 24, 36)`;
  - `amount_cents > 0` and `installment_count between 1 and 36`;
  - `not postpone_first or installment_count > 1`;
  - `(is_paid is true) = (paid_date is not null)`, the same for `paid_account_id`.
- Foreign keys restrict deletes. Reversals are deleted together with their purchase.

### API changes (endpoints, DTOs)
New `Api/Endpoints/CardPurchaseEndpoints.cs` and `Monthwise.Application/CardPurchases` handlers,
FluentValidation validators and the same error mapping as before.

| Method and path | Purpose | Notes |
|---|---|---|
| `POST /api/cards`, `PUT /api/cards/{id}`, `GET /api/cards…` (extended) | Billing details (FR-1, FR-2, Appendix 2) | `statementDay`, `paymentDay`, `payingAccount`, `paysFromAccountDirectly`, `maxInstallments`, `needsBillingDetails`; terms history on details |
| `GET /api/card-purchases/cards` | Cards screen overview (FR-7) | cards that allow installments, with the next card payment, remaining installments total and `needsBillingDetails` / `needsAccount` |
| `GET /api/cards/{id}/purchases?show=Active\|All` | Purchases of a card (FR-7) | charged and total installments, remaining amount, reversed marker |
| `GET /api/cards/{id}/card-payments?from=yyyy-MM&to=yyyy-MM` | Card payments by month (FR-7) | statement and payment date, period, amount, paid state |
| `GET /api/cards/{id}/card-payments/{month}` | Drill-down (FR-8) | the card payment and its lines: description, purchase date, installment `3 of 12`, category, amount, credit marker, purchase id |
| `POST /api/card-purchases`, `GET /api/card-purchases/{id}`, `PUT /api/card-purchases/{id}`, `DELETE /api/card-purchases/{id}` | Record, view, edit, delete (FR-3, FR-4, FR-6) | details list all installments with their card payment dates, and `hasChargedInstallments` |
| `POST /api/card-purchases/{id}/reverse`, `POST /api/card-purchases/{id}/installments/{number}/reverse` | Reverse (FR-5) | optional `reason`; `409` when already reversed |
| `GET /api/card-payments?month=yyyy-MM` | Month screen list and totals (FR-8) | same totals shape as loan payments |
| `POST /api/cards/{id}/card-payments/{month}/paid`, `DELETE …/paid` | Tick (with `paidDate`, `accountId`), untick (FR-8) | also corrects or unticks an automatic payment |
| `GET /api/purchase-categories?includeHidden=`, `POST`, `PUT /{id}`, `POST /{id}/hide`, `POST /{id}/show`, `DELETE /{id}` | Categories (FR-10) | delete is `409` when used |
| `GET /api/cards/dashboard`, `GET /api/cards/{id}/balance`, `GET /api/cash-flow/chart`, `GET /api/cash-flow/report` (extended) | Balances, chart and report (FR-8, FR-9) | same shapes plus `ByCard` and the `cardId` filter |

- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (ADR 0003).

### Front-end changes (pages, components)
- Routes (pages, as for cards and loans, so they work well on a phone):
  - `/cards`: overview, replacing the placeholder text;
  - `/cards/:cardId`: the card with its card payments and purchases (FR-7);
  - `/cards/:cardId/payments/:month`: drill-down (FR-8);
  - `/cards/purchases/new?card=`, `/cards/purchases/:id`, `/cards/purchases/:id/edit`;
  - `/settings/purchase-categories`.
- `src/api/cardPurchases.ts`: TanStack Query hooks. Mutations invalidate the cards, purchases,
  card payments, month card payments, dashboard balances, chart and report queries.
- Card settings (Appendix 2):
  - `CardForm` shows **Statement date**, **Payment date**, **Paying account**, **Connected to
    the paying account** and **Max installments** (12, 24 or 36) when the card allows installments (Credit, Charge, or Debit with the box
    ticked). The paying account is prefilled with the only primary account. The "no primary
    account yet" state links to Settings.
  - When the days change on an existing card, the form says they apply from next month.
  - `CardDetailsPage` and the Settings list show the billing details. The details link to the card
    on the Cards screen.
- Components in `src/components/cardPurchases/`:
  - `CardOverviewList` (Cards screen) and `CardPaymentsList` (by month, paid state, overdue
    highlight).
  - `PurchaseList`, with installment progress and a reversed marker.
  - `PurchaseForm`: card (usable cards with billing details only), description, purchase date,
    amount (`parseEuroInput`), **In installments** (default off). Installments shows the number (2 up
    to the card's max installments) and **Postpone first installment**. A category select uses add-new (`ListSelectWithAdd`). A live
    preview of the installments shows amounts and payment dates.
  - `PurchaseDetails`: installments and their card payments, remaining amount, and **Reverse
    purchase** / **Reverse installment**, **Edit** and **Delete**. Confirmation dialogs name the
    purchase, take an optional reason, and warn when charged card payments will change.
  - `CardPaymentDrillDown`: the lines of one card payment with totals. Each line opens its purchase.
  - `CardPaymentRow` in the Month screen **Expenses** part, next to the loan payments: one
    summed-up row per card with the month's card payment total, not the single purchases (FR-8).
    Selecting the row opens `/cards/:cardId/payments/:month`. The row has a **Paid** check box for cards that are not connected (asks for the account, prefilled with the
    paying account). A connected card's payment can be corrected or unticked. Markers show overdue
    and needs-account.
  - Labels and messages in `components/cardPurchases/texts.ts` (NFR-5).
- `PurchaseCategoriesSettingsPage`, linked from Settings like Input and Loan categories.
- `CashFlowChart` gets the card filter. `MonthlyReportPage` gets the **By card** group, whose rows
  link to the drill-down.

## Steps
1. Commit the approved requirements (done) and this plan.
2. Domain: billing details and terms on `Card`, `CardBillingCalendar`, `CardPurchase` with
   installments and reversals, `CardLedgerCalculator`, purchase categories, with unit tests.
3. Infrastructure: EF configurations, the `AddCardPurchases` migration with indexes and checks,
   repositories and month queries, `CardActivityQuery` with installments.
4. Application: DTOs, validators, handlers, `CardPaymentLedger`, the extended card handlers,
   balances, chart and report with card payments.
5. Api: endpoints, error mapping, OpenAPI metadata, regenerated OpenAPI document, API tests.
6. Front-end foundations: regenerated types and `cardPurchases.ts` hooks.
7. Settings: billing details in the card form, list and details; Purchase categories section.
8. Cards screen: overview, card page, purchase form, details, reversal, edit and delete,
   drill-down.
9. Month screen: card payments in Expenses with the Paid dialogs.
10. Chart and report with card payments.
11. Documentation: `docs/INIT.md` data model.
12. Manual check on desktop and phone width, then set this plan's status to `Done`.

## Tests
- Unit tests (Domain):
  - card billing rules: each invalid case from acceptance criteria 1 and 2. Fields are required
    only when the card allows installments, and unticking clears them.
  - `CardBillingCalendar`:
    - statement day 30 and payment day 31 in February, in 30-day months and in a leap year;
    - a payment on Saturday and on Sunday moves to Monday;
    - a payment on the last Saturday or Sunday of the month stays on its day;
    - billing period boundaries;
    - a change of days from next month leaves no gap and no overlap (criterion 4).
  - `CardPurchase`:
    - 100.00 EUR in 3 gives 33.33, 33.33 and 33.34 (criterion 6);
    - a purchase in full;
    - a purchase on the statement date and the day after;
    - postponed first installment;
    - the validation rules of criterion 7, including more installments than the card allows.
  - Reversals:
    - a whole purchase before any payment;
    - after some installments were charged, giving a credit in the reversal's billing period;
    - a single installment, uncharged and charged;
    - reversing twice is rejected;
    - remaining amount.
  - `CardLedgerCalculator`:
    - the card payment amount from installments, full purchases and credits;
    - connected cards are paid at the end of the payment date, not on it;
    - an unticked automatic payment stays expected;
    - a paying account that is blocked or no longer primary gives `needsAccount`;
    - overdue.
  - `AccountBalance` with card payments, before and after a checkpoint.
  - Purchase category rename, hide and unique-name rules.
- Application tests:
  - a card with installments left counts as an ongoing transaction;
  - editing a purchase changes past card payments and reports `hasChargedInstallments`;
  - changing billing days keeps the current month's card payment.
- API / integration tests (real PostgreSQL in CI):
  - add a Credit card with billing details; prefill and validation errors;
  - record purchases in full and in installments; card payments by month and the drill-down lines;
  - reverse a purchase and an installment, with the `409` cases;
  - edit and delete;
  - tick and untick a card payment; the automatic payment of a connected card;
  - categories: duplicate name `400`, delete used `409`, hide;
  - balances, chart buckets and report with card payments, `ByCard` and the month result;
  - database checks reject `payment_day <= statement_day` and a zero amount.
- Front-end (Vitest + Testing Library):
  - `CardForm`: billing fields shown only when installments are allowed; paying account prefill
    with one primary account, required with several, the no-primary state.
  - `PurchaseForm`: in installments off by default, installment preview with the remaining cents on
    the last, postpone option, API validation errors.
  - `PurchaseDetails` reversal and delete dialogs with the warning.
  - `CardPaymentRow`: one row per card with the total, opens the drill-down; Paid asks for the
    account.
  - `CardPaymentDrillDown` totals and links.
  - Report **By card** group.
- Manual checks:
  - Add a Credit card with statement on the 5th and payment on the 20th, connected to the paying
    account, and a Debit card with installments that is not connected.
  - Record a purchase in full, a purchase in 12 installments, and a postponed purchase in 3.
    Reverse one installment and one purchase.
  - Change the statement day.
  - Check the Cards screen, the drill-down, the Month screen, the Dashboard balances, the chart and
    the report on desktop and phone width.

## Changes during implementation
- **API paths:** the Cards screen card is `GET /api/cards/{id}/installments`; its purchases,
  card payments and drill-down are under `/api/cards/{id}/…` as planned. Purchases are listed
  newest first without a filter.
- **Unticking installments:** a card that has purchases must keep allowing installments; the edit
  returns a field error on `allowsInstallments`.
- **Automatic payments kept:** when a card's paying account or its connection changes, card
  payments already paid automatically are stored as paid first, so they stay paid (the same as
  loans).
- **Credits:** a card payment can be negative when its credits are larger than its charges.
- **Card response:** `billing` (days for next month, paying account, connection, max
  installments and every version of the days) and `needsBillingDetails`.
- **Report:** card payments are one **Card payments** group in `byCategory` (id `null`), count in
  `byAccount` and the totals, and are listed in the new `byCard` group, which links to the
  drill-down. The chart's loan category filter shows loan payments only, the new card filter card
  payments only.
- **Database:** `card_purchase.installment_count` is checked to be 1 to 36; `card.paying_account_id`
  restricts deletes, so the test clean-up SQL clears it before deleting cards.
- **Tests:** the card API tests create debit cards by default, and a primary paying account for
  cards that allow installments. The API tests need PostgreSQL and run in CI.
