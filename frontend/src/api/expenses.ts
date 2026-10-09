import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cardPurchasesQueryKey } from './cardPurchases'
import { cardsQueryKey } from './cards'
import { cashFlowQueryKey } from './cashFlow'
import { api, type Schemas } from './client'
import { ensureOk, unwrap } from './http'

export type ExpenseCategory = Schemas['ExpenseCategoryResponse']
export type PaidFrom = Schemas['PaidFromResponse']
export type RecurringBill = Schemas['RecurringBillResponse']
export type RecurringBillRequest = Schemas['RecurringBillRequest']
export type Bill = Schemas['BillResponse']
export type BillState = Schemas['BillState']
export type BillRequest = Schemas['BillRequest']
export type BillPaidRequest = Schemas['BillPaidRequest']
export type MonthBills = Schemas['MonthBillsResponse']
export type Expense = Schemas['ExpenseResponse']
export type ExpenseRequest = Schemas['ExpenseRequest']
export type CashWithdrawalRequest = Schemas['CashWithdrawalRequest']
export type MonthExpenses = Schemas['MonthExpensesResponse']
export type ExpenseTotals = Schemas['ExpenseTotalsResponse']

/** The fixed Cash withdrawal category (bills and expenses FR-8). */
export const cashWithdrawalCategoryId = '0199c8b0-0000-7000-8000-000000000499'

/** Every bill and expense query shares this prefix. */
export const expensesQueryKey = ['expenses'] as const
const categoriesKey = ['expense-categories'] as const

/**
 * Refreshes everything a bill or expense change can affect: bills and expenses, card payments (bills
 * and expenses paid with a credit card are in them), balances, chart and report.
 */
function useInvalidateExpenses() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: expensesQueryKey }),
      queryClient.invalidateQueries({ queryKey: cardPurchasesQueryKey }),
      queryClient.invalidateQueries({ queryKey: cashFlowQueryKey }),
      queryClient.invalidateQueries({ queryKey: cardsQueryKey }),
    ])
}

// Expense categories: one list for card purchases, bills and expenses (FR-1).

export function useExpenseCategories(includeHidden = false) {
  return useQuery({
    queryKey: [...categoriesKey, includeHidden],
    queryFn: async () =>
      unwrap(await api.GET('/api/expense-categories', { params: { query: { includeHidden } } })),
  })
}

export type ExpenseCategoryChange =
  | { action: 'create'; name: string }
  | { action: 'rename'; id: string; name: string }
  | { action: 'hide' | 'show' | 'delete'; id: string }

export function useChangeExpenseCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (change: ExpenseCategoryChange) => {
      switch (change.action) {
        case 'create':
          return unwrap(await api.POST('/api/expense-categories', { body: { name: change.name } }))
        case 'rename':
          return unwrap(
            await api.PUT('/api/expense-categories/{id}', {
              params: { path: { id: change.id } },
              body: { name: change.name },
            }),
          )
        case 'hide':
          return unwrap(
            await api.POST('/api/expense-categories/{id}/hide', {
              params: { path: { id: change.id } },
            }),
          )
        case 'show':
          return unwrap(
            await api.POST('/api/expense-categories/{id}/show', {
              params: { path: { id: change.id } },
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/expense-categories/{id}', {
              params: { path: { id: change.id } },
            }),
          )
      }
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: categoriesKey }),
        queryClient.invalidateQueries({ queryKey: cardPurchasesQueryKey }),
        queryClient.invalidateQueries({ queryKey: expensesQueryKey }),
      ]),
  })
}

// Recurring bills (FR-2) and adding them to a month (FR-3).

export function useRecurringBills(includeStopped = false) {
  return useQuery({
    queryKey: [...expensesQueryKey, 'recurring', includeStopped],
    queryFn: async () =>
      unwrap(await api.GET('/api/recurring-bills', { params: { query: { includeStopped } } })),
  })
}

