# Requirements: Card and bank account settings

- **Branch:** `feat/credit_card_settings`
- **Status:** Approved (2026-10-04)
- **Date:** 2026-10-04

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-credit_card_settings.md`) is written after these
> requirements are approved.

## 1. Goal

The user can register, view, change, block, expire and archive the payment cards and bank accounts
they use, from the Settings screen. The Dashboard (initial screen) shows all currently valid cards
at a glance, and tells the user to set up cards when none exist yet.

Cards registered here are the foundation for later features (card installments, card statement
reconciliation, debt overview, income accounts), which refer to them.

## 2. Terms

| Term | Meaning |
|---|---|
| Card | A payment card or bank account record the user registers in the app. In this document "card" covers both. One record per card; cards of the same bank are grouped by their owner. |
| Issuer | The card network / scheme, e.g. Visa, Mastercard, Diners, Maestro, American Express. |
| Owner | The bank or institution that holds the account, e.g. ZaBa, PBZ, Erste, OTP, RBA. |
| Primary account | A main current account (Croatian: *tekući račun*). |
| Ongoing transaction | A transaction connected to a card that is still running (e.g. an installment plan with installments left). Introduced by later features; until then no card has any. |

## 3. Card attributes

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| A1 | Custom name | Yes | Free name chosen by the user, e.g. "Family Visa". Shown everywhere the card is referenced. Unique among the user's cards, ignoring upper/lower case. Maximum 50 characters. |
| A2 | Issuer | Yes | Chosen from a list. The list starts with Visa, Mastercard, Diners, Maestro, American Express, and the user can **add new issuers**, which are then available for every card. |
| A3 | Owner (bank) | Yes | Chosen from a list. The list starts with ZaBa, PBZ, Erste, OTP, RBA, Addiko, and the user can **add new banks**, which are then available for every card. |
| A4 | Card number | No | The full card number as printed on the card (13 to 19 digits), checked for typing mistakes (checksum). Shown **masked** (`•••• 1234`) everywhere unless the user reveals it (FR-3). |
| A5 | IBAN | No | The bank account IBAN, e.g. `HR12 3456 7890 1234 5678 9`, checked for a valid format and checksum. Shown **masked** (country code and last 4 digits, `HR•• … 6789`) unless revealed. |
| A6 | Valid from | No | Month and year as printed on the card (`MM/YYYY`). Some cards print it, some do not; when it is empty, the card counts as valid from the start. |
| A7 | Valid through | Yes | Month and year as printed on the card (`MM/YYYY`). Same as or later than *Valid from* when that is given. The card is valid until the last day of that month. |
| A8 | Type | Yes | One of: **Debit**, **Charge**, **Credit**, **Prepaid**. |
| A9 | Is primary account | No (default: No) | Marks the card as a primary current account (*tekući*). |
| A10 | Max debt limit | No | The maximum amount that can be owed on the card (for Debit: the allowed overdraft, *dopušteni minus*), in EUR with cents. Optional for every type; when given, zero or greater. A fixed value for now (see section 8). |
| A11 | Allows monthly installments | No (default: No) | A check box for **Debit** cards: the card can be used to pay in monthly installments. Credit and Charge cards always allow monthly installments, and Prepaid cards never do, so the check box is only offered for Debit cards. |

General rules:
- *Card number* and *IBAN* are both optional; either, both or none can be given.
- When given, the same card number or IBAN cannot be registered on two cards that are not archived.
- All amounts are in EUR and exact to the cent.
- Expired, blocked and archived cards stay in the app with their history.

## 4. Functional requirements

### 4.1 Settings: card management

The Settings screen gets a **Cards and accounts** section.

**FR-1 List cards**
- Shows cards with: custom name, issuer, owner, masked card number and/or IBAN, type, valid
  through, max debt limit, primary marker, and card status (FR-11).
- Default order: primary accounts first, then by custom name.
- Shown by default: all cards that are not archived, except expired cards without ongoing
  transactions.
- Filters show the hidden ones on demand: **Expired** and **Archived**.
- When no cards exist, the section shows an empty state with a short explanation and an
  **Add card** action.

**FR-2 Add a card**
- The user can open an **Add card** form, fill in the attributes from section 3 and save.
- Issuer and owner fields offer the existing list entries and an **Add new…** option; a new entry
  is saved to the list and selected.
- Required fields are clearly marked; the form cannot be saved until all rules in section 3 pass.
- Each validation error is shown next to the field it belongs to, in plain language.
- After saving, the new card appears in the list and a confirmation is shown.
- The user can cancel; nothing is saved and no partial card is created.

**FR-3 View card details**
- The user can open a card to see all its attributes, its status history (blocked on, expired on,
  reasons) and its replacement link (FR-8).
- Card number and IBAN stay masked; the user can **reveal** each one with an explicit action. A
  revealed number is hidden again when the user leaves the screen.

**FR-4 Edit a card**
- The user can change any attribute of a card that is not blocked, expired by the user, or
  archived, with the same rules as when adding.
- Cancelling an edit leaves the card unchanged.

**FR-5 Archive a card**
- Cards are not deleted. The **Archive** action (in place of delete) asks for confirmation in a
  dialog that names the card.
- An archived card is read-only, hidden from the default list, from selection lists and from the
  Dashboard, and visible under the **Archived** filter with its history.
- Archiving cannot be undone: an archived card cannot be restored. The dialog says so.
- Archiving a primary account removes its primary mark.

**FR-6 Primary account**
- Only cards of type Debit can be marked as primary.
- Several cards can be primary (there can be several accounts that receive income). How income
  sources are connected to accounts is described in later requirements.

**FR-7 Block a card**
- The user can **block** a card, e.g. when it is lost, stolen or blocked by the bank. Blocking is
  not deleting: the card and all its history stay in the app.
- Blocking asks for confirmation in a dialog that names the card. The user can add an optional
  reason (e.g. "lost", "blocked by bank"). The app records the date the card was blocked.
- A blocked card cannot be used for any new activity (installments, charges, statements, payments).
  Ongoing transactions stay on the card.
- A blocked card is read-only, except for unblocking and archiving.
- The user can **unblock** a card after confirmation; it then returns to its date-based status
  (FR-11).
- Blocking a primary account removes its primary mark; the user is told this before confirming.

**FR-8 Mark a card as expired**
- The user can manually **mark a card as expired** before its *Valid through* date, e.g. when the
  bank replaces it with a new card because of a technical problem, damage or a security issue.
- Marking as expired asks for confirmation, records the date it was marked and an optional
  reason, and warns that it **cannot be undone**.
- When marking as expired, the user can optionally choose the **replacement card**: an existing
  card, or a new card added right away with the form prefilled from the old card's details
  (name, issuer, owner, type, primary, debt limit). The link is shown on both cards' details.
- From the marked date on, the card is treated exactly like a card whose *Valid through* has
  passed: it cannot be used for new activities, ongoing transactions stay on it, and its history
  stays visible.
- A card expired by the user is read-only, except for archiving.
- Expiring a primary account removes its primary mark; the user is told this before confirming.

### 4.2 Dashboard: card status

**FR-9 Card status widget**
- The Dashboard (initial screen) shows a **Cards** widget with every **valid** card: status Active
  or Expiring soon.
- Blocked and expired cards appear on the widget only while they have ongoing transactions.
  Archived cards never appear.
- Each entry shows: custom name, issuer, owner, masked card number and/or IBAN, type, max debt
  limit, valid through, and card status.
- Primary accounts are shown first and visibly marked.
- Selecting an entry takes the user to that card's details.

**FR-10 Empty state**
- When no cards are registered, the widget shows a message that cards need to be set up, e.g.
  *"No cards yet. Set up your cards and accounts to start tracking card spending."*, with a
  **Set up cards** action that opens the Cards and accounts section in Settings.
- When cards exist but none is shown, the widget says there are no valid cards and links to
  Settings.

**FR-11 Card status**
Each card has exactly one status. Statuses set by the user take priority over date-based ones,
checked in this order:

| Status | When | Set by |
|---|---|---|
| Archived | the user archived the card (FR-5) | user |
| Blocked | the user blocked the card (FR-7) | user |
| Expired | the user marked it as expired (FR-8), or today is after the last day of *Valid through* | user or date |
| Not yet valid | *Valid from* is given and today is before it | date |
| Expiring soon | *Valid through* ends within the next 2 months | date |
| Active | none of the above | date |

Blocked, Expired and Expiring soon are visually highlighted on both the Dashboard and in Settings.
For a card blocked or expired by the user, the status shows the date and the reason, if one was
given. Only Active and Expiring soon cards can be used for new activities.

**FR-12 Card usage (later)**
The widget is designed so that the state of each card (ongoing transactions, current debt,
remaining limit) can be added once card installments and other transactions exist. This is
**not** part of this feature.

## 5. Non-functional requirements

- **NFR-1 Data protection:** card numbers and IBANs are sensitive personal financial data.
  - They are stored encrypted, so a copy of the database or a backup alone does not reveal them.
  - They are masked everywhere by default, and a full number is only sent to the screen when the
    user reveals it.
  - They never appear in logs or error messages.
  - Real numbers are never committed to the repository; test data uses made-up numbers only, as
    required by `CONTRIBUTING.md`.
- **NFR-2 Responsive:** the Settings section and the Dashboard widget work on phone and desktop.
- **NFR-3 Exact amounts:** the debt limit is stored and shown exactly to the cent, never rounded.
- **NFR-4 Access:** only signed-in users of the household can see or change cards.
- **NFR-5 Language:** UI text in English for now; field labels must be easy to translate to
  Croatian later.

## 6. Out of scope

- Card installments, one-off card charges, statement reconciliation, debt overview, and the
  ongoing-transaction state per card.
- Connecting income sources to accounts (later requirements).
- Renaming or removing issuer and bank list entries.
- Loans, leasing and savings accounts (other account kinds).
- Importing cards or transactions from a bank.
- Card PIN, CVV or any other secret card data; these are never stored.
- Multiple currencies.

## 7. Acceptance criteria

1. With no cards, the Dashboard shows the "cards need to be set up" empty state, and its action
   opens the Cards and accounts section in Settings.
2. The user can add a card with all attributes from section 3; a valid card then appears in
   Settings and on the Dashboard.
3. Saving is blocked, with a clear message, when:
   - a required field is missing;
   - a given card number or IBAN fails its check;
   - *Valid from* is given and *Valid through* is before it;
   - the debt limit is negative;
   - the custom name, card number or IBAN is already used.
4. The user can add a new issuer or bank from the form, and it is offered for the next card.
5. Card numbers and IBANs are masked in the list, on the Dashboard and in details until revealed.
6. Editing a card changes it in Settings and on the Dashboard.
7. Archiving a card hides it from the list (except the Archived filter) and from the Dashboard,
   and keeps its details readable.
8. Only Debit cards can be primary; several cards can be primary at once.
   Only Debit cards offer the monthly installments check box; Credit and Charge cards always allow
   installments and Prepaid cards never do.
9. A card whose *Valid through* has passed is shown as Expired and is hidden from the default list
   and the Dashboard; one ending within the expiring window is shown as Expiring soon.
10. Blocking a card shows it as Blocked, removes it from the Dashboard, and prevents any new
    activity; unblocking returns it to its date-based status.
11. Marking a card as expired cannot be undone. The card shows as Expired from that date, and the
    optional reason and replacement card are visible on its details.
12. Adding a replacement while expiring a card opens the Add form prefilled from the old card, and
    the two cards are linked after saving.
13. Blocking, expiring or archiving a primary account removes its primary mark after the user
    confirms.
14. A card can be saved without a card number and IBAN, and without *Valid from*. Such a card
    shows no numbers (or only the one given) and is valid until *Valid through*.
15. A Debit card with the monthly installments check box ticked shows that it allows installments
    in its details; saving the card keeps the check box.

## 8. Remarks (possible later, nice to have)

These are not part of this feature and are kept here so they are not forgotten.

- **Changing max debt limit:** the max debt limit is not always a fixed value; a bank can raise or
  lower it over time. A later version may keep the limit as a history of values with the date each
  one applies from, so that debt and remaining limit can be shown for any past or current month.
  For now the limit is one value that the user edits.

## Appendix 1: Shorter card entries on the Dashboard

- **Date:** 2026-10-07
- **Branch:** `feat/input_cash_flow`
- **Changes:** FR-9 (what each Dashboard entry shows)

Each entry of the Dashboard **Cards** widget shows only: custom name, primary marker, card status,
masked card number and/or IBAN (when given), and the account balance (current balance and expected
balance at the end of the month, see
[input-cash-flow.md](input-cash-flow.md) FR-11).

Issuer, owner (bank), type, valid through and max debt limit are **no longer shown on the
Dashboard**. They stay visible in the Settings list and on the card details.

The other rules of FR-9 (which cards appear, primary accounts first, selecting an entry opens the
card's details) are unchanged.


## Appendix 2: Billing dates and paying account

- **Date:** 2026-10-08
- **Branch:** `feat/credit_card_installments`
- **Changes:** section 3 (card attributes), FR-2 and FR-4 (add and edit), FR-1 and FR-3 (list and
  details)

Cards that allow monthly installments (A11) get five new attributes: **Statement date** and
**Payment date** (days of the month), which define the card's billing period, the **Paying
account** (a primary account, prefilled when exactly one is primary), **Connected to the paying
account** (whether the bank charges card payments directly from it), and **Max installments**
(12, 24 or 36 installments per purchase). They are set in the
Add and Edit card forms and shown in the Settings list and on the card details. Their rules are
described in [credit-card-installments.md](credit-card-installments.md) FR-1 and FR-2.

**Why:** card purchases and installments are collected per billing period and paid in one card
payment from an account on a known date, so each card needs these attributes.

## Appendix 3: Cards and accounts move from the Dashboard to the Cards screen

- **Date:** 2026-10-18
- **Branch:** `feat/design_refresh`
- **Changes:** FR-9, FR-10, FR-11 (where statuses are highlighted), Appendix 1

The **Cards** widget is removed from the Dashboard (see
[design-refresh.md](design-refresh.md) FR-9). Everything it showed moves to a new **Accounts** part
at the top of the **Cards** screen (see [design-refresh.md](design-refresh.md) FR-12):

- Where FR-9, FR-10 and Appendix 1 say "the Dashboard" or "the widget", read "the Accounts part of
  the Cards screen". Which cards appear, the order, what each entry shows, the empty states and
  opening a card's details are unchanged.
- FR-11: Blocked, Expired and Expiring soon are highlighted on the Cards screen and in Settings.
- The empty state action **Set up cards** still opens the Cards and accounts section in Settings.
