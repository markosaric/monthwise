# Plan: Screens show every saved change at once

- **Branch:** `fix/screen-refresh`
- **Status:** In progress
- **Date:** 2026-11-12
- **Requirements:** [screen-refresh.md](../feature-requirements/screen-refresh.md)

## Goal

Every change the user saves shows up on every screen at once (FR-1, FR-2), and new features get
this without listing which screens they affect (non-functional requirements). Fixes finding L8 of
the main branch code review.

## Scope

- In scope: front-end query refresh after successful mutations, for every mutation in
  `frontend/src/api/*.ts`; one shared query client setup used by the app and the tests.
- Out of scope: back end, API, database; changes made on another device (except imports, which
  keep their current reload); push updates from the server.

## Design

- Domain / data model changes: none.
- API changes (endpoints, DTOs): none.
- Front-end changes:
  - New `frontend/src/api/queryClient.ts` with `createQueryClient()`. It builds the `QueryClient`
    with a `MutationCache` whose `onSuccess` refreshes all shown data after **any** successful
    mutation (FR-1): `queryClient.invalidateQueries({ predicate })`, where the predicate skips the
    status check (`health`, FR-4) and the backup data (`data-transfer`, FR-4). The promise is
    returned, so a mutation stays pending until the refreshed data has arrived, as today; buttons
    and dialogs keep their current "saving" state and close with the numbers already updated.
  - Invalidation only refetches queries that are on screen; the others are marked stale and load
    when their screen opens (FR-1 last point). `useCardList` keeps its 5-minute stale time, which
    an invalidation overrides, so open choice lists update at once (acceptance criterion 6).
  - Mutations that must not refresh everything opt out with `meta: { refreshAll: false }`: the
    import check (changes nothing), export, and the export reminder settings (FR-4). They keep
    their own `data-transfer` refresh. Import and restore keep reloading every query (they already
    do) and opt out too, so their reload runs only once.
  - The `meta` type is declared once through TanStack Query's `Register` interface, so a typo in
    the flag fails the type check.
  - Remove the hand-written lists from every API module: the `useInvalidate…` helpers and the
    `onSuccess` invalidations in `cardPurchases.ts`, `cards.ts`, `cashFlow.ts`, `expenses.ts`,
    `loans.ts`, `revolving.ts` and `sharedCosts.ts`, together with the cross-module key imports
    they needed (for example `dashboardQueryKey` in `revolving.ts`). Query keys stay as they are.
  - `main.tsx`, `src/test/renderRoute.tsx` and the revolving component test use
    `createQueryClient()` (tests pass `retry: false` as today), so tests run the same refresh rule
    as the app.
  - Comments that describe the old lists (for example on `cashFlowQueryKey` and `useDashboard`)
    are updated to point to the shared rule.
- Database migrations: none.

## Steps

1. Add `queryClient.ts` with `createQueryClient()`, the refresh rule and the `meta` typing.
2. Use it in `main.tsx` and the test helpers.
3. Remove the per-module invalidation from the seven API modules; add the opt-outs in
   `dataTransfer.ts`.
4. Update comments; run `npm run typecheck`, `npm run lint`, `npm run format:check`,
   `npm test` and `npm run build`.

## Tests

- Unit tests (`queryClient.test.ts`): after a successful mutation, cached queries of other areas
  (for example `dashboard`, `expenses`, `loans`, `card-lists`) are invalidated; `health` and
  `data-transfer` are not; a failed mutation invalidates nothing (FR-3); a mutation with
  `refreshAll: false` invalidates nothing.
- Route tests: on the Month screen, adding a card purchase loads the bills and loan payments
  again (acceptance criterion 1); on the Dashboard, a quick action reloads the Dashboard
  (criterion 2). Existing tests keep passing.
- Manual checks: Month screen add / edit / delete in each section updates the key figures and the
  other sections; rename an account and check open forms and Card purchases; add a bank in a card
  form; check that a form being typed in keeps its values while data refreshes; import still
  reloads everything.

## Open questions

- None.
