# Plan: Croatian language

- **Branch:** `feat/croatian-language`
- **Status:** Done
- **Date:** 2026-11-19
- **Requirements:** [croatian-language.md](../feature-requirements/croatian-language.md) (approved),
  and the NFR-5 appendices it adds:
  [credit-card-settings.md Appendix 5](../feature-requirements/credit-card-settings.md#appendix-5-ui-text-in-english-and-croatian),
  [input-cash-flow.md Appendix 4](../feature-requirements/input-cash-flow.md#appendix-4-ui-text-in-english-and-croatian),
  [credit-card-installments.md Appendix 3](../feature-requirements/credit-card-installments.md#appendix-3-ui-text-in-english-and-croatian),
  [monthly-bills-and-expenses.md Appendix 3](../feature-requirements/monthly-bills-and-expenses.md#appendix-3-ui-text-in-english-and-croatian),
  [loans-and-leases.md Appendix 3](../feature-requirements/loans-and-leases.md#appendix-3-ui-text-in-english-and-croatian),
  [shared-bills.md Appendix 1](../feature-requirements/shared-bills.md#appendix-1-ui-text-in-english-and-croatian),
  [revolving-credit-card.md Appendix 2](../feature-requirements/revolving-credit-card.md#appendix-2-ui-text-in-english-and-croatian),
  [design-refresh.md Appendix 5](../feature-requirements/design-refresh.md#appendix-5-ui-text-in-english-and-croatian)

## Goal
The user picks English or Hrvatski in Settings, and every text the app writes (screens, forms,
messages from the server, predefined category names, month names) appears in that language. Each
device remembers its own choice. A Croatian user manual sits next to the English one.

## Requirement changes
The appendices listed above replace NFR-5 "UI text in English for now" in eight earlier
requirement documents with: "UI text in English and Croatian; the user chooses the language in
Settings. See croatian-language.md."

## Scope
- In scope: FR-1 to FR-9, NFR-1 to NFR-5 of the requirements; every screen in `routes.tsx`, every
  message the API returns to the user, the predefined names, the user manual.
- Out of scope: section 5 of the requirements (other languages, user text, formats that change
  with the language, developer docs, per-user language).

## Design

### Front-end: translations (FR-3, FR-6, FR-9, NFR-1)
- No new package. A small in-house module `src/i18n/` is enough for two languages and keeps the
  dependency list short (production packaging, Appendix 4):
  - `language.ts`: the language (`en` | `hr`), read once when the page loads (see below).
  - `texts.ts`: `localized(en, hr)` picks one of two text objects for the page's language;
    `Translation<T>` is the deep type of the English texts, so a missing or extra Croatian text, or
    a function with different values, is a **compile error** (FR-9, criterion 9);
    `plural(count, { one, few, other })` on top of `Intl.PluralRules`, for "1 rata / 2 rate /
    5 rata" (FR-5). English uses `one` and `other`.
- The texts stay where each area already keeps them: every `components/<area>/texts.ts` holds the
  English texts and gets a `texts.hr.ts` next to it; texts that were written into components and
  pages (layout, menu, error screens, loading and error messages, Month, Settings, reports) moved
  into the new `components/common/texts.ts` / `texts.hr.ts`. A text with values is a function,
  so values are typed.
- **Changed from the draft:** no `LanguageProvider` / `useT()` and no on-demand loading of the
  Croatian texts. Changing the language stores it and reloads the page. Module-level constants
  (zod schemas with their messages, menu items, chart labels) are then always in the right
  language without being rebuilt, and no screen can show a mix. Settings has nothing to type, so
  the reload loses nothing (criterion 1). Both languages' texts are in the bundle; they are small
  and the first load is not noticeably slower (NFR-3).
- **Check for left-over English (NFR-1, criterion 9):** `i18n/noHardCodedTexts.test.ts` reads every
  non-test `.ts`/`.tsx` file with the TypeScript compiler API and fails on JSX text, on a string in
  an attribute people read or hear (`title`, `label`, `placeholder`, `aria-label`, `alt`,
  `description`, …), on a toast text, and on a template string with words shown in JSX. Texts files
  are skipped (the compiler checks them). A short allow-list covers "Monthwise", "M", "€", "%".

### Front-end: choosing and remembering the language (FR-1, FR-2)
- Settings gets a **Language** card with two options, "English" and "Hrvatski" (each written in
  its own language, with its own `lang`). Choosing the other language stores it and reloads.
- The choice is kept in `localStorage` under `monthwise.language` (`en` | `hr`); storage that is
  unavailable falls back to the browser language without errors.
- With nothing stored: `hr` when the first `navigator.languages` entry is Croatian, otherwise `en`.
  Nothing is stored until the user chooses.
- `<html lang>` is set when the app starts.

### Front-end: formats (FR-5)
- Amounts, numbers and percents keep `APP_LOCALE = 'de-DE'` in both languages (`1.234,56 €`,
  `8,33 %`). Numeric dates stay `DD.MM.YYYY`.
- Only month names follow the language: `formatYearMonth` and the data and backups dates use
  `dateLocale()`, `en-GB` or `hr-HR` ("November 2026" / "studeni 2026.").
- **Backup and restore:** the word typed to confirm an import is `ZAMIJENI` in Croatian; the front
  end still sends the API's fixed word `REPLACE`, so the API protocol does not change.

### API: language of the request (FR-3)
- The front-end's `openapi-fetch` client (`api/client.ts`) and the plain `fetch` calls in
  `api/dataTransfer.ts` send `Accept-Language: en` or `hr` with every request.
- **Changed from the draft:** a small middleware (`Api/Hosting/RequestLanguage.cs`) instead of
  ASP.NET Core request localization: it sets only `CurrentUICulture` (Croatian when the most
  preferred language is `hr`, otherwise English) and restores it after the request.
  `CurrentCulture` stays as today, so parsing and formatting of amounts and dates on the server
  cannot change with the language (FR-5, FR-7).

### API: messages (FR-3)
- **Changed from the draft:** no `.resx` files and no `Args` on `DomainError`. Several codes share
  one message and several messages hold values written in the code (lengths, day ranges, a
  release), so the English message itself is the key: `Application/Localization/ServerTexts.cs`
  holds the Croatian text of every fixed message and a pattern for each message with values.
  `ServerTexts.Translate(english)` returns Croatian for a Croatian request, otherwise the English
  text. Domain errors and their codes are unchanged.
- It is applied where messages leave the API: the problem details mapping
  (`ResultHttpExtensions`, which also covers FluentValidation errors), `MonthParameter`,
  `SupportedRangeFilter`, `DatabaseRuleExceptionHandler`, and the migration and import gates.
- **Texts the server makes up** for display (`NoAccountYet`, `CardPayments`, "Unknown" for a
  missing item, "Car lease, residual value") go through `ServerTexts` as well.

### API: predefined names (FR-4)
- Predefined rows already have fixed ids (seeded in the EF configurations).
  `Application/Localization/PredefinedNames.cs` maps each id of a predefined input category,
  expense category (including the fixed Cash withdrawal), loan category and loan subcategory to its
  English and Croatian name. The English name stays in the database, so exports are unchanged
  (FR-7).
- `PredefinedNames.Display(id, storedName)` shows the Croatian name for a Croatian request while
  the stored name is still the predefined English one; a renamed entry is the user's own text and
  is shown as typed. It is used by the category lists and every ledger that shows a category name
  (months, dashboard, reports, charts, card payments, revolving cards, loans). Card issuers and
  banks are not in the table (proper names).
- **Duplicate check:** adding or renaming a category or subcategory is refused with the usual
  "name already in use" message when the name equals (name-comparison rule: case ignored, accents
  kept) another entry's Croatian predefined name, in addition to the existing check against the
  stored (English) names (criterion 6). Existing user data is not changed.

### User manual (FR-8)
- `docs/user-manual/hr/index.html`: the same sections as the English manual, in Croatian, with
  `lang="hr"`; screenshots in `docs/user-manual/images/hr/`, taken from the app in Croatian with
  the same made-up demo data as the English ones.
- Each manual links to the other at the top. The README links both.
- The English manual gets one short paragraph on the Language setting (FR-1).

### Data model and migrations
- None. The language is stored only in the browser; predefined names keep their English name in
  the database.

## Steps
1. `src/i18n/` (language, texts, plurals, storage, browser detection), Settings language card,
   `<html lang>`; `formatYearMonth` and dates by language.
2. `Accept-Language` on API calls; request language middleware (UI culture only); `ServerTexts`
   applied to problem details, month and date checks, database rule errors and gates.
3. Server-made texts and predefined names with the duplicate check.
4. Croatian `texts.hr.ts` for every area and the new common texts, using the terms of Appendix A
   of the requirements; texts written into components moved into texts files; the left-over
   English test.
5. Check the screens in Croatian on phone and desktop for cut-off or overlapping text (NFR-2) and
   fix the layout where needed.
6. Croatian user manual with screenshots; language links; English manual paragraph; README.
7. Update [INIT.md](../INIT.md) §4 with a "Languages" row and CONTRIBUTING (FR-9: a new text is
   added to `texts.ts` and `texts.hr.ts`, and server texts to `ServerTexts`, in the same branch).
   Set this plan to Done.

## Tests
- **Unit tests (front-end, Vitest):** `i18n/language.test.ts`: the language is taken from storage,
  then from the browser (`hr`, `hr-HR`, `en-US`, `de`), and a broken storage falls back without
  errors; `plural` for Croatian 1, 2, 4, 5, 11, 12, 21, 22 and English; `formatYearMonth` in both
  languages; amounts and dates identical in both (criterion 7).
  `i18n/croatianApp.test.tsx`: the menu and Settings in Croatian, `Accept-Language: hr` on every
  request, choosing English stores it and reloads, choosing the active language does not reload.
  `i18n/noHardCodedTexts.test.ts`: the left-over English test (criterion 9).
- **Unit tests (back-end, `LocalizationTests`):** every `DomainError` in the code (found by
  reflection) and every message written in the source (found by reading the source files) has a
  Croatian text; messages with values, the import's name lists and the server-made names have one;
  every predefined id of the seed data has its English seed name and a Croatian name;
  `Accept-Language` parsing.
- **API tests (`CroatianEndpointTests`):** the same invalid request returns an English message with
  `Accept-Language: en` (or none) and a Croatian one with `hr`; a refused month is explained in
  Croatian; predefined categories come back in the requested language, banks do not change; a
  renamed predefined category keeps the user's name; adding "Plaća", "Salary", "Namirnice",
  "Leasing" or "Stambeni kredit" is refused, "Placa" is not (criterion 6).
- Export and import (criterion 8) read and write stored names only and were not changed; the
  existing backup and restore tests cover them.
- **Manual checks:** the screens in Croatian on a phone and a desktop (criteria 4, 11); reload and
  reopen keep the language (criterion 2); a browser set to Croatian on a fresh device opens in
  Croatian (criterion 3); Croatian letters in charts and the manual (NFR-5).

## Open questions
- None. FR-8 was changed on this branch (decided 2026-11-19): the manuals link each other and the
  README links both; there is no help link in the app.
