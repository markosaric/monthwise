# Plan: Card and bank account settings

- **Branch:** `feat/credit_card_settings`
- **Status:** Draft
- **Date:** 2026-10-04
- **Requirements:** [credit-card-settings.md](../feature-requirements/credit-card-settings.md) (approved)

## Goal
The user can add, view, edit, block, unblock, mark as expired and archive their cards and bank
accounts in **Settings → Cards and accounts**, and sees all valid cards (or a "set up your cards"
message) in a **Cards** widget on the Dashboard. Card numbers and IBANs are stored encrypted.

## Scope
- In scope:
  - `Card` domain entity with its rules and status calculation (FR-5 to FR-8, FR-11).
  - `CardNumber` and `Iban` value objects with checksum validation.
  - Encryption of card numbers and IBANs at rest, masking, and an explicit reveal (NFR-1, FR-3).
  - Extendable issuer and bank lists (A2, A3).
  - Database tables and migration.
  - API endpoints for cards, the Dashboard view, and the two lists.
  - Settings section: list with filters, details with reveal, add/edit form, action dialogs.
  - Dashboard Cards widget with both empty states.
  - Tests at every layer.
- Out of scope:
  - **Ongoing transactions:** they do not exist yet. Card visibility goes through one Application
    interface (`ICardActivityQuery`) that reports no ongoing transactions for now. The installments
    feature implements it later, and expired or blocked cards with running installments then show
    up again (FR-1, FR-9).
  - **Access control** (NFR-4): there is no authentication yet. The endpoints go behind auth when
    the authentication plan lands; nothing in this plan blocks that.
  - Households: there is one implicit household until authentication adds `household`/`user`.
    The new tables get a `household_id` then.
  - Renaming or removing issuer and bank entries, and Croatian UI texts. For NFR-5, card labels
    and messages are kept in one place in the front-end (`src/components/cards/texts.ts`), ready
    to be translated.

## Design

### Key rules from the requirements
| Requirement | Rule used in this plan |
|---|---|
| A8 | Types: `Debit`, `Charge`, `Credit`, `Prepaid`. |
| A2, A3 | Issuers and banks are database tables seeded with the predefined entries; the user adds entries from the card form. |
| A4, A5 | Full card number and full IBAN, both optional but at least one required, stored encrypted (see "Protecting card numbers and IBANs"). |
| A6, A7, FR-11 | Validity is month/year (`YearMonth`). "Expiring soon": the last day of *Valid through* is within 2 months of today. |
| A10 | Debt limit is optional for every type, ≥ 0 when given. |
| A1 | Custom names are unique, case-insensitive. |
| FR-1, FR-5 | No delete; **Archive** instead, and it cannot be undone. The default list hides expired cards without ongoing transactions. |
| FR-6 | Several cards can be primary; only Debit cards can be primary. |
| FR-9 | The Dashboard shows Active and Expiring soon cards; blocked and expired cards only with ongoing transactions. |
| FR-7, FR-8 | Ongoing transactions stay on the original card; nothing moves on block or expire. |
| FR-7, FR-8 | Unblock exists; manual expiry is final (no undo endpoint). |
| FR-8 | Expire can link an existing replacement, or open the Add form prefilled from the old card; saving links the two. |

### Domain / data model changes (`Monthwise.Domain/Cards`)
- Value objects:
  - `CardNumber`: 13 to 19 digits, spaces allowed on input, Luhn check.
  - `Iban`: ISO 13616 format, country length table, mod-97 check.
  - Both expose `Last4` (and `Iban.CountryCode`) for masking. Their `ToString()` returns the
    **masked** form, so a stray log line or exception message never prints a full number.
- Enums: `CardType`, `CardStatus` (`Archived`, `Blocked`, `Expired`, `NotYetValid`,
  `ExpiringSoon`, `Active`).
- `CardIssuer` and `Bank` entities (Guid id, `Name`, `IsPredefined`).
- `Card` entity (Guid v7 id). Fields:
  - `Name`, `IssuerId`, `BankId`, `CardNumber?`, `Iban?`;
  - `ValidFrom` and `ValidThrough` (`YearMonth`), `Type`, `IsPrimary`, `DebtLimit` (`Money?`);
  - `BlockedOn?`, `BlockReason?`, `ExpiredOn?` (manual expiry), `ExpiryReason?`;
  - `ArchivedOn?`, `ReplacedByCardId?`, `CreatedAt`, `UpdatedAt`.
