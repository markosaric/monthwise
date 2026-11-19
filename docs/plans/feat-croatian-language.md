# Plan: Croatian language

- **Branch:** `feat/croatian-language`
- **Status:** Draft
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
  - `messages/en.ts`: every English app text, grouped by area (`common`, `menu`, `dashboard`,
    `month`, `cards`, `loans`, `sharedCosts`, `cashFlow`, `settings`, `dataTransfer`,
    `validation`, `errors`, …). A text with values is a function, e.g.
    `installmentOf: (n: number, count: number) => \`${n} of ${count}\``, so values are typed.
  - `messages/hr.ts`: typed as `Messages` (the type of `en`), so a missing or extra text, or a
    function with different values, is a **compile error** (FR-9, criterion 9).
  - `plural(language, n, { one, few, other })` on top of `Intl.PluralRules`, for "1 rata / 2 rate /
    5 rata" (FR-5). English uses `one` and `other`.
  - `LanguageProvider` (in `App`) holds the language; `useT()` returns the messages of the current
    language, `useLanguage()` returns the language and `setLanguage`.
  - The Croatian messages are loaded on demand (`import()`), so an English user downloads nothing
    more, and a Croatian user only the texts (NFR-3). Until loaded, the provider keeps showing the
    previous language for that moment, never a mix.
- Every component replaces its literal texts (JSX text, `placeholder`, `title`, `aria-label`,
  toasts, confirm dialogs, chart legends and axis labels, `PageHeader` titles) with `t.<area>.<key>`.
  About 105 component files; done area by area (see Steps).
- **Form validation:** the zod schemas keep their structure, but each `message` becomes a message
  key (e.g. `'validation.required'`). The form error display translates the key with `t`, so the
  schemas stay module-level constants and switch language without being rebuilt.
- **Check for left-over English (NFR-1, criterion 9):** a Vitest test reads every non-test `.tsx`
  file with the TypeScript compiler API (already a dev dependency) and fails on JSX text, or on a
  string literal in `placeholder`, `title`, `aria-label`, `label` or toast calls, that contains
  letters. A short allow-list covers texts that are the same in both languages (e.g. "Monthwise",
  "Visa", "€").

### Front-end: choosing and remembering the language (FR-1, FR-2)
- Settings gets a **Language** section with two options, "English" and "Hrvatski" (each written in
  its own language). Choosing one switches at once; Settings has no form, so nothing typed is lost.
- The choice is kept in `localStorage` under `monthwise.language` (`en` | `hr`), read with the
  same safe pattern as `menuState.ts` (storage can be unavailable).
- With nothing stored: `hr` when the first `navigator.languages` entry starts with `hr`, otherwise
  `en`. Nothing is stored until the user chooses.
