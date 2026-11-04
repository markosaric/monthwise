# Plan: Dates entered as DD.MM.YYYY

- **Branch:** `fix/date-input`
- **Status:** In progress
- **Date:** 2026-11-04
- **Requirements:** [design-refresh.md, Appendix 2](../feature-requirements/design-refresh.md#appendix-2-dates-entered-as-ddmmyyyy)

## Goal
The user can enter every date in the app as `DD.MM.YYYY` and the date that the field shows is
always the date that is saved, in every browser, including Safari, where the Add bill form showed
*Enter the due date.* although a due date was filled in.

## Requirement changes
[Appendix 2](../feature-requirements/design-refresh.md#appendix-2-dates-entered-as-ddmmyyyy)
changes FR-5 and how a date is entered in every form and dialog:

- Every day field is entered as `DD.MM.YYYY` with a `DD.MM.YYYY` placeholder and a numeric
  keyboard on phones, the same in every browser.
- Only digits are typed; the dots are added after the day and the month. `16.11.2026`,
  `16/11/2026` and `16112026` give the same result. The year has four digits and the date must
  exist.
- An empty field keeps its old rule (required message, or stays empty when optional). A field that
  is not empty but not a complete, existing date blocks saving with *Enter the date as DD.MM.YYYY,
  e.g. 16.11.2026.*
- Prefilled and stored dates are shown as `DD.MM.YYYY`. Other date rules, storage and display
  elsewhere are unchanged. Month-only fields are out of scope.

**Why:** Safari's built-in date field can show a full date while the value it passes to the page
stays empty and no change event is fired (reproduced in WebKit when the year is set with the arrow
keys), so the form saw an empty due date. The page cannot read a value the browser does not pass
on, so the built-in field is replaced by a typed one, like the `MM/YY` card validity fix.

## Scope
- In scope: all 20 `type="date"` fields:
  - Bills: `BillForm` (issue, due, paid date), `BillsSection` (mark paid dialog).
  - Expenses: `ExpenseForm` (date).
  - Cash flow: `InputForm` (issue, expected, received, end date), `BalanceEntries` (date).
  - Cards: `CardForm` (starting debt date), `PurchaseForm` (purchase date), `CardPaymentRow`
    (paid date).
  - Revolving cards: `DebtEntryDialog`, `ExtraPaymentForm`, `RevolvingBillRow` (date / paid date).
  - Loans: `PaymentDialogs` (due date, paid date).
  - Shared costs: `ContributionRow` (expected, received date).
- Out of scope: the five `type="month"` fields (loan form, loan payment change dialog, bills
  section month), back-end and API (they keep receiving and returning `YYYY-MM-DD`).

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes (pages, components):
  - `frontend/src/lib/dates.ts`: pure helpers
    - `formatDateInput(text)`: keeps up to 8 digits from what was typed or pasted and inserts the
      dots once the next part has started (`16112026` → `16.11.2026`, `161` → `16.1`, `16` stays
      `16`, so deleting back over a dot works).
    - `dateInputToIso(text)`: `16.11.2026` → `2026-11-16`, or `null` when it is not a complete,
      existing date (checks day against the month's length, including leap years).
    - `isoToDateInput(iso)`: `2026-11-16` → `16.11.2026`; empty for `''`.
    - `isInvalidDateEntry(value)`: true when a form value is not empty and not `YYYY-MM-DD`.
  - `frontend/src/components/DateInput.tsx`: a text input (`inputMode="numeric"`,
    `placeholder="DD.MM.YYYY"`, `autoComplete="off"`, `maxLength={10}`) taking `value` and
    `onChange` in the form's terms (`YYYY-MM-DD`). It keeps the typed text in local state and
    reports `YYYY-MM-DD` when the text is a complete, existing date, `''` when empty, and the typed
    text otherwise (never a valid ISO date, so the forms can tell it apart). When the value changes
    from outside (prefill, reset), the text follows it.
  - Forms with react-hook-form (`BillForm`, `ExpenseForm`, `InputForm`, `CardForm`,
    `PurchaseForm`): replace `register` on date fields with a `Controller` rendering `DateInput`.
    Their schemas add the new message for `isInvalidDateEntry` values before the existing
    required / order checks, so an empty required field keeps its message.
  - Dialogs with `useState` (`BillsSection`, `BalanceEntries`, `CardPaymentRow`,
    `DebtEntryDialog`, `ExtraPaymentForm`, `RevolvingBillRow`, `PaymentDialogs`,
    `ContributionRow`): swap `Input type="date"` for `DateInput`; before saving, an
    `isInvalidDateEntry` value shows the new message and nothing is sent. The `min` / `max`
    attributes only limited the browser's calendar and are dropped; the API's rules are unchanged
    and their errors still show.
  - The message lives once in `lib/dates.ts` (`dateEntryMessage`) and is reused.
- Database migrations: none.

## Steps
1. Add the date helpers and their unit tests.
2. Add `DateInput` with component tests (typing, pasting, prefill, outside value change).
3. Switch the bill form and bills section, then the other forms and dialogs listed in Scope.
4. Update existing tests that type `YYYY-MM-DD` into date fields to type `DD.MM.YYYY`; add tests
   for the Appendix 2 acceptance criteria.
5. Run typecheck, lint, format check and tests; check the Add bill form and one dialog in a WebKit
   browser and in Chromium.

## Tests
- Unit tests: `formatDateInput`, `dateInputToIso` (valid dates, `31.02.2026`, `29.02.2028`,
  short year, too few digits, letters), `isoToDateInput`, `isInvalidDateEntry`.
- Component tests: Add bill with `16112026` as due date posts `dueDate: "2026-11-16"`; invalid
  dates show *Enter the date as DD.MM.YYYY, e.g. 16.11.2026.* and nothing is posted; editing a
  bill shows `20.10.2026`; the empty-field messages are unchanged; one useState dialog
  (mark bill paid) saves a typed date and rejects an invalid one.
- API / integration tests: none (no back-end change).
- Manual checks: Add bill in WebKit and Chromium: type the dates, save, and confirm the bill is
  added without *Enter the due date.*
