# Plan: Load only what one card needs

- **Branch:** `fix/single_card_loading`
- **Status:** In progress
- **Date:** 2026-11-12
- **Requirements:** none (performance fix without behaviour change)

## Goal
Opening one card (`GET /api/cards/{id}`, the card details and edit form) reads only the data that
card needs, instead of calculating the debt of every revolving card. This is the last part of
review finding L9; it waited for H4 ([fix-repeated-loading.md](fix-repeated-loading.md)), which
now reads each table only once per request. Nothing the user sees changes.

## Scope
- In scope: `GetCardHandler` (one card).
- Out of scope:
  - The card list, the Dashboard cards and the revolving card screens, which need every card.
  - Reading only part of a table for a revolving card (for example only its own bills and
    expenses). The debt needs the card's whole history and these tables are read once per request
    since H4; narrowing the reads is out of scope, as in H4's requirements.

## Design
- Measured on the local test database (PostgreSQL statement log, one `GET /api/cards/{id}`):
  - today, a debit card and a revolving card both run **10 queries**: issuers, banks, cards, and
    then expense categories, bills, expenses, card purchases, debt entries, extra payments and
    revolving bills, which are only needed for the debt of revolving cards;
  - after this fix, a debit, credit or prepaid card runs **3 queries** (issuers, banks, cards);
    a revolving card keeps its 10, because its debt needs that history.
- Domain / data model changes: none.
- Application changes:
  - `GetCardHandler` loads the issuer, bank and card lists as today (they give the names of the
    issuer, bank, replacement and paying account), finds the card, and returns 404 before loading
    anything else when it does not exist.
  - Only for a revolving card it loads the revolving ledger and sets the debt of **that card**
    (`RevolvingBook.DebtOf(id)`) instead of the debts of all revolving cards.
- API changes (endpoints, DTOs): none; the response is the same field for field.
- Front-end changes: none.
- Database migrations: none.

## Steps
1. Change `GetCardHandler` as above.
2. Add the API test below; run the back-end build, format check and tests.

## Tests
- Unit tests: none.
- API / integration tests:
  - Existing tests that read one card keep passing unchanged: a revolving card with its debt
    (`RevolvingEndpointTests`), a card with installments (`CardPurchaseEndpointTests`) and a
    replacement card (`CardEndpointTests`).
  - New test: an unknown card id answers 404.
  - New test: the details of a debit card and of a revolving card with debt return the same
    fields as the card list for those cards, so skipping the revolving ledger changes nothing.
- Manual checks: count the statements of one `GET /api/cards/{id}` in the PostgreSQL log for a
  debit and a revolving card (3 and 10, as above).

## Open questions
- None.
