# Plan: Shared bills and loan payments

- **Branch:** `feat/shared_bills`
- **Status:** Draft
- **Date:** 2026-10-10
- **Requirements:** [shared-bills.md](../feature-requirements/shared-bills.md) (approved), and
  [monthly-bills-and-expenses.md, Appendix 1](../feature-requirements/monthly-bills-and-expenses.md#appendix-1-shared-bills),
  [loans-and-leases.md, Appendix 1](../feature-requirements/loans-and-leases.md#appendix-1-shared-loans)

## Goal
The user shares a recurring bill, a bill, a loan or a single loan payment with one participant by
splitting it into 2 to 4 shares. Every issued shared bill and every shared loan payment then has one
expected **Agreed contributions** input from that participant, shown in the Income part of the Month
screen, that the user ticks when the money arrives. The Month screen shows the household's own part,
a new **Shared costs** screen shows what each participant owes, and the monthly report shows the
shared totals.

## Requirement changes
- [monthly-bills-and-expenses.md, Appendix 1](../feature-requirements/monthly-bills-and-expenses.md#appendix-1-shared-bills)
  (2026-10-09, changes section 5):
  - Sharing a bill is no longer out of scope; a recurring bill or a bill can be shared with one
    participant.
  - The bill itself is unchanged: its full amount still counts in the cash flow, the balance and the
    card payment; the participant's part is a separate input.
  - The Month screen also shows the own part of a shared bill and of the bills totals.
- [loans-and-leases.md, Appendix 1](../feature-requirements/loans-and-leases.md#appendix-1-shared-loans)
  (2026-10-09, changes section 5):
  - Sharing a loan payment is no longer out of scope; a loan or a single loan payment can be shared
    with one participant.
  - Loan payments are unchanged; the participant's part is a separate input.
  - The residual value bill of a shared lease is shared with the same shares.
  - The Month screen also shows the own part of a shared loan payment and of the loan payment totals.

## Scope
- In scope:
  - Domain: participants, the cost share value object, shares on recurring bills, bills, loans and
    loan payments, and contributions derived from shared bills and loan payments.
  - Database: one migration adding the participant table, share columns and the contribution link on
    inputs.
  - API: participants, shares in the bill, recurring bill and loan requests, one loan payment's share,
    contribution actions, the Shared costs overview, and the extended month lists and report.
  - Front-end: share fields in the recurring bill, bill and loan forms and the loan payment dialog,
    own part on the Month screen, contribution rows in the Income part, the Shared costs screen with
    participants, and the report section.
  - Tests at every layer.
- Out of scope:
  - Everything in section 5 of the requirements: sharing expenses, cash withdrawals or card purchases,
    more than one participant per cost, percentage or fixed-amount shares, paying the payee directly,
    participants as users, grouped contributions, currencies.
  - **Access control** (NFR-3): there is still no authentication, as for the earlier features.
  - Households: one implicit household.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| FR-1 | `Participant`: name 1 to 50, unique case-insensitive; description max 200; `ArchivedOn`. Archived participants are not offered for new shares; a participant used by any share (recurring bill, bill, loan, loan payment) can only be archived; an unused one can be deleted. |
| FR-1, payer | Each participant has a `PayerId`. Creating a participant links the existing payer with the same name (ignoring upper/lower case, restoring it when removed) or creates one. Renaming a participant renames its payer; when another payer already has that name, the rename is refused with a field error. |
| FR-2 | `CostShare(ParticipantId, Shares, ParticipantShares, ContributionAccountId?)`: `Shares` 2 to 4 (`CostShare.MaxShares = 4`), `ParticipantShares` 1 to `Shares − 1`. Default 2 and 1. The percentage shown is `round(100 × ParticipantShares ÷ Shares)`. |
| FR-3, recurring bill | `RecurringBill.Share` is copied to each bill added by **Add recurring bills**. Changing it later never changes bills already added. |
| FR-3, bill | `Bill.Share` can be set, changed or cleared on one bill (also a one-off bill); a change on a bill added from a recurring bill sets `IsShareChanged` (shown as a marker). |
| FR-3, loan | `Loan.Share` applies to every payment (residual value included) that is not paid and not changed by hand. A paid payment keeps the share it was paid with: paying stores the share on the `LoanPayment`, the same way its amount is kept today. |
| FR-3, loan payment | `LoanPayment.Share` with `IsShareChanged`: a share or "not shared" set on one payment. When the loan's share changes and some expected payments have a hand-changed share, the API returns them and the front-end asks whether to keep them, as for hand-changed amounts after a payment change (loans FR-7). |
| FR-4 | `CostShare.PartOf(amount) = round_half_up(amount.Cents × ParticipantShares ÷ Shares)` in integer cents; `OwnPartOf(amount) = amount − PartOf(amount)`. A Not issued bill has no part. A card-paid bill or payment is shared the same way. |
| FR-5 | A contribution belongs to one issued shared bill or one shared loan payment, identified by a `ContributionSource` (`Bill` + bill id, or `LoanPayment` + loan id, month and kind). Values: payer = the participant's payer, category = Agreed contributions (`0199b0c0-0000-7000-8000-000000000203`), amount = part, issue date = bill issue date / payment bill date, expected date = due date, description = bill name / "loan name n/N". |
| FR-5, account | `CostShare.ContributionAccountId` when the user chose one, otherwise the account the bill or payment is paid from when it is an account (bill *Paid from*, standing order account, or the paid account), otherwise the only primary account. When none applies, the contribution is shown as **needs an account**, is left out of every account balance, and is an expected input without an account in the chart and report; choosing one saves it as `ContributionAccountId`. |
| FR-5, FR-6 | **Calculated, then stored** — the same approach as recurring input occurrences and loan payments: an expected contribution is calculated from its cost every time it is read, so it always follows the cost. It is stored as a `CashFlowInput` with its `ContributionSource` only when the user ticks it as received or edits it, and a stored contribution is never changed automatically. |
| FR-6, received | When the cost changes, a received contribution whose amount differs from the new part is listed in the confirmation with the difference. The bill and loan payment details return the stored contribution, and the front-end works out the new part with the same rounding (`costShare.ts`) to warn before saving. |
| FR-6, edit and reset | Editing a contribution stores it with `IsEdited = true`. **Reset to part**: an expected stored contribution is deleted, so it is calculated again; a received one gets the current part as amount and `IsEdited = false`. Unticking a received contribution that was not edited deletes it (calculated again); an edited one stays stored as expected. |
| FR-6, delete | Deleting a shared bill, or closing or deleting a shared loan, deletes the stored expected contributions of the removed bills and payments and clears the `ContributionSource` of received ones, so they stay as ordinary inputs. The delete and close confirmations list the received ones. A contribution itself cannot be deleted from the Income list (`409`, "stop sharing the bill or loan payment instead"). |
| FR-7 | Month bills and loan payments return `share` and `ownPartCents`; their totals add `ownPartCents`. Income entries return `contribution` (source, cost name, part, `isEdited`, `needsAccount`). |
| FR-8 | `GET /api/shared-costs?month=` per participant: shared bills and loans with their shares, the month's parts, received and outstanding (all months, oldest first, `isOverdue` when expected date < today). |
| FR-9 | Balances, chart and report already count bills, loan payments and inputs; contributions join through the input ledger, so the cash flow stays gross. The report adds a `sharedCosts` block: total of shared costs, participants' parts, own part, received and outstanding contributions of the month. |

### Contributions in the input ledger
- `InputEntry` gets an optional `ContributionSource`. `InputLedger` gets a third source next to stored
  inputs and calculated recurring occurrences: `ContributionCalculator` returns the calculated
  contributions of shared bills (issued, by month date) and shared loan payments (from the loan
  schedules) between two dates, leaving out sources that have a stored contribution.
- Every caller of `InputLedger` (month inputs, balances, expected balance, chart, report) therefore
  gets contributions without further changes. An entry without an account (FR-5, account) is skipped
  by the balance calculation and reported as an expected input without an account.
- `LoanLedger` already builds the loan schedules; the calculator reuses it so a payment change, a
  hand-changed payment or a paid payment is reflected the same way as on the Month screen.

### Domain / data model changes
- `Monthwise.Domain/SharedCosts`:
  - `Participant`: `Create`, `Update`, `Archive`, `Restore`; `SharedCostErrors` with field keys.
  - `CostShare` (record): `Create` with validation, `PartOf`, `OwnPartOf`, `Percentage`.
  - `ContributionSource` (record): `ForBill(billId)`, `ForLoanPayment(loanId, month, kind)`.
  - `Contribution.Calculate(source, share, cost amount, dates, description, accounts)` returning
    `InputDetails` (with a nullable account).
- `RecurringBill`, `Bill`: `Share` (nullable `CostShare`), `SetShare`, `ClearShare`; `Bill` also
  `IsShareChanged`. `Bill.CreateFromRecurring` copies the share.
- `Loan`: `Share`, `SetShare`. `LoanPayment`: `Share`, `IsShareChanged`, `ChangeShare(share or none)`,
  `ResetShare()`; paying a payment keeps the share that applies. `ScheduledPayment` gets the share
  that applies to it (stored, hand-changed or the loan's). `IsEmpty` also checks the share.
- `CashFlowInput`: `ContributionSource?` and `IsEdited`; `CreateContribution`, `Detach` (clears the
  source).

### Database migrations
- Migration `AddSharedCosts` (snake_case):
  - `participant` (`id`, `name`, `payer_id` → `payer` unique, `description`, `archived_on`,
    `created_at`, `updated_at`), unique index on `lower(name)`.
  - Share columns on `recurring_bill`, `bill`, `loan` and `loan_payment`: `share_participant_id` →
    `participant`, `share_count`, `participant_share_count`, `contribution_account_id` → `card`;
    `bill` and `loan_payment` also `is_share_changed` (bool, default false).
  - `cash_flow_input`: `contribution_bill_id` → `bill`, `contribution_loan_id` → `loan`,
    `contribution_month`, `contribution_payment_kind`, `is_edited` (bool, default false); unique
    indexes on `contribution_bill_id` and on (`contribution_loan_id`, `contribution_month`,
    `contribution_payment_kind`) where not null.
- Check constraints:
  - share columns all null or all set (`contribution_account_id` optional), `share_count between 2
    and 4`, `participant_share_count between 1 and share_count − 1`;
  - `loan_payment`: a null share with `is_share_changed = true` means "not shared" for that payment;
  - `cash_flow_input`: a bill source and a loan source are never both set; loan source columns all
    null or all set.
- Foreign keys restrict deletes; the delete handlers detach received contributions first (FR-6).

### API changes (endpoints, DTOs)
New `Api/Endpoints/SharedCostEndpoints.cs` and `Monthwise.Application/SharedCosts` handlers, with the
same error mapping as before. `ShareRequest` / `ShareResponse`: `participantId`, `shares`,
`participantShares`, `contributionAccountId` (response also `participantName`, `percentage`).

| Method and path | Purpose | Notes |
|---|---|---|
| `GET /api/participants?includeArchived=`, `POST`, `PUT /{id}`, `POST /{id}/archive`, `POST /{id}/restore`, `DELETE /{id}` | Participants (FR-1) | delete `409` when used; rename `409` when another payer has the name |
| `RecurringBillRequest`, `BillRequest`, `LoanRequest` (extended) | Share a recurring bill, a bill or a loan (FR-2, FR-3) | `share: ShareRequest \| null`; `LoanRequest.keepChangedPaymentShares` when expected payments have a hand-changed share |
| `PUT /api/loans/{id}/payments/{month}/{kind}/share`, `DELETE …/share` | Share, change or stop sharing one loan payment; reset it to the loan's share (FR-3) | body `ShareRequest \| null` (null = not shared) |
| `GET /api/bills?month=`, `GET /api/loan-payments?month=` (extended) | Own part per row and in the totals (FR-7) | `share`, `ownPartCents`, `isShareChanged` |
| `GET /api/bills/{id}`, `GET /api/loans/{id}` (extended) | Contribution(s) for the warning (FR-6) | stored contribution state and amount |
| `GET /api/inputs?month=` (extended) | Contributions in the Income part (FR-5, FR-7) | `contribution` on entries |
| `PUT /api/bills/{id}/contribution`, `POST …/contribution/received`, `DELETE …/contribution/received`, `POST …/contribution/reset` | Edit, tick, untick, reset a bill's contribution (FR-5, FR-6) | edit body is `InputRequest` |
| same four under `/api/loans/{id}/payments/{month}/{kind}/contribution` | The same for a loan payment's contribution | |
| `DELETE /api/inputs/{id}` (changed) | Refuses a contribution | `409` |
| `GET /api/shared-costs?month=yyyy-MM` | Shared costs overview (FR-8) | per participant: costs, month parts, received, outstanding |
| `GET /api/cash-flow/report` (extended) | Shared totals (FR-9) | `sharedCosts` block |

- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (ADR 0003).

### Front-end changes (pages, components)
- Routes: `/shared-costs` (Shared costs screen with the month picker and the participants list),
  `/shared-costs/participants/new`, `/shared-costs/participants/:id/edit`. A **Shared costs** item is
  added to the navigation after Loans.
- `src/api/sharedCosts.ts`: participants and overview hooks, contribution mutations. Mutations
  invalidate inputs, bills, loan payments, balances, chart, report and the overview.
- `src/components/sharedCosts/`:
  - `ShareFields`: **Shared** check box, participant select with **Add new…**, shares (2 to 4) and the
    participant's shares, with the percentage, the participant's part and the own part for the
    current amount. Used in `RecurringBillForm`, `BillForm`, `LoanForm` and the loan payment dialog
    (`PaymentDialogs`, "Share this payment").
  - `costShare.ts`: `partOf`, `ownPartOf` in integer cents, matching the back-end rounding.
  - `ContributionWarning`: lists received contributions whose amount differs from the new part.
  - `SharedCostsPage`, `ParticipantForm`, `ParticipantsList` (archive, restore, delete).
  - Labels and messages in `texts.ts` (NFR-5).
- Month screen:
  - `BillsSection` and `LoanPaymentsSection`: shared marker, own part next to the amount, own part
    total; hand-changed share marker.
  - `IncomeSection`: contribution rows show their bill or loan payment (linking to it), **needs an
    account**, and the actions tick, edit and **Reset to part**; no delete.
- `MonthlyReportPage`: **Shared costs** block.

### Documentation
- `docs/INIT.md` section 3: add `participant`, the share columns and the contribution link on inputs.

## Steps
1. Commit the approved requirements (done) and this plan.
2. Domain: `Participant`, `CostShare`, `ContributionSource`, contribution calculation, shares on
   recurring bills, bills, loans and loan payments, contribution fields on inputs, with unit tests.
3. Infrastructure: EF configurations, the `AddSharedCosts` migration with checks and indexes,
   repositories.
4. Application: participant handlers, shares in the bill, recurring bill and loan handlers,
   `ContributionCalculator` in the input ledger, contribution handlers, delete/close detaching,
   Shared costs overview, report block.
5. Api: endpoints, error mapping, regenerated OpenAPI document, API tests.
6. Front-end foundations: regenerated types, hooks, `costShare.ts`.
7. `ShareFields` in the recurring bill, bill and loan forms and the loan payment dialog, with the
   contribution warning.
8. Month screen: own part in Bills and Loan payments, contribution rows in Income.
9. Shared costs screen and participants, navigation item.
10. Monthly report block.
11. Documentation: `docs/INIT.md` data model.
12. Manual check on desktop and phone width, then set this plan's status to `Done`.

## Tests
- Unit tests (Domain):
  - `CostShare`: shares 2 to 4, participant shares 1 to shares − 1; parts for 2/1, 3/1, 3/2, 4/3 with
    odd cents (e.g. 100.01 split 2/1 gives 50.01 and 50.00, half a cent rounded up) always add up to
    the amount.
  - `Participant`: name rules, archive and restore.
  - `Bill`: share copied from the recurring bill, changed on one bill, Not issued has no part.
  - `LoanSchedule`: the loan's share applies to unpaid payments; a hand-changed or "not shared"
    payment keeps its own; a paid payment keeps the share it was paid with; residual value shared.
  - Contribution calculation: dates, description, account fallback order, no account.
- Application tests:
  - An issued shared bill and a shared loan payment each give exactly one calculated contribution; a
    Not issued bill gives none.
  - Changing the bill amount, a payment change or the loan's share changes calculated contributions;
    a received one stays and the difference is reported.
  - Receive, edit, untick and reset rules; delete of a contribution refused.
  - Deleting a shared bill and closing a shared loan detach received contributions and drop expected
    ones.
  - Balances, chart and report: the full cost is an output, the contribution an input; a contribution
    without an account is in no balance.
- API / integration tests (real PostgreSQL in CI):
  - participants CRUD, `409` cases, payer linking and renaming;
  - share in recurring bill, bill and loan requests; add-recurring copies the share; loan payment
    share endpoints;
  - inputs month list with contributions; contribution endpoints for bills and loan payments;
  - shared costs overview and report block;
  - database checks reject 5 shares, participant shares equal to shares, and half-set share columns.
- Front-end (Vitest + Testing Library):
  - `ShareFields`: defaults 2 and 1, participant shares limited to fewer than shares, percentage and
    parts shown, Add new participant.
  - `costShare.ts` matches the back-end examples.
  - `BillsSection` / `LoanPaymentsSection`: own part and totals.
  - `IncomeSection`: contribution row with link, needs-account marker, Reset to part, no delete.
  - `SharedCostsPage`: outstanding oldest first with overdue highlighted, tick as received.
- Manual checks:
  - Share an electricity recurring bill 2/1 with a made-up participant, add recurring bills to next
    month, issue the bill, tick the contribution as received; change the bill amount and see the
    warning.
  - Share a lease 2/1, change one payment's share to not shared, record a payment change, check the
    residual value.
  - Check the Month screen, the Shared costs screen, the Dashboard balances, the chart and the report
    on desktop and phone width.

## Decisions
- **Expected contributions are calculated, not stored,** like recurring input occurrences and loan
  payments, so they follow their cost without sync code; only received or edited ones are stored.
- **The contribution account** chosen by the user is kept on the share (`ContributionAccountId`), so
  a calculated contribution needs no stored row just to remember it.
- **A contribution cannot be deleted** from the Income list; it goes away when the bill or payment is
  no longer shared.
- **Shared costs** gets its own navigation item and screen, and participants are managed there (FR-1,
  FR-8), not in Settings.
