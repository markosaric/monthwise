import type { Dashboard } from '@/api/dashboard'

/** A Dashboard of October 2026 with made-up figures. */
export function aDashboard(overrides: Partial<Dashboard> = {}): Dashboard {
  return {
    month: '2026-10',
    today: '2026-10-18',
    inputs: { totalCents: 325000, doneCents: 290000, previousMonthTotalCents: 312500 },
    outputs: { totalCents: 248000, doneCents: 191500, previousMonthTotalCents: 227500 },
    monthResultCents: 77000,
    previousMonthResultCents: 85000,
    stillToPay: { totalCents: 56500, count: 4, overdueCents: 0, overdueCount: 0 },
    nextDue: { kind: 'Bill', name: 'Electricity', date: '2026-10-22', amountCents: 6420 },
    comingUp: [
      { kind: 'Bill', name: 'Electricity', date: '2026-10-22', amountCents: 6420 },
      { kind: 'LoanPayment', name: 'Car loan', date: '2026-10-25', amountCents: 31000 },
      { kind: 'Input', name: 'Contribution from Ana', date: '2026-10-24', amountCents: 15000 },
    ],
    outputsByKind: {
      billsCents: 114080,
      expensesCents: 44640,
      loanPaymentsCents: 59520,
      cardPaymentsCents: 29760,
    },
    lastSixMonths: [
      { month: '2026-05', inputsCents: 300000, outputsCents: 250000 },
      { month: '2026-06', inputsCents: 310000, outputsCents: 320000 },
      { month: '2026-07', inputsCents: 305000, outputsCents: 240000 },
      { month: '2026-08', inputsCents: 330000, outputsCents: 260000 },
      { month: '2026-09', inputsCents: 312500, outputsCents: 227500 },
      { month: '2026-10', inputsCents: 325000, outputsCents: 248000 },
    ],
    loans: {
      activeCount: 2,
      monthlyPaymentCents: 59520,
      remainingCents: 1840000,
      hasOverdue: false,
    },
    cardPayments: { monthTotalCents: 29760, count: 2, cardsWithInstallments: 1 },
    sharedCosts: { outstandingCents: 15000, participantsOwing: 1, hasOverdue: false },
    recurringBills: { activeCount: 9, notIssuedThisMonth: 2 },
    ...overrides,
  }
}