- Behaviour on the entity, so the rules live in one place and are unit-tested without a database:
  - `Create(...)` and `Update(...)` validate the rules in section 3 of the requirements: at least
    one number, validity order, debt limit ≥ 0, primary only for Debit. `Update` fails while
    blocked, manually expired or archived.
  - `Block(today, reason)`, `Unblock()`, `MarkExpired(today, reason, replacement?)` (final), and
    `Archive(today)`. Block, expire and archive clear `IsPrimary`.
  - `StatusOn(DateOnly today)` returns the status using the priority order of FR-11.
  - `CanBeUsedOn(today)`: true only for `Active` and `ExpiringSoon`; later features call this.
  - `IsListedByDefault(today, hasOngoingTransactions)` and
    `IsShownOnDashboard(today, hasOngoingTransactions)` implement FR-1 and FR-9.
- Domain errors are returned as results (error code + field), not exceptions, so Application can
  turn them into a `ValidationProblem` or `409 Conflict`.
- "Today" comes from `TimeProvider` (injected in Application), so tests can fix the date.

### Protecting card numbers and IBANs (NFR-1)
- **Encryption at rest:** the Infrastructure layer encrypts `CardNumber` and `Iban` with AES-256-GCM
  before they are written, through an EF Core value converter backed by an `ISensitiveDataCipher`
  service.
  - Each value gets a random nonce. The stored text is `v1:<keyId>:<base64(nonce|ciphertext|tag)>`,
    so keys can be rotated later without a schema change.
  - A database dump or backup alone shows only ciphertext.
- **Keys:** the encryption key and a separate hashing key (256-bit, base64) come from configuration
  (`SensitiveData:EncryptionKeys`, `SensitiveData:HashKey`).
  - Locally they are stored in .NET user secrets, never in `appsettings*.json` or the repository
    (CONTRIBUTING, "Secrets and sensitive data"). In CI they come from throwaway keys generated in
    the workflow.
  - The API refuses to start when the keys are missing.
  - README gets the command to generate the keys, and a warning that losing them makes stored
    numbers unreadable, so they must be backed up separately from the database.
- **Uniqueness without decrypting:** a keyed HMAC-SHA256 of the normalised number is stored next to
  each encrypted value (`card_number_hash`, `iban_hash`). A partial unique index on it, excluding
  archived cards, enforces "the same number cannot be registered twice".
- **Masking columns:** `card_number_last4`, `iban_country` and `iban_last4` are stored unencrypted,
  so lists and the Dashboard never decrypt anything.
- **API:** list, details and Dashboard responses carry only the masked parts.
  - A full number comes from `GET /api/cards/{id}/numbers/{cardNumber|iban}`, one number per call,
    called only when the user presses Reveal next to that number (FR-3). It is sent with
    `Cache-Control: no-store`.
  - The front-end keeps the revealed values in component state only (never in the TanStack Query
    cache or storage) and drops them when the user leaves the page.
- **Logs and errors:** HTTP body logging stays off. Validation messages never echo the value
  ("Card number is not valid", not the number). A test asserts that a failed create with a
  made-up number does not contain the number in the response or in captured logs.
- **Test data:** only published test numbers, e.g. `4111 1111 1111 1111` and documented example
  IBANs.

### Database migrations
- Migration `AddCards` creates three tables (snake_case, per the existing convention):
  - `card_issuer` and `bank`: `id`, `name`, `is_predefined`, with a unique index on `lower(name)`.
    Seeded with the predefined entries.
  - `card`:
    - `issuer_id` and `bank_id` foreign keys;
    - encrypted `card_number` and `iban` (text), their `_hash` and masking columns;
    - `valid_from` and `valid_through` as `date` (the existing `YearMonth` convention);
    - `type` as text, `is_primary`, `debt_limit_cents bigint null`;
    - block, expiry and archive dates and reasons;
    - `replaced_by_card_id`, a self-referencing foreign key.
- Constraints that protect the data even if a rule is bypassed:
  - unique index on `lower(name)` (A1);
  - partial unique indexes on `card_number_hash` and `iban_hash` where `archived_on is null`;
  - check constraints:
    - `card_number is not null or iban is not null`;
    - `valid_through >= valid_from`;
    - `debt_limit_cents >= 0`;
    - `not is_primary or type = 'Debit'`.
