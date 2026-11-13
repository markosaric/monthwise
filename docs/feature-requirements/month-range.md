# Requirements: Months and dates stay within a supported range

- **Branch:** `fix/month-range`
- **Status:** Approved (2026-11-13)
- **Date:** 2026-11-13

> This document describes **what** the fix must do, not **how** it is built.
> The implementation plan (`docs/plans/fix-month-range.md`) is written after these
> requirements are approved.

## 1. Goal

The app accepts any month from 0001-01 to 9999-12 wherever it asks for one, and any date in that
span in what it stores. Near the ends of that span, the calculations that look a month, a year or a
whole schedule ahead or back fail, and the app answers with an internal error (500) instead of a
clear message. The main branch code review lists this as finding M1: asking for month 9999-12 on
the inputs, monthly report or Overall balance, or for month 0001-01 on the monthly report, ends in
an internal error.

After this fix, the app works with one supported range of months and dates. A month or date outside
it is refused with a clear message, and nothing the user can enter or ask for ends in an internal
error because it lies too far in the past or the future.

## 2. Terms

| Term | Meaning |
|---|---|
| Supported range | The months from January 2000 to December 2100, and the dates inside them (2000-01-01 to 2100-12-31). |
| Month asked for | A month the user or the screens ask the app about: the month of the Month screen, the Overall balance, the monthly report, the month of a payment marked paid or corrected, the end month of a revolving card schedule, and every other place the app takes a month. |
| Stored date | A date or month the user saves: dates of inputs, bills, expenses, purchases, payments and balance entries, start and end of recurrences, loans and leases, first due dates, card expiry, and every other date or month the app stores. |

## 3. Functional requirements

**FR-1 Months asked for are checked**
- A month asked for outside the supported range is refused with the same kind of message as a
  month in the wrong format today, naming the field and the allowed range.
- Every place that takes a month applies the same range, so the same month is accepted or refused
  the same way everywhere.

**FR-2 Stored dates are checked**
- A stored date outside the supported range is refused when it is saved, with a message on the
  field that names the allowed range, in the same way as other invalid input today.
- This holds for adding and for changing, in every part of the app.

**FR-3 No internal errors at the edges**
- Asking for any month inside the supported range never ends in an internal error, including the
  first and the last month of the range, with data whose recurrences, loans, leases, installments or
  revolving schedules run past the end of the range or started before its beginning.
- Where a calculation would need a month outside the supported range (for example the month before
  January 2000, or a schedule that runs past December 2100), the app shows the result for what lies
  inside the range rather than failing.

**FR-4 Existing limits stay**
- Limits that are already narrower stay as they are, for example the ten year limit of the chart.

**FR-5 Existing data**
- Data already stored with a date outside the supported range keeps loading and is shown as
  today. It is refused only when it is changed and saved again with a date that is still outside
  the range.
- Import and restore keep accepting files made by earlier versions, whatever dates they hold.

## 4. Non-functional requirements

- A refused month or date answers at once and with the same message format as other validation
  errors, so the screens show it the way they show other input errors.
- The supported range is defined in one place, so it can be widened later without hunting through
  the app.

## 5. Out of scope

- Limiting how far the screens let the user page backwards or forwards through months, beyond the
  error message shown when the range is left.
- Changing any calculation for months inside the supported range.
- Moving or correcting stored data that lies outside the supported range.

## 6. Acceptance criteria

1. Asking for month 9999-12 or 0001-01 on the inputs, the monthly report, the Overall balance or any
   other place that takes a month gives a validation error naming the allowed range, not an
   internal error.
2. Asking for January 2000 and December 2100 on the same places works, with a recurring input, a
   recurring bill, a loan, an installment purchase and a revolving card in the data.
3. Saving an input, a bill, an expense, a purchase, a loan or a balance entry dated 1999-12-31 or
   2101-01-01 is refused with a message on the date field; 2000-01-01 and 2100-12-31 are accepted.
4. The chart still refuses a range longer than ten years, as today.
5. An import of a file made by an earlier version still succeeds.
