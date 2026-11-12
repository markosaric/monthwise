# Requirements: Entering amounts in euros

- **Branch:** `fix/euro_amount_input`
- **Status:** Approved (2026-11-12)
- **Date:** 2026-11-12

> This document describes **what** the app must do, not **how** it is built.
> The implementation plan (`docs/plans/fix-euro_amount_input.md`) is written after these
> requirements are approved.

## 1. Goal

Every amount field in the app (inputs, bills, expenses, card purchases, loans, card limits,
revolving card bills and payments, shared cost contributions, balance entries) takes an amount in
euros typed by the user. Today a dot means two different things depending on how many digits follow
it: `1.23` is read as 1,23 € but `1.234` as 1.234,00 €, and `1234.567` as 1.234.567,00 €. A typing
slip can therefore save an amount a thousand times too large without any warning.

This document sets **one rule** for how a typed amount is read, so that the same text always means
the same amount and text that could mean two amounts is refused instead of guessed.

## 2. Terms

| Term | Meaning |
|---|---|
| Decimal comma | The `,` before the cents, as amounts are written in the app (e.g. `1.234,56`). |
| Thousands dot | A `.` that groups three digits (e.g. `1.234.567`), as amounts are shown in the app. |

## 3. Functional requirements

**FR-1 What is read as an amount**
- A whole number: `250` is 250,00 €.
- A comma before one or two digits is the decimal comma: `1234,5` is 1.234,50 €, `0,05` is
  0,05 €.
- Dots before the comma group thousands, in groups of exactly three digits: `1.234,56` is
  1.234,56 €.
- Without a comma, a single dot followed by **one or two** digits is a decimal point, because
  that is how amounts are typed on many keyboards: `1234.5` is 1.234,50 €, `12.30` is 12,30 €.
- Without a comma, **two or more** dots group thousands when every group after the first has
  exactly three digits: `1.234.567` is 1.234.567,00 €.
- Spaces and the `€` sign are ignored: ` 1 000,00 € ` is 1.000,00 €.
- Fields that allow a negative amount (balance entries and other balances) accept a leading `-`
  with the same rules.

**FR-2 What is refused**
- A single dot followed by **exactly three** digits and no comma, such as `1.234` or `1234.567`.
  It could be a thousands dot or a decimal point with one digit too many, so the app does not
  guess. The user writes `1234` or `1.234,00` for the thousand, or `1,23` for the cents.
- Thousands dots in the wrong place, such as `12.34.5`, `1.23,45` or `1234.567.890`.
- More than two digits after the decimal comma or decimal point, such as `1,234`.
- Anything that is not a number, a comma in the wrong place (`12,3,4`), or an empty field where an
  amount is required.
- A refused amount cannot be saved. The field shows its usual message that asks for an amount with
  an example (e.g. *Enter an amount greater than zero, e.g. 1.234,56.*), the same as for any other
  invalid amount.

**FR-3 Existing amounts**
- Amounts already saved are not changed. An amount shown for editing (e.g. `1234,56`) is read
  back as the same amount.

## 4. Non-functional requirements

- The rule is the same in every amount field and in every browser.
- Amounts stay exact to the cent; no rounding happens while reading what was typed.

## 5. Out of scope

- Percentages (minimum payment, interest rates), which have their own field rules.
- How amounts are shown (always `1.234,56 €`), which does not change.

## 6. Acceptance criteria

1. `250`, `1234,5`, `1.234,56`, `1234.5`, `12.30`, `1.234.567` and ` 1 000,00 € ` are read as
   250,00 €, 1.234,50 €, 1.234,56 €, 1.234,50 €, 12,30 €, 1.234.567,00 € and 1.000,00 € in every
   amount field.
2. `1.234`, `1234.567`, `1.500`, `12.34.5`, `1.23,45` and `1,234` are refused with the field's
   amount message, and the form cannot be saved.
3. Editing a saved amount and saving it without changes keeps the same amount, also for negative
   balances.
