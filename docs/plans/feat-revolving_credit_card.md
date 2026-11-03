# Plan: Revolving credit card

- **Branch:** `claude/project-thread-no7dqr`
- **Status:** Done
- **Date:** 2026-11-03
- **Requirements:** [revolving-credit-card.md](../feature-requirements/revolving-credit-card.md) (approved)

## Goal
The user can add a card of the new type **Revolving**, with its minimum payment terms, interest
rate, bill and due day and paying account. Bills, expenses and purchases paid with it raise its
debt. While there is debt, a monthly bill for the minimum payment appears in the cash flow. The user
pays it from an account or makes an extra payment at any time to lower the debt. When the bank
issues a new card, the debt moves to it.

## Scope
- In scope:
  - Domain:
    - the Revolving card type and its terms, with their history;
    - debt entries (starting debt, corrections, transfers to a replacement card);
    - extra payments;
    - the debt and monthly bill calculation;
    - revolving cards excluded from accounts.
  - Database tables and one migration.
  - API: the extended card form, revolving card overview and details, monthly bills, extra
    payments, debt entries, and revolving payments in balances, chart and report.
  - Settings: the revolving fields in the card form, list and details.
  - **Cards** screen: revolving cards with debt, limit and next bill; the revolving card page.
  - **Month** screen: monthly bills and extra payments in the Expenses part.
  - Quick action **Pay revolving card** on the Dashboard.
  - Bills, expenses and card purchases paid with a revolving card.
  - Tests at every layer, and the `docs/INIT.md` data model.
