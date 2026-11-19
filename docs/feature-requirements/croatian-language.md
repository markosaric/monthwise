# Requirements: Croatian language

- **Branch:** `feat/croatian-language`
- **Status:** Approved (2026-11-19)
- **Date:** 2026-11-19

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-croatian-language.md`) is written after these
> requirements are approved.

## 1. Goal

Monthwise is in English only. The household it is built for lives in Croatia, and the old Numbers
workbook it replaces used Croatian terms. Earlier requirements already asked for texts that are
"easy to translate to Croatian later" (NFR-5 in several documents).

This feature adds **Croatian** as a second language of the app. The user can switch between
English and Croatian at any time, and everything the app writes on screen then appears in the
chosen language. English stays available and works as before.

## 2. Terms

| Term | Meaning |
|---|---|
| Language | English or Croatian (*hrvatski*). |
| App text | Every text the app itself writes: menu, page titles, buttons, field labels, hints, tooltips, empty states, confirmations, validation and error messages, toasts, chart labels and legends, month and day names, and descriptions the app makes up (e.g. "Card payments", "Car lease, residual value"). |
| User text | Everything the user typed: names of cards, payers, participants, loans, bills and categories they added, descriptions and notes. |
| Predefined name | A name the app ships with and the user cannot rename, e.g. the input category "Salary", the expense category "Electricity", the loan subcategory "Mortgage". |
| Proper name | A predefined name that is the same in every language: card issuers (Visa, Mastercard, …) and banks (ZaBa, PBZ, …). |

## 3. Functional requirements

**FR-1 Choosing the language**
- Settings has a **Language** choice with two options, shown in their own language: "English" and
  "Hrvatski".
- The choice takes effect at once, without leaving Settings and without losing anything typed.
- The language is chosen only in Settings; there is no language switch elsewhere in the app.

**FR-2 Which language is used**
- The first time the app is opened on a device, it uses Croatian when the browser's preferred
  language is Croatian, and English otherwise.
- After the user chooses a language, the app remembers it **on that device and browser** and uses
  it every time it is opened there, until the user changes it.
- The choice is not shared between devices: the app has no user accounts yet (sign-in is planned,
  [INIT.md](../INIT.md) §4). When sign-in is added, it may move the choice to the user.

**FR-3 App text in the chosen language**
- All app text (section 2) appears in the chosen language, on every screen, form, dialog, tooltip,
  toast and chart. No screen mixes the two languages, except for user text and proper names.
- Messages that come from the server (validation errors, "not found", conflicts, import and export
  results) appear in the chosen language too.
- The text read by screen readers (labels, descriptions, status announcements) is in the chosen
  language, and the page declares its language, so screen readers pronounce it correctly.
- The browser tab title is in the chosen language.

**FR-4 Predefined names**
- Predefined names of input categories, expense categories, loan categories and loan subcategories
  appear in the chosen language, wherever they are shown: lists, forms, filters, charts and the
  dashboard.
- Proper names (card issuers, banks) are not translated.
- User text is never translated and is shown exactly as typed. A category the user added keeps its
  name in both languages.
- A name the user types is checked for duplicates against the predefined names in **both**
  languages, so the user cannot add "Plaća" next to the predefined "Salary".

**FR-5 Numbers, amounts and dates**
- Amounts and numbers look the same in both languages, as today: `1.234,56 €`, `8,33 %`.
- Numeric dates look the same in both languages, as today: entered and shown as `DD.MM.YYYY`.
- Month and day names follow the language: "November 2026" in English, "studeni 2026." in
  Croatian; short forms on chart axes likewise ("Nov" / "stu").
- Croatian text uses correct grammar where a number or a month is part of a sentence (e.g. "1
  rata", "2 rate", "5 rata"; "do 15. studenoga").

**FR-6 Croatian terms**
- The Croatian text uses the everyday banking terms of Croatian banks and the terms already noted
  in the earlier requirements (e.g. *uplatitelj*, *datum dospijeća*, *saldo*, *rata*,
  *podizanje gotovine*, *sudionik u troškovima*).
- The same English term is always translated with the same Croatian term. The main terms are
  listed in [Appendix A](#appendix-a-key-terms), approved with these requirements.

**FR-7 What stays the same**
- The data does not change with the language: switching never changes amounts, dates, names or
  anything stored.
- Exported backup files are the same whichever language is chosen, and a file exported in one
  language imports in the other.
- Addresses of the screens (links, bookmarks) are the same in both languages.

**FR-8 User manual**
- The user manual is available in English and Croatian, with the same content.
- Each manual links to the other language at its top, and the README links both. The app itself
  has no help link (an in-app help link would be a feature of its own).
- The Croatian manual's text is in Croatian; its screenshots show the app in Croatian.

**FR-9 New texts**
- Every app text added from now on is added in both languages in the same branch. A screen with
  text in only one language counts as not finished.

## 4. Non-functional requirements

- **NFR-1 Complete:** no app text is left in English when Croatian is chosen (checked
  automatically, see acceptance criterion 9).
- **NFR-2 Layout:** Croatian texts are often longer than English ones; no text is cut off, overlaps
  or breaks the layout on phone, tablet or desktop.
- **NFR-3 Speed:** switching the language takes effect at once; the first load of the app is not
  noticeably slower than today.
- **NFR-4 Accessible:** as FR-3; the same accessibility as today in both languages.
- **NFR-5 Character set:** Croatian letters (č, ć, đ, š, ž and capitals) show correctly everywhere,
  including in the fonts, charts and the manual.

## 5. Out of scope

- Languages other than English and Croatian (the solution should not make a third language hard,
  but adds none).
- Translating user text.
- Currencies other than EUR, or amount and date formats that change with the language.
- Translating the developer documentation (README, CONTRIBUTING, INIT, requirements, plans, ADRs),
  code, logs and server-side technical messages that the user never sees.
- A language per user (waits for sign-in) or per household.

## 6. Acceptance criteria

1. Settings offers "English" and "Hrvatski"; choosing one changes the screen at once without
   losing anything typed.
2. The chosen language is used again after a reload and after closing and reopening the browser on
   the same device.
3. On a device that has never chosen a language, a browser set to Croatian opens the app in
   Croatian; any other browser language opens it in English.
4. With Croatian chosen, every screen in the menu, every form, dialog and tooltip, and the error
   shown for an invalid entry are in Croatian.
5. Predefined categories and loan subcategories appear in the chosen language; banks, card issuers
   and names the user typed look the same in both languages.
6. Adding a category named like a predefined one in either language is refused as a duplicate.
7. Amounts and numeric dates look identical in both languages; month names follow the language.
8. A backup exported in English imports in Croatian with no difference, and the other way round.
9. An automatic check fails when an app text exists in only one language.
10. Each manual links to the other language, and the README links both manuals.
11. Long Croatian texts are not cut off on a phone.

## 7. Changes to earlier requirements

These requirements change requirements already merged to `main`. They are not rewritten; each gets
an appendix in this branch that replaces its **NFR-5 Language** ("UI text in English for now") with
"UI text in English and Croatian; see [croatian-language.md](croatian-language.md)":

- [credit-card-settings.md](credit-card-settings.md)
- [input-cash-flow.md](input-cash-flow.md)
- [credit-card-installments.md](credit-card-installments.md)
- [monthly-bills-and-expenses.md](monthly-bills-and-expenses.md)
- [loans-and-leases.md](loans-and-leases.md)
- [shared-bills.md](shared-bills.md)
- [revolving-credit-card.md](revolving-credit-card.md)
- [design-refresh.md](design-refresh.md)

## Appendix A: Key terms

| English | Croatian |
|---|---|
| Dashboard | Pregled |
| Month | Mjesec |
| Input / Inputs | Uplata / Uplate |
| Output / Outputs | Isplata / Isplate |
| Month result | Rezultat mjeseca |
| Still to pay | Preostalo za platiti |
| Overall balance | Ukupno stanje |
| Balance | Saldo |
| Cash flow | Novčani tok |
| Payer | Uplatitelj |
| Issue date | Datum računa |
| Expected date | Datum naplate |
| Received | Primljeno |
| Card | Kartica |
| Primary account | Tekući račun |
| Card payment | Plaćanje kartice |
| Statement date | Datum obračuna |
| Payment date / Due date | Datum dospijeća |
| Purchase | Kupnja |
| Installment | Rata |
| Paid in full | Jednokratno |
| Revolving card | Revolving kartica |
| Debt | Dug |
| Interest | Kamata |
| Minimum payment | Minimalna uplata |
| Bill | Račun |
| Recurring bill | Ponavljajući račun |
| Expense | Trošak |
| Cash withdrawal | Podizanje gotovine |
| Category | Kategorija |
| Loan | Kredit |
| Lease | Leasing |
| Residual value | Ostatak vrijednosti |
| Shared costs | Zajednički troškovi |
| Participant | Sudionik u troškovima |
| Share | Udio |
| Savings | Štednja |
| Settings | Postavke |
| Export / Import | Izvoz / Uvoz |
