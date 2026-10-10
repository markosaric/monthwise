import type { InputEntry } from '@/api/cashFlow'
import type { Contribution, Participant, Share, SharedCosts } from '@/api/sharedCosts'
import { anEntry } from './cashFlowFixtures'

export const ana: Participant = {
  id: 'ana',
  name: 'Ana',
  description: null,
  isArchived: false,
  canBeDeleted: false,
}

export const halfWithAna: Share = {
  participant: { id: ana.id, name: ana.name },
  shares: 2,
  participantShares: 1,
  percentage: 50,
  contributionAccount: null,
}

export function aContribution(overrides: Partial<Contribution> = {}): Contribution {
  return {
    kind: 'Bill',
    billId: 'bill-1',
    loanId: null,
    month: null,
    paymentKind: null,
    costName: 'Electricity, apartment',
    participant: { id: ana.id, name: ana.name },
    partCents: 2125,
    inputId: null,
    amountCents: 2125,
    expectedDate: '2026-10-15',
    receivedDate: null,
    isReceived: false,
    isEdited: false,
    needsAccount: false,
    isOverdue: false,
    account: { id: 'tekuci', name: 'Tekući račun' },
    ...overrides,
  }
}

/** A calculated contribution in the Income list. */
export function aContributionEntry(overrides: Partial<InputEntry> = {}): InputEntry {
  return anEntry({
    id: null,
    payer: { id: 'payer-ana', name: 'Ana' },
    category: { id: 'agreed', name: 'Agreed contributions' },
    amountCents: 2125,
    expectedDate: '2026-10-15',
    receivedDate: null,
    isReceived: false,
    description: 'Electricity, apartment',
    contribution: {
      kind: 'Bill',
      billId: 'bill-1',
      loanId: null,
      month: null,
      paymentKind: null,
      costName: 'Electricity, apartment',
      partCents: 2125,
      isEdited: false,
      needsAccount: false,
    },
    ...overrides,
  })
}

export function aSharedCosts(contributions: Contribution[], month = '2026-10'): SharedCosts {
  const outstanding = contributions.filter((c) => !c.isReceived)
  return {
    month,
    participants: [
      {
        participant: ana,
        costs: [
          { kind: 'Bill', id: 'recurring-1', name: 'Electricity, apartment', share: halfWithAna },
        ],
        monthPartsCents: contributions.reduce((s, c) => s + c.partCents, 0),
        monthReceivedCents: contributions
          .filter((c) => c.isReceived)
          .reduce((s, c) => s + c.amountCents, 0),
        outstandingCents: outstanding.reduce((s, c) => s + c.amountCents, 0),
        monthContributions: contributions,
        outstanding,
      },
    ],
  }
}
