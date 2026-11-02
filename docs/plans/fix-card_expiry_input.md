# Plan: Card validity entered as MM/YY

- **Branch:** `claude/project-thread-qlqnr4`
- **Status:** In progress
- **Date:** 2026-11-02
- **Requirements:** [credit-card-settings.md, Appendix 4](../feature-requirements/credit-card-settings.md#appendix-4-valid-from-and-valid-through-entered-as-mmyy)

## Goal
The user can enter a card's *Valid from* and *Valid through* as `MM/YY`, the way it is printed on
the card, in every browser, including Safari, where the fields are now empty text boxes.

## Requirement changes
[Appendix 4](../feature-requirements/credit-card-settings.md#appendix-4-valid-from-and-valid-through-entered-as-mmyy)
changes A6, A7, FR-2, FR-4 and acceptance criterion 3:

- Both fields are entered as `MM/YY` (e.g. `02/28`) with an `MM/YY` placeholder and a numeric
  keyboard on phones, the same in every browser.
- Only digits are typed; the slash is added after the month. `02/28` and `0228` give the same
  result. A two-digit year is in the 2000s.
- Anything that is not a complete `MM/YY` with month `01` to `12` blocks saving with *Enter the
  month and year as on the card, e.g. 02/28.*
- *Valid from* stays optional, *Valid through* required, and the order rule is unchanged.
- Editing a card shows its stored dates as `MM/YY`. Storage and display elsewhere are unchanged.

**Why:** `<input type="month">` is not supported by Safari, so in production both fields were
empty text boxes with no hint of the format.

## Scope
- In scope: the Add and Edit card forms (*Valid from*, *Valid through*).
- Out of scope: other month fields (loan form, loan payment change dialog, bills section), which
  use the same browser month picker; they can get their own fix. No back-end or API change.

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none. The API keeps receiving and returning `YYYY-MM`.
- Front-end changes (pages, components):
  - `frontend/src/lib/dates.ts`: pure helpers `formatCardMonthInput` (keeps up to 4 digits from
    what was typed or pasted and inserts the slash once a third digit is typed: `0228` → `02/28`,
    `022` → `02/2`, `02` stays `02`, so deleting back over the slash works), `cardMonthToYearMonth` (`02/28` → `2028-02`, or
    `null` when not a valid `MM/YY`) and `yearMonthToCardMonth` (`2028-02` → `02/28`).
  - `frontend/src/components/cards/CardMonthInput.tsx`: a text input with
    `inputMode="numeric"`, `placeholder="MM/YY"`, `autoComplete="off"` and `maxLength={5}` that
    reformats the value on change with `formatCardMonthInput`. Used through react-hook-form's
    `Controller` so the formatted value is the form value.
  - `cardFormModel.ts`: form values for both fields hold `MM/YY`. The schema checks them with an
    `MM/YY` pattern (month `01`-`12`) and the new message; the order rule compares the converted
    `YYYY-MM` values; `toCardRequest` converts to `YYYY-MM`.
  - `CardForm.tsx`: replace both `type="month"` inputs with `CardMonthInput`.
  - `CardFormPages.tsx`: the Edit page prefills both fields with `yearMonthToCardMonth`.
  - `texts.ts`: `monthRequired` becomes *Enter the month and year as on the card, e.g. 02/28.*
- Database migrations: none.

## Steps
1. Add the date helpers and their unit tests.
2. Add `CardMonthInput` and use it for both fields in `CardForm`.
3. Switch the form model (schema, order rule, request) and the Edit page prefill to `MM/YY`.
4. Update the card form tests to type `MM/YY`, add tests for the new behaviour.
5. Run typecheck, lint, format check and tests; check the form in a WebKit (Safari) browser.

## Tests
- Unit tests: helpers for `0228`, `02/28`, partial input, deleting the slash, letters,
  `13/28`, `00/28`, conversions both ways.
- Component tests (cards.test.tsx): adding a card by typing `0125` and `1229` sends
  `validFrom: '2025-01'`, `validThrough: '2029-12'`; `13/28` and `02/2` show the message and do
  not save; Valid through before Valid from shows the order message; the Edit form shows
  `01/25` and `02/28` for a card from the fixtures.
- Manual checks: open Add card in Playwright WebKit and Chromium, type `0228`, see `02/28` and a
  saved card valid through 02/2028.
