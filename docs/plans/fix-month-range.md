# Plan: Months and dates stay within a supported range

- **Branch:** `fix/month-range`
- **Status:** Done
- **Date:** 2026-11-13
- **Requirements:** [month-range.md](../feature-requirements/month-range.md)

## Goal

Every month the app is asked about and every date or month it saves lies between January 2000 and
December 2100. Anything outside that range gets a validation error (400) naming the field and the
range, never an internal error (500). Fixes finding M1 of the main branch code review.

## Scope

- In scope: back-end checks of months in query strings and route values, of dates and months in
  request bodies, and of date query parameters; one definition of the supported range; tests at the
  edges of the range and with stored data outside it.
- Out of scope: limits on paging through months in the screens; changes to any calculation inside
  the range; correcting stored data; import and restore (they keep accepting older files, FR-5).

## Design

- Domain / data model changes:
  - New `Monthwise.Domain/Common/SupportedRange.cs` (FR-1, FR-2, non-functional requirements): the
    one place that defines the range, `FirstMonth = 2000-01` and `LastMonth = 2100-12`, with
    `Contains(YearMonth)`, `Contains(DateOnly)` and the two messages:
    "Enter a month from 2000-01 to 2100-12." and "Enter a date from 2000-01-01 to 2100-12-31.".
  - New `YearMonth.TryParseSupported(string?, out YearMonth)`: `TryParse` plus the range check.
    `YearMonth` itself keeps accepting years 1 to 9999, so stored data and import keep working
    (FR-5).
- API changes (endpoints, DTOs):
  - **Month query and route values (FR-1):** one shared helper in `Monthwise.Api/Endpoints`,
    `MonthParameter`, with `TryParse(value, out month)` and `Invalid(field, value)`, replaces the `YearMonth.TryParse` calls and the six private `InvalidMonth()` helpers in
    `CashFlowEndpoints`, `ExpenseEndpoints`, `LoanEndpoints`, `CardPurchaseEndpoints`,
    `RevolvingEndpoints` and `SharedCostEndpoints`. A wrong format keeps today's message ("Enter
    the month as yyyy-MM."); a month outside the range gets the range message. Every endpoint then
    accepts and refuses the same months.
  - **Dates in request bodies and date query parameters (FR-2):** one endpoint filter,
    `SupportedRangeFilter`, added once to the `/api` group in `Program.cs`. It looks at every
    argument of the request: a `DateOnly` or `DateOnly?` argument (for example the occurrence
    `date`, the chart `from` and `to`, the revolving debt `date`) and every `DateOnly`/`DateOnly?`
    property of a request record, including nested records such as `RecurrenceRequest`. A value
    outside the range answers 400 with problem details keyed by the field's own camelCase name
    (for example `expectedDate`, `endDate` of a recurrence), as the domain errors are, so the forms
    already show it on the field. The
    property list of each request type is built once and cached. New request types are covered
    without listing them (non-functional requirements).
  - **Months in request bodies (FR-2):** the places that parse them use `TryParseSupported`:
    `LoanInputChecker` (`startMonth`, `endMonth`), `LoanHandlers` (`fromMonth` of a payment
    change) and `CardRequestValidator` (`validFrom`, `validThrough`), each with the range message on
    its field.
  - The chart keeps its ten year limit (FR-4). The OpenAPI document changes only in endpoint
    descriptions, if at all; the generated client is regenerated and checked for drift.
- Front-end changes: none planned. The forms already show validation messages on their fields,
  and the Month screen already shows a failed load. This is checked by hand (see Tests).
- Database migrations: none. Stored data outside the range stays as it is (FR-5).

## Steps

1. Add `SupportedRange` and `YearMonth.TryParseSupported`, with domain tests.
2. Add `MonthParameter` and use it in all month query and route values; remove the private
   `InvalidMonth()` helpers.
3. Add `SupportedRangeFilter` and register it on the `/api` group.
4. Use `TryParseSupported` for the month strings in loan, payment change and card requests.
5. Run the edge-of-range tests (below) and fix any calculation that still fails inside the range
   (FR-3), for example by stopping a schedule at `SupportedRange.LastMonth` instead of failing.
6. Regenerate the OpenAPI document and the front-end client, run the full back-end and front-end
   test suites, and check the forms by hand.

## Tests

- Unit tests:
  - `SupportedRange` and `YearMonth.TryParseSupported`: 1999-12 and 2101-01 refused, 2000-01 and
    2100-12 accepted, wrong formats refused.
- API / integration tests (with the database):
  - Months 9999-12, 0001-01, 1999-12 and 2101-01 on every endpoint that takes a month give 400
    with the range message on the right field, not 500 (acceptance criterion 1).
  - Months 2000-01 and 2100-12 on the inputs, monthly report, Overall balance, Month screen
    data, loans, card payments, revolving bills and shared costs answer 200, with a
    recurring input, a recurring bill, a loan, an installment purchase and a revolving card in the
    data (acceptance criterion 2, FR-3).
  - Saving an input, a recurring input (end date), a bill, an expense, a purchase, a loan (start and
    end month), a payment change, a balance entry, a contribution and a paid date with 1999-12-31 or
    2101-01-01 is refused on the date field; 2000-01-01 and 2100-12-31 are accepted (acceptance
    criterion 3).
  - The chart still refuses a range longer than ten years (acceptance criterion 4).
  - Data stored directly in the database with a date outside the range (for example a recurring
    input starting in 1990) still loads on the screens that show it (FR-5).
  - The existing import tests still pass with files from earlier versions (acceptance criterion 5).
- Manual checks:
  - In the running app, enter a bill dated 2101-01-01 and a loan starting 1999-12: the message
    shows on the field.
  - Open `/month/2101-01` in the browser: the screen shows its error state, not a blank page.
