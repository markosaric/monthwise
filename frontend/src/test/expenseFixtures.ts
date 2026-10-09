import type {
  Bill,
  Expense,
  ExpenseCategory,
  MonthBills,
  MonthExpenses,
  RecurringBill,
} from '@/api/expenses'
import { cashWithdrawalCategoryId } from '@/api/expenses'

export const electricity: ExpenseCategory = {
  id: 'electricity',
  name: 'Electricity',
  isPredefined: true,
  isFixed: false,
  isHidden: false,
}
export const groceries: ExpenseCategory = {
  id: 'groceries',
  name: 'Groceries',
  isPredefined: true,
  isFixed: false,
  isHidden: false,
}
export const cashWithdrawalCategory: ExpenseCategory = {
  id: cashWithdrawalCategoryId,
  name: 'Cash withdrawal',
  isPredefined: true,
  isFixed: true,
  isHidden: false,
}
export const expenseCategories = [electricity, groceries, cashWithdrawalCategory]

export const tekuciPaidFrom = { id: 'tekuci', name: 'Tekući račun', isCreditCard: false }
export const visaPaidFrom = { id: 'visa', name: 'Family Visa', isCreditCard: true }

export function aBill(overrides: Partial<Bill> = {}): Bill {
  return {
    id: 'bill-1',
    recurringBillId: 'recurring-1',
    month: '2026-10',
    name: 'Electricity, apartment',
    category: { id: electricity.id, name: electricity.name },
    payee: 'HEP',
    amountCents: 4250,
    issueDate: '2026-10-03',
    dueDate: '2026-10-15',
    state: 'Expected',
    paidDate: null,
    paidFrom: tekuciPaidFrom,
    description: null,
    isOverdue: false,
    dueDateHint: null,
    paidWithCard: false,
    changesChargedCardPayment: false,
    ...overrides,
  }
}

/** A bill added from a recurring bill and not issued yet. */
export function aNotIssuedBill(overrides: Partial<Bill> = {}): Bill {
  return aBill({
    id: 'bill-2',
    name: 'Internet',
    amountCents: 0,
    issueDate: null,
    dueDate: null,
    state: 'NotIssued',
    dueDateHint: '2026-10-20',
    ...overrides,
  })
}

export function aBillMonth(bills: Bill[], month = '2026-10'): MonthBills {
  const counted = bills.filter((b) => b.state !== 'NotIssued' && !b.paidWithCard)
  const expected = counted
    .filter((b) => b.state === 'Expected')
    .reduce((s, b) => s + b.amountCents, 0)
  const paid = counted.filter((b) => b.state === 'Paid').reduce((s, b) => s + b.amountCents, 0)
  return {
    month,
    bills,
    totals: { expectedCents: expected, paidCents: paid, totalCents: expected + paid },
    withCardCents: bills.filter((b) => b.paidWithCard).reduce((s, b) => s + b.amountCents, 0),
    notIssuedCount: bills.filter((b) => b.state === 'NotIssued').length,
  }
}

export function anExpense(overrides: Partial<Expense> = {}): Expense {
  return {
    id: 'expense-1',
    date: '2026-10-09',
    amountCents: 4000,
    category: { id: groceries.id, name: groceries.name },
    paidFrom: tekuciPaidFrom,
    description: 'Konzum',
    isCashWithdrawal: false,
    paidWithCard: false,
    changesChargedCardPayment: false,
    ...overrides,
  }
}

export function anExpenseMonth(expenses: Expense[], month = '2026-10'): MonthExpenses {
  const total = expenses.reduce((s, e) => s + e.amountCents, 0)
  const withCard = expenses.filter((e) => e.paidWithCard).reduce((s, e) => s + e.amountCents, 0)
  const cash = expenses.filter((e) => e.isCashWithdrawal).reduce((s, e) => s + e.amountCents, 0)
  return {
    month,
    expenses,
    totals: {
      totalCents: total,
      fromAccountsCents: total - withCard,
      withCardCents: withCard,
      cashWithdrawalCents: cash,
    },
  }
}

export function aRecurringBill(overrides: Partial<RecurringBill> = {}): RecurringBill {
  return {
    id: 'recurring-1',
    name: 'Electricity, apartment',
    category: { id: electricity.id, name: electricity.name },
    payee: 'HEP',
    paidFrom: tekuciPaidFrom,
    description: null,
    isActive: true,
    stoppedOn: null,
    canBeDeleted: false,
    ...overrides,
  }
}