- Update `docs/INIT.md` section 3: replace `card` as a kind of `account` with the new `card`,
  `card_issuer` and `bank` tables, and point `installment_plan` and `card_statement` at `card_id`.

### API changes (endpoints, DTOs)
New groups in `Api/Endpoints/CardEndpoints.cs` (tag `Cards`). Each use case gets one Application
handler in `Monthwise.Application/Cards`, and requests are checked with FluentValidation validators.

| Method and path | Purpose | Notes |
|---|---|---|
| `GET /api/cards?show=default\|expired\|archived` | Settings list (FR-1) | primary first, then by name |
| `GET /api/cards/{id}` | Details (FR-3) | masked numbers, history, replacement link |
| `GET /api/cards/{id}/numbers/{cardNumber\|iban}` | Reveal (FR-3) | one full number, `no-store` |
| `POST /api/cards` | Add (FR-2) | optional `replacesCardId` links a replacement (FR-8) |
| `PUT /api/cards/{id}` | Edit (FR-4) | omitted number fields keep their stored value; `409` while blocked, expired by the user, or archived |
| `POST /api/cards/{id}/block` | Block (FR-7) | body `{ reason? }` |
| `POST /api/cards/{id}/unblock` | Unblock (FR-7) | |
| `POST /api/cards/{id}/expire` | Mark as expired (FR-8) | body `{ reason?, replacementCardId? }`; final |
| `POST /api/cards/{id}/archive` | Archive (FR-5) | |
| `GET /api/cards/dashboard` | Dashboard widget (FR-9, FR-10) | valid cards, plus `hasAnyCards` for the empty states |
| `GET /api/card-issuers`, `POST /api/card-issuers` | Issuer list (A2) | `409` on a duplicate name |
| `GET /api/banks`, `POST /api/banks` | Bank list (A3) | `409` on a duplicate name |

- DTOs:
  - `CardResponse`:
    - all attributes, with `cardNumberLast4`, `ibanMasked` and `debtLimitCents`;
    - `validFrom` and `validThrough` as `yyyy-MM`;
    - the computed `status` and `canBeUsed`;
    - block, expiry and archive dates and reasons, and the replacement card.
  - `CardRequest` for create and update.
  - `CardNumbersResponse` for the reveal.
  - Enums are serialised as strings.
- Errors:
  - `400 ValidationProblem` with field keys matching the form fields;
  - `404` for an unknown card;
  - `409` for state conflicts and duplicates that got past validation.
- Regenerate `backend/openapi/monthwise.json` and `frontend/src/api/schema.d.ts` (ADR 0003).

### Front-end changes (pages, components)
- Routes:
  - `/settings` becomes a list of sections, the first being **Cards and accounts**.
  - `/settings/cards` for the list, `/settings/cards/new` to add, `/settings/cards/:id` for details,
    and `/settings/cards/:id/edit` to edit.
  - Pages instead of modal forms, so the form is comfortable on a phone.
- `src/api/cards.ts`: TanStack Query hooks (`useCards`, `useCard`, `useDashboardCards`,
  `useCardIssuers`, `useBanks`, and mutations). Mutations invalidate the `cards` queries so
  Settings and the Dashboard stay in sync. The reveal is a plain call, not a cached query.
- Components in `src/components/cards/`:
  - `CardForm`: add/edit form. It shows field errors from the API `ValidationProblem`, and the same
    rules (including Luhn and IBAN checks) run on the client. The primary switch is shown only for
    Debit. On edit, the number fields are empty with a "leave empty to keep •••• 1234" hint.
  - `ListSelectWithAdd`: select with an "Add new…" option, used for issuer and bank.
  - `CardList`: Default, Expired and Archived filters, and the empty state with an
    **Add card** action (FR-1).
  - `CardListItem`, `CardStatusBadge` (colour per status), and `CardDetails` (status history and
    replacement link).
  - `RevealableNumber`: one per number, each with its own Reveal button.
  - `ConfirmCardActionDialog`: block, unblock and archive, with an optional reason. It names the
    card, warns when the action removes the primary mark, and says archiving cannot be undone.
  - `ExpireCardDialog`: warns that expiry is final, offers an optional replacement picker, and
    "Add replacement". That opens the Add page prefilled with the old card's name, issuer, owner,
    type, primary mark and debt limit (FR-8).
  - `CardsWidget`: the Dashboard widget with both empty states (FR-10).