- On every change: `<html lang>` is set (`en` / `hr`), the tab title is set from the messages, and
  all TanStack queries are invalidated, so data with server-made texts (predefined names, "Card
  payments", loan descriptions) is fetched again in the new language (FR-3, FR-4).

### Front-end: formats (FR-5)
- Amounts, numbers and percents keep `APP_LOCALE = 'de-DE'` in both languages (`1.234,56 €`,
  `8,33 %`). Numeric dates stay `DD.MM.YYYY` (`DateInput`, `formatDayShort` unchanged).
- Only month and day names follow the language: `formatYearMonth` and the date formatting in
  `dataTransfer/format.ts` take the language and use `en-GB` or `hr-HR`
  ("November 2026" / "studeni 2026."; short "Nov" / "stu").

### API: language of the request (FR-3)
- The front-end's `openapi-fetch` client (`api/client.ts`) and the plain `fetch` calls in
  `api/dataTransfer.ts` send `Accept-Language: en` or `hr` with every request.
- `Program.cs` adds request localization with supported **UI** cultures `en` (default) and `hr`.
  Only `CurrentUICulture` follows the request; `CurrentCulture` stays as today, so parsing and
  formatting of amounts and dates on the server cannot change with the language (FR-5, FR-7).

### API: messages (FR-3)
- New resources in `Monthwise.Application`: `Localization/Messages.resx` (English) and
  `Messages.hr.resx` (Croatian), read through a small `IAppText` service (wrapping
  `IStringLocalizer`).
- **Domain errors** already have a stable code (`DomainError.Code`, about 200 codes, e.g.
  `name.required`). The English text in the domain stays as the fallback; the API's error mapping
  (problem details) looks up the code in the resources for the request's language. The few errors
  with values (e.g. "at most {MaxYears} years") get an optional `Args` on `DomainError`, used as
  `{0}` in the resource text.
- **FluentValidation** (`CardRequestValidator`): each rule gets `.WithErrorCode(...)` and is
  translated the same way.
- **General errors** (`DatabaseRuleExceptionHandler`, not found, conflicts, the fallback
  "Something went wrong") and the import/export results of `backup-and-restore` use the resources
  as well.
- **Texts the server makes up** for display (e.g. `NoAccountYet`, `CardPayments` in the dashboard
  and cash-flow groupings, loan payment descriptions like "Car lease, residual value", "18/60")
  come from `IAppText` instead of constants.

### API: predefined names (FR-4)
- Predefined rows already have fixed ids (seeded in the EF configurations). A table in
  `Application/Localization/PredefinedNames.cs` maps each id of a predefined input category,
  expense category (including the fixed Cash withdrawal), loan category and loan subcategory to its
  Croatian name. The English name stays in the database, so exports are unchanged (FR-7).
- Every response that shows such a name (lists, forms, months, dashboard, charts, shared costs)
  goes through one helper `DisplayName(id, storedName, isPredefined)`. Card issuers and banks are
  not in the table (proper names).
- **Duplicate check:** when the user adds or renames a category or subcategory, the name is
  compared (with the rule of [name-comparison.md](../feature-requirements/name-comparison.md))
  against the English **and** Croatian name of every predefined entry of that list, and refused
  with the usual "name already in use" message (criterion 6). Existing user data is not changed.

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
1. `src/i18n/` (provider, messages types, plurals, storage, browser detection), Settings language
   section, `<html lang>` and tab title; `formatYearMonth` and date formats by language.
2. `Accept-Language` on API calls; server request localization (UI culture only); resources and
   `IAppText`; error mapping by code; FluentValidation codes; general errors.
3. Server-made texts and predefined names with the duplicate check.
4. Move the front-end texts into `en.ts`, area by area: layout and menu, Dashboard, Month and bills,
   expenses, cash flow, cards and card purchases, revolving, loans, shared costs, savings,
   settings, backup and restore, error screens. Zod messages become keys.
5. Write `hr.ts` and `Messages.hr.resx` using the terms of Appendix A of the requirements; add the
   left-over English test.
6. Check every screen in Croatian on phone and desktop for cut-off or overlapping text (NFR-2) and
   fix the layout where needed.
7. Croatian user manual with screenshots; language links; English manual paragraph; README.
8. Update [INIT.md](../INIT.md) §4 with a "Languages" row (how texts are added in both languages),
   and CONTRIBUTING (FR-9: a new text is added to `en.ts` and `hr.ts`, and server texts to both
   resource files, in the same branch). Set this plan to Done.

## Tests
- **Unit tests (front-end, Vitest):**
  - the language is taken from storage, then from the browser (`hr`, `hr-HR`, `en-US`, `de`), and
    a broken storage falls back without errors;
  - changing the language updates the rendered text, `<html lang>` and the title, and keeps typed
    input of a form on screen (criterion 1);
  - `plural` for Croatian 1, 2, 5, 11, 21, 22 and English;
  - `formatYearMonth` in both languages; amounts and dates identical in both (criterion 7);
  - the left-over English test (criterion 9).
- **Unit tests (back-end):** every key in `Messages.resx` exists in `Messages.hr.resx` and the other
  way round; every `DomainError` code used in the source has a resource entry (found by reading the
  source files); every predefined id has a Croatian name.
- **API tests:** the same invalid request returns an English title with `Accept-Language: en` (or
  none) and a Croatian one with `hr`; predefined categories come back in the requested language,
  banks do not change; adding "Plaća" as an input category is refused (criterion 6); an amount sent
  with `Accept-Language: hr` is stored the same as with `en`; an export made with `hr` equals one
  made with `en` and imports under either (criterion 8).
- **Manual checks:** every menu screen, form, dialog and tooltip in Croatian on a phone and a
  desktop (criteria 4, 11); reload and reopen keep the language (criterion 2); a browser set to
  Croatian on a fresh device opens in Croatian (criterion 3); Croatian letters in charts and the
  manual (NFR-5); screen reader reads Croatian labels (NFR-4).

## Open questions
- None. FR-8 was changed on this branch (decided 2026-11-19): the manuals link each other and the
  README links both; there is no help link in the app.
