import type { Card } from '@/api/cards'

const unusable = ['Blocked', 'Expired', 'Archived', 'NotYetValid']

/**
 * Whether what is paid with the card is charged to the card instead of an account: Credit and Charge
 * cards, whose card payment it goes into (FR-6), and revolving cards, whose debt it raises (revolving
 * FR-6). Debit cards are always accounts, also when they allow installments.
 */
export function isCreditCard(card: Card): boolean {
  return card.type === 'Credit' || card.type === 'Charge' || card.type === 'Revolving'
}

/** A revolving credit card (revolving FR-1). */
export function isRevolvingCard(card: Card): boolean {
  return card.type === 'Revolving'
}

/** Accounts a bill or expense can be paid from: usable, and not credit or revolving cards (FR-5). */
export function payableAccounts(cards: Card[], primaryOnly = false): Card[] {
  return cards.filter(
    (c) => !isCreditCard(c) && !unusable.includes(c.status) && (!primaryOnly || c.isPrimary),
  )
}

/**
 * Credit and revolving cards a bill or expense can be paid with: usable, with billing details or
 * revolving terms (FR-5, revolving FR-6).
 */
export function payableCreditCards(cards: Card[]): Card[] {
  return cards.filter(
    (c) =>
      isCreditCard(c) &&
      !unusable.includes(c.status) &&
      !c.needsBillingDetails &&
      (!isRevolvingCard(c) || !!c.revolving),
  )
}

/** The only primary account, to prefill; empty when there are none or several (E4). */
export function onlyPrimaryAccount(cards: Card[]): string {
  const primary = payableAccounts(cards, true)
  return primary.length === 1 ? primary[0]!.id : ''
}
