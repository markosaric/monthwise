# Plan: Outstanding contributions without status chips

- **Branch:** `fix/shared-costs-outstanding-list`
- **Status:** In progress
- **Date:** 2026-11-19
- **Requirements:** [shared-bills.md, Appendix 2](../feature-requirements/shared-bills.md#appendix-2-outstanding-contributions-without-status-chips),
  [design-refresh.md, Appendix 6](../feature-requirements/design-refresh.md#appendix-6-outstanding-contributions-without-status-chips)

## Goal
On the Shared costs screen, the Outstanding contributions list shows each contribution without an
*Expected* or *Overdue* chip, so that card shows only what is outstanding and what was received
this month.

## Requirement changes
[shared-bills.md, Appendix 2](../feature-requirements/shared-bills.md#appendix-2-outstanding-contributions-without-status-chips)
changes FR-8 and acceptance criterion 9, and
[design-refresh.md, Appendix 6](../feature-requirements/design-refresh.md#appendix-6-outstanding-contributions-without-status-chips)
changes FR-19 (Outstanding contributions):

- The Outstanding contributions list shows each contribution's cost, expected date, amount and the
  one-tap Received check box, with no *Expected* or *Overdue* chip; overdue ones are no longer
  highlighted in the list.
- The **Overdue** key figure stays unchanged.
- The contributions of the month in a participant's card keep their chips.

**Why:** every line in that list is outstanding, so the chips repeat the list title and make the
list busy; the user only needs Outstanding and Received this month there.

## Scope
- In scope: the Outstanding contributions list on the Shared costs screen.
- Out of scope: the key figures (Overdue stays), the participant cards and their month
  contributions, the API, other screens.

## Design
- Domain / data model changes: none.
- API changes (endpoints, DTOs): none; `isOverdue` stays in the response (used by the key figure).
- Front-end changes (pages, components):
  - `frontend/src/pages/sharedCosts/SharedCostsPage.tsx`: `ContributionList` and
    `ContributionLine` get a `showStatus` prop (default `true`); the Outstanding contributions list
    passes `showStatus={false}`, so its lines render no `StatusChip`. The *Needs account* badge
    stays, since it explains why the check box is disabled.
  - No text changes (`overdue` and `expected` stay in use by the key figure and month list).
- Database migrations: none.

## Steps
1. Add the `showStatus` prop and turn it off for the Outstanding contributions list.
2. Update the Shared costs screen test: the outstanding list has no *Overdue* or *Expected* chip,
   the Overdue key figure still shows the total, and the month list still shows chips.
3. Run typecheck, lint, format check and the front-end tests; check the screen in a browser.

## Tests
- Unit tests: `sharedCosts.test.tsx` as in step 2.
- API / integration tests: none (no back-end change).
- Manual checks: Shared costs screen on desktop and phone width, with an overdue contribution.