export function useRecurringBill(id: string | undefined) {
  return useQuery({
    queryKey: [...expensesQueryKey, 'recurring', 'one', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/recurring-bills/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

export type RecurringBillChange =
  | { action: 'create'; request: RecurringBillRequest }
  | { action: 'update'; id: string; request: RecurringBillRequest }
  | { action: 'stop' | 'restart'; id: string }
  | { action: 'delete'; id: string }

export function useChangeRecurringBill() {
  const invalidate = useInvalidateExpenses()
  return useMutation({
    mutationFn: async (change: RecurringBillChange) => {
      switch (change.action) {
        case 'create':
          return unwrap(await api.POST('/api/recurring-bills', { body: change.request }))
        case 'update':
          return unwrap(
            await api.PUT('/api/recurring-bills/{id}', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'stop':
          return unwrap(
            await api.POST('/api/recurring-bills/{id}/stop', {
              params: { path: { id: change.id } },
            }),
          )
        case 'restart':
          return unwrap(
            await api.POST('/api/recurring-bills/{id}/restart', {
              params: { path: { id: change.id } },
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/recurring-bills/{id}', { params: { path: { id: change.id } } }),
          )
      }
    },
    onSuccess: invalidate,
  })
}

/** Adds a Not issued bill for every active recurring bill to the month (FR-3). */
export function useAddRecurringBills() {
  const invalidate = useInvalidateExpenses()
  return useMutation({
    mutationFn: async (month: string) =>
      unwrap(await api.POST('/api/bills/add-recurring', { params: { query: { month } } })),
    onSuccess: invalidate,
  })
}

// Bills (FR-4 to FR-6).

export function useMonthBills(month: string) {
  return useQuery({
    queryKey: [...expensesQueryKey, 'bills', month],
    queryFn: async () => unwrap(await api.GET('/api/bills', { params: { query: { month } } })),
  })
}

export function useBill(id: string | undefined) {
  return useQuery({
    queryKey: [...expensesQueryKey, 'bill', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/bills/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

export type BillChange =
  | { action: 'create'; month: string; request: BillRequest }
  | { action: 'update'; id: string; request: BillRequest }
  | { action: 'paid'; id: string; request: BillPaidRequest }
  | { action: 'unpaid'; id: string }
  | { action: 'delete'; id: string }

export function useChangeBill() {
  const invalidate = useInvalidateExpenses()
  return useMutation({
    mutationFn: async (change: BillChange) => {
      switch (change.action) {
        case 'create':
          return unwrap(
            await api.POST('/api/bills', {
              params: { query: { month: change.month } },
              body: change.request,
            }),
          )
        case 'update':
          return unwrap(
            await api.PUT('/api/bills/{id}', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'paid':
          return unwrap(
            await api.POST('/api/bills/{id}/paid', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'unpaid':
          return unwrap(
            await api.DELETE('/api/bills/{id}/paid', { params: { path: { id: change.id } } }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/bills/{id}', { params: { path: { id: change.id } } }),
          )
      }
    },
    onSuccess: invalidate,
  })
}

// Expenses and cash withdrawals (FR-7, FR-8).

export function useMonthExpenses(month: string) {
  return useQuery({
    queryKey: [...expensesQueryKey, 'expenses', month],
    queryFn: async () => unwrap(await api.GET('/api/expenses', { params: { query: { month } } })),
  })
}

export function useExpense(id: string | undefined) {
  return useQuery({
    queryKey: [...expensesQueryKey, 'expense', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/expenses/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

/** What the last expense was paid with, to prefill a new one (E4). */
export function useLastExpensePaidFrom() {
  return useQuery({
    queryKey: [...expensesQueryKey, 'last-paid-from'],
    queryFn: async () => unwrap(await api.GET('/api/expenses/last-paid-from')),
  })
}

export type ExpenseChange =
  | { action: 'create'; request: ExpenseRequest }
  | { action: 'update'; id: string; request: ExpenseRequest }
  | { action: 'createCash'; request: CashWithdrawalRequest }
  | { action: 'updateCash'; id: string; request: CashWithdrawalRequest }
  | { action: 'delete'; id: string }

export function useChangeExpense() {
  const invalidate = useInvalidateExpenses()
  return useMutation({
    mutationFn: async (change: ExpenseChange) => {
      switch (change.action) {
        case 'create':
          return unwrap(await api.POST('/api/expenses', { body: change.request }))
        case 'update':
          return unwrap(
            await api.PUT('/api/expenses/{id}', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'createCash':
          return unwrap(await api.POST('/api/expenses/cash-withdrawals', { body: change.request }))
        case 'updateCash':
          return unwrap(
            await api.PUT('/api/expenses/cash-withdrawals/{id}', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/expenses/{id}', { params: { path: { id: change.id } } }),
          )
      }
    },
    onSuccess: invalidate,
  })
}
