import type {
  CardPayment,
  CardPaymentDetails,
  CardPurchaseDetails,
  InstallmentCard,
} from '@/api/cardPurchases'

export function anInstallmentCard(overrides: Partial<InstallmentCard> = {}): InstallmentCard {
  return {
    id: 'card-1',
    name: 'Family Visa',
    cardNumberMasked: '•••• 1111',
    type: 'Credit',
    status: 'Active',
    canBeUsed: true,
    needsBillingDetails: false,
    statementDay: 5,
    paymentDay: 20,
    payingAccount: { id: 'account-1', name: 'Tekući račun' },
    paysFromAccountDirectly: false,
    maxInstallments: 12,
    needsAccount: false,
    nextPayment: null,
    remainingCents: 0,
    purchasesWithInstallmentsLeft: 0,
    ...overrides,
  }
}

export function aCardPayment(overrides: Partial<CardPayment> = {}): CardPayment {
  return {
    cardId: 'card-1',
    cardName: 'Family Visa',
    month: '2026-10',
    periodFrom: '2026-09-06',
    statementDate: '2026-10-05',
    paymentDate: '2026-10-20',
    amountCents: 12000,
    lineCount: 2,
    isPaid: false,
    paidDate: null,
    account: { id: 'account-1', name: 'Tekući račun' },
    paysFromAccountDirectly: false,
    isOverdue: false,
    needsAccount: false,
    ...overrides,
  }
}

export function aCardPaymentDetails(): CardPaymentDetails {
  return {
    payment: aCardPayment(),
    lines: [
      {
        sourceId: 'purchase-1',
        description: 'Washing machine',
        date: '2026-09-10',
        kind: 'Installment',
        installmentNumber: 1,
        installmentTotal: 3,
        category: { id: 'home', name: 'Home' },
        amountCents: 10000,
      },
      {
        sourceId: 'purchase-2',
        description: 'Groceries',
        date: '2026-10-01',
        kind: 'FullPurchase',
        installmentNumber: 1,
        installmentTotal: 1,
        category: null,
        amountCents: 3000,
      },
      {
        sourceId: 'purchase-3',
        description: 'Shoes',
        date: '2026-08-01',
        kind: 'Credit',
        installmentNumber: 2,
        installmentTotal: 2,
        category: null,
        amountCents: -1000,
      },
    ],
  }
}

export function aPurchaseDetails(): CardPurchaseDetails {
  return {
    purchase: {
      id: 'purchase-1',
      card: { id: 'card-1', name: 'Family Visa' },
      description: 'Washing machine',
      purchaseDate: '2026-09-10',
      amountCents: 30000,
      inInstallments: true,
      installmentCount: 3,
      postponeFirst: false,
      category: null,
      reversedOn: null,
      reversalReason: null,
      installmentsCharged: 1,
      installmentsLeft: 2,
      chargedCents: 10000,
      remainingCents: 20000,
      hasChargedInstallments: true,
    },
    installments: [1, 2, 3].map((number) => ({
      number,
      total: 3,
      amountCents: 10000,
      paymentMonth: `2026-${String(9 + number).padStart(2, '0')}`,
      statementDate: `2026-${String(9 + number).padStart(2, '0')}-05`,
      paymentDate: `2026-${String(9 + number).padStart(2, '0')}-20`,
      isCharged: number === 1,
      reversedOn: null,
      reversalReason: null,
      reversedWithPurchase: false,
      creditMonth: null,
    })),
  }
}