- New shadcn/ui components: `input`, `label`, `select`, `switch`, `textarea`, `alert-dialog`,
  `sonner` (save confirmations).
- Forms: **react-hook-form** with **zod** for client-side checks. This is a new dependency, chosen
  because the card form is the first of many forms (recurring items, installments). The server
  stays the source of truth.
- `src/lib/money.ts`: add `parseEuroInput("1.234,56") → 123456` (cents) for the debt limit field,
  next to `formatCents` (ADR 0002).
- `src/lib/cardNumbers.ts`: Luhn and IBAN checks, plus input formatting in groups of four.
- Validity inputs use `<input type="month">`, which already yields `yyyy-MM`.

## Steps
1. Commit the approved requirements and this plan.
2. Domain: `CardNumber`, `Iban`, enums, `CardIssuer`, `Bank`, and the `Card` entity with its
   rules, status and visibility logic, plus unit tests.
3. Infrastructure:
   - `ISensitiveDataCipher` (AES-GCM and HMAC) with key configuration and a startup check;
   - EF Core configurations and converters;
   - the `AddCards` migration with seed data, indexes and checks;
   - `ICardActivityQuery`, which reports no ongoing transactions for now.
4. Application: request and response DTOs, validators, and handlers for each use case, using
   `TimeProvider`.
5. Api: `CardEndpoints`, error mapping, `no-store` on the reveal, and OpenAPI metadata. Regenerate
   the OpenAPI document and add API tests.
6. Front-end foundations:
   - shadcn components, react-hook-form and zod;
   - `parseEuroInput` and `cardNumbers.ts`;
   - the regenerated API types and the `cards.ts` hooks.
7. Settings: sections page, card list with filters, details with reveal, add/edit form, and the
   action dialogs.
8. Dashboard: `CardsWidget` with both empty states.
9. Documentation:
   - `docs/INIT.md` (data model);
   - `README.md`: key generation and backup, plus the user-secrets setup;
   - `.github/workflows/ci.yml`: throwaway keys for the tests;
   - a new ADR `0005-encrypted-sensitive-data.md`.
10. Manual check on desktop and phone width, then set this plan's status to `Done`.

## Tests
- Unit tests (Domain):
  - `CardNumber` and `Iban`: valid and invalid checksums, spaces, length limits, and a masked
    `ToString()`;
  - the card rules: each invalid case from section 3 of the requirements;
  - `StatusOn`:
    - the priority order;
    - the first and last day of the validity period;
    - the 2-month expiring window;
    - a manual expiry before *Valid through*;
  - primary is cleared on block, expire and archive;
  - expiry is final;
  - `IsListedByDefault` and `IsShownOnDashboard`, with and without ongoing transactions;
  - `CanBeUsedOn` for every status.
- Unit tests (Infrastructure and Application):
  - the cipher: encrypt/decrypt round trip, a new nonce for each value, tampered ciphertext is
    rejected, the key id is read;
  - the hash is stable for different spacing;
  - validators;
  - duplicate name, card number and IBAN checks;
  - editing a blocked or expired card returns a conflict.
- API / integration tests (real PostgreSQL in CI, skipped locally without `MONTHWISE_TEST_DB`):
  - the full round trip: create, read, update, block, unblock, expire, archive;
  - the stored columns contain ciphertext, not the number;
  - the reveal returns one number at a time, with `no-store`;
  - list and Dashboard filtering;
  - adding issuers and banks, including duplicates;
  - the `400` response shape and that it does not echo the number;
  - the database rejects a duplicate number and a non-Debit primary.
- Front-end (Vitest + Testing Library):
  - `parseEuroInput` and `cardNumbers.ts`;
  - `CardForm`:
    - shows the primary switch only for Debit;
    - requires one of the two numbers;
    - maps API field errors;
  - adding a new list entry from `ListSelectWithAdd`;
  - `RevealableNumber` hides the number again when unmounted;
  - both Dashboard empty states and the Settings empty state;
  - `CardStatusBadge` renders every status.
- Manual checks:
  - add one card of each type, with a card number, an IBAN, and both;
  - try every validation error;
  - add a new bank;
  - reveal a number;
  - block, then unblock;
  - expire with a prefilled replacement;
  - archive;
  - confirm Settings and the Dashboard agree, on desktop and on a phone-width viewport;
  - check in `psql` that the numbers are not readable.

## Open questions
- None.
