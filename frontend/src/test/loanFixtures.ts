import type { Loan, LoanCategory, LoanPayment, MonthLoanPayments } from '@/api/loans'

export const bankLoan: LoanCategory = {
  id: 'cat-bank-loan',
  name: 'Bank loan',
  allowsResidualValue: false,
  isPredefined: true,
  isHidden: false,
  subcategories: [
    {
      id: 'sub-cash',
      categoryId: 'cat-bank-loan',
      name: 'Cash loan',
      isPredefined: true,
      isHidden: false,
    },
  ],
}

export const lease: LoanCategory = {
  id: 'cat-lease',
  name: 'Lease',
  allowsResidualValue: true,
  isPredefined: true,
  isHidden: false,
  subcategories: [
    {
      id: 'sub-car-lease',
      categoryId: 'cat-lease',
      name: 'Car lease',
      isPredefined: true,
      isHidden: false,
    },
  ],
}

export function aLoan(overrides: Partial<Loan> = {}): Loan {
  return {
    id: 'loan-1',
    name: 'Example flat',
    category: { id: bankLoan.id, name: bankLoan.name },
    subcategory: { id: 'sub-cash', name: 'Cash loan' },
    lender: { id: 'bank-zaba', name: 'ZaBa' },
    paymentMethod: 'StandingOrder',
    account: { id: 'tekuci', name: 'Tekući' },
    startMonth: '2026-08',
    endMonth: '2027-07',
    paymentDay: 15,
    monthlyPaymentCents: 50000,
    paymentCount: 12,
    residualValueCents: null,
    description: null,
    closedOn: null,
    status: 'Active',
    currentMonthlyPaymentCents: 50000,
    currentPaymentDay: 15,
    nextPaymentDate: '2026-11-15',
    paymentsPaid: 3,
    paymentsLeft: 9,
    paidAmountCents: 150000,
    remainingAmountCents: 450000,
    hasOverduePayment: false,
    needsAccount: false,
    ...overrides,
  }
}

export function aPayment(overrides: Partial<LoanPayment> = {}): LoanPayment {
  return {
    loanId: 'loan-1',
    month: '2026-10',
    kind: 'Regular',
    loanName: 'Example flat',
    category: { id: bankLoan.id, name: bankLoan.name },
    subcategory: { id: 'sub-cash', name: 'Cash loan' },
    number: 3,
    total: 12,
    amountCents: 50000,
    billDate: '2026-10-01',
    dueDate: '2026-10-15',
    paidDate: null,
    account: { id: 'tekuci', name: 'Tekući' },
    paymentMethod: 'StandingOrder',
    isPaid: false,
    isChanged: false,
    isOverdue: false,
    needsAccount: false,
    share: null,
    ownPartCents: null,
    isShareChanged: false,
    contribution: null,
    ...overrides,
  }
}

export function aLoanMonth(payments: LoanPayment[], month = '2026-10'): MonthLoanPayments {
  const expected = payments.filter((p) => !p.isPaid).reduce((sum, p) => sum + p.amountCents, 0)
  const paid = payments.filter((p) => p.isPaid).reduce((sum, p) => sum + p.amountCents, 0)
  return {
    month,
    payments,
    totals: { expectedCents: expected, paidCents: paid, totalCents: expected + paid },
    ownPartCents: payments.reduce((sum, p) => sum + (p.ownPartCents ?? p.amountCents), 0),
  }
}
