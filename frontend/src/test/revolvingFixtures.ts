import type { Card } from '@/api/cards'
import type { RevolvingBill, RevolvingCard, RevolvingTerms } from '@/api/revolving'
import { aCard } from './cardFixtures'

// Made-up test values only.
export const account = aCard({
  id: 'account-1',
  name: 'Tekući račun',
  type: 'Debit',
  isPrimary: true,
  allowsInstallments: false,
})

export const revolvingTerms: RevolvingTerms = {
  minPaymentBasisPoints: 833,
  minPaymentAmountCents: 1327,
  interestBasisPoints: 1275,
  minPaymentInterestBasisPoints: null,
  billDay: 5,
  dueDay: 20,
  payingAccount: { id: account.id, name: account.name },
  paysFromAccountDirectly: false,
  terms: [
    {
      fromMonth: null,
      minPaymentBasisPoints: 833,
      minPaymentAmountCents: 1327,
      interestBasisPoints: 1275,
      minPaymentInterestBasisPoints: null,
      billDay: 5,
      dueDay: 20,
    },
  ],
}

/** A revolving card as the card endpoints return it. */
export function aRevolvingCard(overrides: Partial<Card> = {}): Card {
  return aCard({
    id: 'revolving-1',
    name: 'Revolving Visa',
    type: 'Revolving',
    allowsInstallments: false,
    revolving: revolvingTerms,
    revolvingDebtCents: 100_000,
    ...overrides,
  })
}

export function aBill(overrides: Partial<RevolvingBill> = {}): RevolvingBill {
  return {
    cardId: 'revolving-1',
    cardName: 'Revolving Visa',
    month: '2026-10',
    billDate: '2026-10-05',
    dueDate: '2026-10-20',
    debtCents: 100_000,
    interestCents: 1_063,
    minimumCents: 8_418,
    isCorrected: false,
    isEstimated: false,
    isPaid: false,
    paidDate: null,
    paidAmountCents: null,
    account: { id: account.id, name: account.name },
    paysFromAccountDirectly: false,
    paidAutomatically: false,
    isOverdue: false,
    needsAccount: false,
    ...overrides,
  }
}

/** A revolving card as the revolving endpoints list it. */
export function aRevolvingListCard(overrides: Partial<RevolvingCard> = {}): RevolvingCard {
  return {
    id: 'revolving-1',
    name: 'Revolving Visa',
    cardNumberMasked: '•••• 1111',
    status: 'Active',
    canBeUsed: true,
    debtCents: 100_000,
    debtLimitCents: 150_000,
    remainingLimitCents: 50_000,
    isOverLimit: false,
    nextBill: aBill({ month: '2026-11', billDate: '2026-11-05', dueDate: '2026-11-20' }),
    terms: revolvingTerms,
    needsAccount: false,
    ...overrides,
  }
}
