import type { Card } from '@/api/cards'

export const visaIssuer = { id: 'issuer-visa', name: 'Visa', isPredefined: true }
export const zaba = { id: 'bank-zaba', name: 'ZaBa', isPredefined: true }

export function aCard(overrides: Partial<Card> = {}): Card {
  return {
    id: 'card-1',
    name: 'Family Visa',
    issuer: visaIssuer,
    bank: zaba,
    cardNumberMasked: '•••• 1111',
    ibanMasked: null,
    validFrom: '2025-01',
    validThrough: '2029-12',
    type: 'Credit',
    isPrimary: false,
    debtLimitCents: 150000,
    allowsInstallments: true,
    status: 'Active',
    canBeUsed: true,
    blockedOn: null,
    blockReason: null,
    expiredOn: null,
    expiryReason: null,
    archivedOn: null,
    replacedBy: null,
    replaces: null,
    ...overrides,
  }
}
