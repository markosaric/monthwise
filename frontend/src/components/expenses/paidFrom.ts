import type { Card } from '@/api/cards'

const unusable = ['Blocked', 'Expired', 'Archived', 'NotYetValid']

/**
 * Whether what is paid with the card goes into its card payment: Credit and Charge cards (FR-6). Debit
 * cards are always accounts, also when they allow installments.
 */
export function isCreditCard(card: Card): boolean {
  return card.type === 'Credit' || card.type === 'Charge'
}

/** Accounts a bill or expense can be paid from: usable, and not credit cards (FR-5). */
export function payableAccounts(cards: Card[], primaryOnly = false): Card[] {
  return cards.filter(
    (c) => !isCreditCard(c) && !unusable.includes(c.status) && (!primaryOnly || c.isPrimary),
  )
}

/** Credit cards a bill or expense can be paid with: usable, with billing details (FR-5). */
export function payableCreditCards(cards: Card[]): Card[] {
  return cards.filter(
    (c) => isCreditCard(c) && !unusable.includes(c.status) && !c.needsBillingDetails,
  )
}

/** The only primary account, to prefill; empty when there are none or several (E4). */
export function onlyPrimaryAccount(cards: Card[]): string {
  const primary = payableAccounts(cards, true)
  return primary.length === 1 ? primary[0]!.id : ''
}