- Out of scope:
  - Everything in section 5 of the requirements: late fees, default interest, card fees, cash
    withdrawals, planned extra payments, bank imports, debt charts and pay-off forecasts, sharing
    revolving bills, multiple currencies.
  - **Access control** (NFR-3): there is still no authentication, as for the other features.
  - Changes to the export file format. New tables are exported and imported automatically
    ([ADR 0007](../decisions/0007-data-export-and-import.md)).

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| FR-1 | `CardType.Revolving`. Not primary, no *Allows monthly installments*, no billing terms or max installments. `DebtLimit` is required. The type cannot change to or from Revolving once the card has debt entries, charges, extra payments or stored monthly bills (field error on `type`). |
| FR-1, accounts | A revolving card is never an account. A new `Card.IsAccount` (Debit or Prepaid) replaces the current "not a credit card" checks: paid-from accounts, input accounts, loan and contribution accounts, the paying account lists and the Accounts part of the Cards screen. |
| FR-2, R1 to R3a | Percentages are stored as whole **basis points** (8.33 % = 833), never floating point. R1 is 1 to 10000. R3 is 0 or more. R3a is optional, 0 up to R1, and is for display only. R2 is `Money`, 0 or more. |
| FR-2, R4 and R5 | Bill day and due day are 1 to 31. Both are clamped to the last day of a shorter month. When the due day is not later than the bill day, the due date is in the next month. The due date uses the weekend rule of `DueDateRule` (bills): Saturday or Sunday moves to Monday unless that Monday is in the next month. |
| FR-2, R6 and R7 | They reuse the card's existing `PayingAccountId` and `PaysFromAccountDirectly`. The paying account must be an account (`IsAccount`) that can be used today. It does not have to be primary. |
| FR-2, change from next bill | R1 to R5 are kept as a history of **revolving terms** (`FromMonth`, …), like `CardBillingTerms`. Changing them in the card form adds or replaces the terms from next month. |
| FR-2, R8 | A starting debt is a **debt entry** of kind `Starting` (date and debt). |
| FR-3 | Minimum = max(round(debt × R1), R2), at most the debt. Rounding is to the cent, half away from zero, in integer arithmetic. |
| FR-4 | The bill of month *m* is made on the bill date of *m* when the debt on that day, after interest, is more than zero. Charges, payments and entries dated on the bill day count in that bill. |
| FR-4, corrections | One bill's minimum and interest can be corrected. The correction is stored on the bill and marked `isCorrected`. A corrected interest changes the debt from that bill day on. |
| FR-4, estimates | Bills after today are **estimated**: from today's debt, assuming each estimated minimum is paid on its due date and there are no new charges. The month after the current one is shown. Later months are calculated only when a month view asks for them. |
| FR-5 | Debt on a day = the latest `Starting` or `Correction` entry on or before it, plus charges, interest and `TransferIn` after it, minus paid bills, extra payments and `TransferOut`. A payment that would make the debt negative on its date is refused. |
| FR-5, limit | `RemainingLimit = DebtLimit − Debt`. When it is below zero, the card has `IsOverLimit`. |
| FR-6 | `PaidFromRules`: a revolving card that can be used on the date is allowed for bills and expenses, but not for cash withdrawals. What it pays is a **charge** on the card. It is not an output of an account (`ExpenseLedger` and `CardPaymentLedger` treat it like a credit card's charge, but it goes to the revolving ledger, not to a card payment). A charge over the limit is saved with a warning flag in the response. |
| FR-6, purchases | A card purchase on a revolving card must have `InstallmentCount = 1` and no `PostponeFirst`. It is a charge on its purchase date. |
| FR-7 | Interest on the bill day = round(debt on the bill day × R3 ÷ 12), with the same rounding. It is added before the minimum is calculated. |
| FR-8, monthly bill | Paid like a loan payment. Connected: paid automatically from the paying account at the end of the due date, unless the user changed its state or the account stopped being usable (`LoanAccount.ChargesStopOn` logic, without the primary rule). Not connected: paid when the user ticks it, with the date, the account and the amount. The paid amount is at least 0.01 and at most the debt on the paid date. The default is the minimum. |
| FR-8, extra payment | Date ≤ today, amount from 0.01 up to the debt on that date, from an account that can be used. Paid on its date only, with no expected state. It does not change a bill that is already made. |
| FR-8, balance | Paid bills and extra payments are negative `BalanceMovement`s of their account. Expected monthly bills due by the end of the month lower the paying account's expected balance, like card payments. |
| FR-9 | Marking a revolving card as expired, or blocking it, with a replacement (an existing card, or a new one added with `?replaces=`): the replacement must be Revolving. Blocking gets an optional replacement for revolving cards only. The handler adds `TransferOut` on the old card and `TransferIn` on the new card, dated that day, for the old card's debt. They are linked to each other. |
| FR-9, archive | `Archive` is refused while the debt is more than zero (`CardErrors.ArchiveWithDebt`). |
| Ongoing transaction | A revolving card with debt is an ongoing transaction (`CardActivityQuery`), so a blocked or expired card stays listed while it has debt. |

### Domain / data model changes
- `Monthwise.Domain/Cards`:
  - `CardType.Revolving`.
  - `Card`:
    - `IsAccount` and `IsRevolving`;
    - the list of `RevolvingTerms`;
    - `ChangeRevolvingTerms`;
    - `Archive` takes the current debt;
    - the FR-1 validation.
  - `CardDetails` gets `Revolving` (R1 to R5, R3a), next to `Billing`.
  - New `RevolvingTerms` (`FromMonth`, `MinPaymentBasisPoints`, `MinPaymentAmount`,
    `InterestBasisPoints`, `MinPaymentInterestBasisPoints?`, `BillDay`, `DueDay`).
  - New `RevolvingCalendar`: bill date and due date of a month under the terms of that month.
- New `Monthwise.Domain/Revolving`:
  - `DebtEntry` (`Starting`, `Correction`, `TransferIn`, `TransferOut`; date, amount, note,
    linked card);
  - `ExtraPayment` (card, account, date, amount, description);
  - `RevolvingBill`: the stored bill, kept only when it is ticked, unticked, corrected or paid
    automatically before a change. `IsPaid` is nullable, and `null` means automatic.
  - `RevolvingCharge` (source id, kind Bill/Expense/Purchase, date, amount);
  - `RevolvingMath`: basis points times cents, rounded half away from zero.
  - `RevolvingDebtCalculator`. Pure and day by day over the events, it returns:
    - the debt on any day;
    - the monthly bills (`ScheduledRevolvingBill`: month, bill date, due date, debt, interest,
      minimum, paid amount and date, account, `IsEstimated`, `IsOverdue`, `NeedsAccount`,
      `IsCorrected`, `PaidAutomatically`);
    - the debt history for the card page.
  - `RevolvingErrors` with field keys.
- `Monthwise.Domain/Expenses/PaidFromRules`: `IsRevolvingCard`, and `Check` allows revolving
  cards (FR-6).
- `Monthwise.Domain/CardPurchases/CardPurchase`: the revolving rule on `InstallmentCount`.

### Database migrations
- Migration `AddRevolvingCards` (snake_case):
  - `card`: the type check allows `'Revolving'`. New checks: `NOT is_primary OR type = 'Debit'`
    (unchanged), and `type <> 'Revolving' OR debt_limit_cents IS NOT NULL`.
  - `revolving_terms` (`card_id`, `from_month`, `min_payment_bp`, `min_payment_cents`,
    `interest_bp`, `min_payment_interest_bp`, `bill_day`, `due_day`), unique on (`card_id`,
    `from_month`). Checks: `min_payment_bp between 1 and 10000`, `interest_bp >= 0`,
    `min_payment_cents >= 0`, `min_payment_interest_bp between 0 and min_payment_bp`, days `1–31`.
  - `revolving_debt_entry` (`card_id`, `kind`, `date`, `amount_cents`, `note`,
    `linked_card_id` → `card`), index on (`card_id`, `date`). `amount_cents >= 0`.
  - `revolving_extra_payment` (`card_id`, `account_id` → `card`, `date`, `amount_cents > 0`,
    `description`), index on (`card_id`, `date`).
  - `revolving_bill` (`card_id`, `month`, `minimum_cents?`, `interest_cents?`, `is_paid?`,
    `paid_date?`, `paid_amount_cents?`, `paid_account_id?` → `card`), unique on (`card_id`,
    `month`). Checks: `(is_paid is true) = (paid_date is not null)`, the same for the paid
    amount and the account.
- Foreign keys restrict deletes, as for the other card tables. The API test clean-up SQL is
  extended.
- Export and import: the tables are household data, so they are exported automatically. An older
  export file is imported through the migrations (ADR 0007), and its cards stay unchanged.

### API changes (endpoints, DTOs)
New `Api/Endpoints/RevolvingEndpoints.cs` and `Monthwise.Application/Revolving`:
- `RevolvingLedger`, which loads cards, terms, entries, extra payments, stored bills and charges
  (from `ExpenseLedger` and card purchases) once per request;
- handlers, FluentValidation validators and the same error mapping as before.

| Method and path | Purpose | Notes |
|---|---|---|
| `POST /api/cards`, `PUT /api/cards/{id}`, `GET /api/cards…` (extended) | Revolving type and terms (FR-1, FR-2) | `revolving` (R1 to R5, R3a, terms history), `startingDebt` on create, reused `payingAccount` and `paysFromAccountDirectly` |
| `POST /api/cards/{id}/archive` (extended) | FR-9 | `409` with `ArchiveWithDebt` |
| `POST /api/cards/{id}/expire`, `POST /api/cards/{id}/block`, `POST /api/cards?replaces=` (extended) | FR-9 | moves the debt to a revolving replacement, also one added right after expiring; `block` gets an optional `replacementId` for revolving cards; `400` when the replacement is not revolving |
| `GET /api/revolving-cards` | Cards screen overview (FR-10) | debt, limit, remaining limit, over limit, next bill (minimum, due date, estimated), `needsAccount`; total debt |
| `GET /api/revolving-cards/{id}?from=yyyy-MM&to=yyyy-MM` | Card page (FR-10) | terms, debt history, charges, bills, extra payments and debt entries by month |
| `GET /api/revolving-bills?month=yyyy-MM` | Month screen (FR-11) | bills and extra payments of the month with the expected, paid and total amounts |
| `POST /api/revolving-cards/{id}/bills/{month}/paid`, `DELETE …/paid` | Pay or untick a monthly bill (FR-8) | `paidDate`, `accountId`, `amount` |
| `PUT /api/revolving-cards/{id}/bills/{month}/correction`, `DELETE …/correction` | Correct the minimum and interest (FR-4, FR-7) | |
| `POST /api/revolving-cards/{id}/extra-payments`, `PUT /api/extra-payments/{id}`, `DELETE /api/extra-payments/{id}` | Extra payments (FR-8) | the response has the debt after the payment; **Pay the whole debt** uses `GET …/debt?date=` |
| `GET /api/revolving-cards/{id}/debt?date=yyyy-MM-dd` | The debt on a date | used by the extra payment form |
| `POST /api/revolving-cards/{id}/debt-entries`, `PUT /api/debt-entries/{id}`, `DELETE /api/debt-entries/{id}` | Starting debt and corrections (FR-5) | the response shows the calculated debt and the difference; transfer entries cannot be edited |
| `GET /api/cards/dashboard`, `GET /api/cash-flow/chart`, `GET /api/cash-flow/report` (extended) | Balances, chart and report (FR-8, FR-11) | revolving payments are outputs; the report adds them to `byCard` and the **Card payments** group |
| bills, expenses and card purchases (extended) | Charges (FR-6) | `paidFrom.isRevolving`, `overLimit` warning in the response |

- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (ADR 0003).

### Front-end changes (pages, components)
- Card settings:
  - `CardForm` offers **Revolving** in *Type*. For a revolving card it shows:
    - the required *Max debt limit*;
    - **Minimum payment %**, **Minimum payment amount**, **Interest rate %** and **Minimum
      payment interest rate %** (optional, labelled "for information");
    - **Bill day** and **Due day**;
    - **Paying account** (accounts only, prefilled with the only primary account) and
      **Connected to the paying account**;
    - on Add only, **Starting debt** with its date.
  - Percent inputs accept up to two decimals (`parsePercentInput`) and are sent as basis points.
  - The form hides primary, installments and billing fields for a revolving card. It says that a
    change of terms applies from the next bill.
  - The Expire and Block dialogs offer only revolving cards as the replacement of a revolving card,
    and say that the debt moves to it. **Archive** shows the debt message instead of the dialog
    while there is debt.
  - The Settings list and the card details show the terms and the current debt.
- `src/api/revolving.ts`: TanStack Query hooks. Mutations invalidate revolving, card, balance,
  month, chart and report queries.
- Components in `src/components/revolving/`:
  - `RevolvingCardsSection` on the **Cards** screen, after the accounts: each card with debt, limit,
    remaining limit (highlighted when over), and the next bill. The key figure **Revolving debt**
    goes next to the existing ones.
  - `RevolvingCardPage` (`/cards/revolving/:cardId`):
    - terms;
    - a small debt-over-time line (Recharts);
    - the bills by month, with Pay, Untick and Correct;
    - charges, extra payments and debt entries;
    - **Extra payment** and **Correct debt** actions.
  - `ExtraPaymentForm` (`/cards/revolving/:cardId/pay`, and `/pay-revolving` from the quick action
    with a card select): account prefilled with the paying account, amount (`parseEuroInput`),
    **Pay the whole debt**, date (today, not later), and description. It fits one phone screen.
  - `RevolvingBillRow` and `ExtraPaymentRow` in the Month screen **Expenses** part, in a
    **Revolving cards** section after the card payments, with totals. The Paid dialog asks for the
    account and the amount (prefilled with the minimum). Markers show overdue, estimated,
    corrected and needs account.
  - `DebtEntryForm`: starting debt or correction, showing the calculated debt and the difference.
  - Labels and messages in `components/revolving/texts.ts` (NFR-5).
- `paidFrom.ts`, `PaidFromSelect`, `PurchaseForm`:
  - revolving cards are listed with the credit cards;
  - they are excluded from the account lists everywhere;
  - a purchase on a revolving card hides *In installments*;
  - an over-limit warning is shown after saving.
- Dashboard: the **Pay revolving card** quick action is shown when a revolving card has debt.

## Steps
1. Commit the approved requirements (done) and this plan.
2. Domain: card type and terms, `IsAccount`, `RevolvingCalendar`, `RevolvingMath`, debt entries,
   extra payments, `RevolvingDebtCalculator`, `PaidFromRules` and purchase rules, with unit
   tests.
3. Infrastructure: EF configurations, the `AddRevolvingCards` migration with checks, repositories,
   and `CardActivityQuery` with debt.
4. Application: `RevolvingLedger`, handlers and validators, the extended card handlers (type lock,
   archive, transfer to a replacement), and every place that picks accounts switched to
   `IsAccount`. Balances, chart and report with revolving payments.
5. Api: endpoints, error mapping, OpenAPI metadata, the regenerated OpenAPI document, and API
   tests.
6. Front-end foundations: regenerated types and the `revolving.ts` hooks.
7. Settings: the card form, list, details and the expire, block and archive dialogs.
8. Cards screen: revolving section, key figure, card page, extra payment and debt entry forms.
9. Month screen section, quick action, and paid-from and purchase form changes.
10. Documentation: the `docs/INIT.md` data model.
11. Manual check on desktop and phone width, then set this plan's status to `Done`.

## Tests
- Unit tests (Domain):
  - Card validation:
    - a revolving card without a debt limit or terms is refused;
    - a revolving card cannot be primary or have installments;
    - R3a above R1 is refused.
  - `RevolvingMath`: 8.33 % of 1,000.00 is 83.30. Rounding half away from zero on .5 cents.
  - Minimum (criterion 2): 1,000.00 gives 83.30, 100.00 gives 13.27, 10.00 gives 10.00.
  - `RevolvingCalendar`:
    - bill day 31 in February;
    - a due day not later than the bill day falls in the next month;
    - a due date on a Saturday or Sunday moves to Monday, except at the end of the month.
  - `RevolvingDebtCalculator`:
    - no debt gives no bill (criterion 3);
    - interest on the bill day;
    - a charge on the bill day is in that bill;
    - an extra payment after the bill day lowers only the next bill (criterion 11);
    - a corrected interest changes the later debt;
    - **Pay the whole debt** stops the bills (criterion 6);
    - a starting debt and corrections;
    - estimated future bills;
    - connected and not connected bills, overdue and needs account (criterion 7);
    - a terms change from next month.
  - `PaidFromRules`: a revolving card is allowed for bills and expenses, not for cash
    withdrawals, and is not an account.
  - `CardPurchase`: installments are refused on a revolving card (criterion 12).
  - `AccountBalance` with revolving payments.
- Application tests:
  - a type change is locked once the card has activity;
  - archive is refused with debt;
  - expire with a revolving replacement moves the debt, and the next bill comes on the new card
    (criterion 13);
  - a blocked card with debt still gets bills and stays listed (criterion 10).
- API / integration tests (real PostgreSQL in CI):
  - add a revolving card with a starting debt;
  - a bill, an expense and a purchase paid with it raise the debt and are not account outputs
    (criterion 4);
  - pay a bill and add an extra payment: the debt and the account balance go down, and both
    appear in the Month list, chart and report (criterion 5);
  - a debt correction (criterion 8);
  - overview figures (criterion 9);
  - `409` on archive with debt;
  - the database checks reject a revolving card without a limit;
  - an export and import round trip keeps the revolving data.
- Front-end (Vitest + Testing Library):
  - `CardForm`: revolving fields shown and others hidden, percent parsing, prefilled paying
    account.
  - `ExtraPaymentForm`: **Pay the whole debt**, a future date and an amount over the debt refused.
  - `RevolvingBillRow` Paid dialog with the amount prefilled.
  - Paid-from lists put revolving cards with the credit cards and never in the accounts.
  - `PurchaseForm` hides installments for a revolving card.
- Manual checks:
  - Add a revolving card (8.33 %, 13.27 EUR, 12.75 %, bill day 5, due day 20) with a starting
    debt of 1,000.00 EUR.
  - Pay a bill with it, make an extra payment and correct the debt.
  - Expire it with a new revolving card and check that the debt moved.
  - Check the Cards screen, the card page, the Month screen, the Dashboard quick action, the chart
    and the report on desktop and phone width.

## Changes during implementation
- **Endpoints as built:**
  - `GET /api/revolving-cards` (the cards with their debt and next bill, and the total debt);
  - `GET /api/revolving-cards/{id}?to=yyyy-MM` (the card page; estimated bills up to `to`, at least
    up to next month) and `GET /api/revolving-cards/{id}/debt?date=` (the debt at the end of a date,
    for **Pay the whole debt** and the correction form);
  - `GET /api/revolving-bills?month=` (the Month screen);
  - `POST`/`DELETE /api/revolving-cards/{id}/bills/{month}/paid` and
    `PUT`/`DELETE /api/revolving-cards/{id}/bills/{month}/correction`;
  - `POST /api/revolving-cards/{id}/extra-payments`, `PUT`/`DELETE /api/extra-payments/{id}`;
  - `POST /api/revolving-cards/{id}/debt-entries`, `PUT`/`DELETE /api/debt-entries/{id}`;
  - `POST /api/cards/{id}/block` takes an optional `replacementCardId` for a revolving card.
- **Bill payment amount:** a bill payment can be more than the minimum but not more than the bill's
  debt on the bill day. An extra payment cannot be more than the debt on its date.
- **Month screen:** a paid bill is listed in the month of its paid date, an unpaid one in the month of
  its due date. The **Revolving cards** section is its own section after **Card payments**, and its
  bills count in the month's overdue figure and in the card payments slice of the outputs chart.
- **Purchases:** revolving cards are not in the installments card list (`/api/card-purchases/cards`);
  the purchase form adds the usable revolving cards from `/api/revolving-cards`, and the **Cards with
  installments** part of the Cards screen does not show them.
- **Over-limit warning:** after a bill, expense or purchase is saved on a revolving card, a warning
  toast says when the card is now over its limit. The charge is saved either way.
- **Starting debt date:** prefilled with today on the card form.
- **Export and import:** the four new tables are household data and are exported. A file of the
  release before this one has no revolving tables; it imports as before (the import test knows
  which tables a file of that release has).

## Open questions
- None. Decisions taken in this plan that the requirements leave open:
  - percentages are stored as basis points;
  - rounding is half away from zero;
  - charges and payments dated on the bill day count in that day's bill;
  - estimated bills assume the minimum is paid on the due date;
  - the paying account does not have to be primary (any usable account, as R6 says).
