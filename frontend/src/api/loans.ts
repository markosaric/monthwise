import { useMutation, useQuery } from '@tanstack/react-query'
import { api, type Schemas } from './client'
import { ensureOk, unwrap } from './http'

export type Loan = Schemas['LoanResponse']
export type LoanRequest = Schemas['LoanRequest']
export type LoanDetails = Schemas['LoanDetailsResponse']
export type LoanPayment = Schemas['LoanPaymentResponse']
export type LoanPaymentTotals = Schemas['LoanPaymentTotalsResponse']
export type MonthLoanPayments = Schemas['MonthLoanPaymentsResponse']
export type PaymentChange = Schemas['PaymentChangeResponse']
export type PaymentChangeRequest = Schemas['PaymentChangeRequest']
export type LoanPaymentChangeRequest = Schemas['LoanPaymentChangeRequest']
export type LoanPaymentPaidRequest = Schemas['LoanPaymentPaidRequest']
export type LoansDashboard = Schemas['LoansDashboardResponse']
export type LoanCategory = Schemas['LoanCategoryResponse']
export type LoanSubcategory = Schemas['LoanSubcategoryResponse']
export type PaymentMethod = Schemas['PaymentMethod']
export type LoanPaymentKind = Schemas['LoanPaymentKind']
export type LoanStatus = Schemas['LoanStatus']
export type LoanListFilter = NonNullable<Schemas['LoanListFilter']>

export const paymentMethods: PaymentMethod[] = ['StandingOrder', 'Bill']

/** Every loan query shares this prefix. Saved changes refresh it through the shared rule in queryClient.ts. */
export const loansQueryKey = ['loans'] as const
const categoriesKey = ['loan-categories'] as const

export function useLoans(filter: LoanListFilter) {
  return useQuery({
    queryKey: [...loansQueryKey, 'list', filter],
    queryFn: async () =>
      unwrap(await api.GET('/api/loans', { params: { query: { show: filter } } })),
  })
}

export function useLoan(id: string | undefined) {
  return useQuery({
    queryKey: [...loansQueryKey, 'detail', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/loans/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

export function useLoansDashboard() {
  return useQuery({
    queryKey: [...loansQueryKey, 'dashboard'],
    queryFn: async () => unwrap(await api.GET('/api/loans/dashboard')),
  })
}

export function useMonthLoanPayments(month: string) {
  return useQuery({
    queryKey: [...loansQueryKey, 'month', month],
    queryFn: async () =>
      unwrap(await api.GET('/api/loan-payments', { params: { query: { month } } })),
  })
}

export function useCreateLoan() {
  return useMutation({
    mutationFn: async (request: LoanRequest) =>
      unwrap(await api.POST('/api/loans', { body: request })),
  })
}

export function useUpdateLoan(id: string) {
  return useMutation({
    mutationFn: async (request: LoanRequest) =>
      unwrap(await api.PUT('/api/loans/{id}', { params: { path: { id } }, body: request })),
  })
}

export function useCloseLoan(id: string) {
  return useMutation({
    mutationFn: async () =>
      unwrap(await api.POST('/api/loans/{id}/close', { params: { path: { id } } })),
  })
}

export function useDeleteLoan(id: string) {
  return useMutation({
    mutationFn: async () =>
      ensureOk(await api.DELETE('/api/loans/{id}', { params: { path: { id } } })),
  })
}

export type PaymentChangeAction =
  | { action: 'create'; request: PaymentChangeRequest }
  | { action: 'update'; changeId: string; request: PaymentChangeRequest }
  | { action: 'delete'; changeId: string }

export function useChangePaymentChange(loanId: string) {
  return useMutation({
    mutationFn: async (change: PaymentChangeAction) => {
      switch (change.action) {
        case 'create':
          return unwrap(
            await api.POST('/api/loans/{id}/payment-changes', {
              params: { path: { id: loanId } },
              body: change.request,
            }),
          )
        case 'update':
          return unwrap(
            await api.PUT('/api/loans/{id}/payment-changes/{changeId}', {
              params: { path: { id: loanId, changeId: change.changeId } },
              body: change.request,
            }),
          )
        case 'delete':
          return unwrap(
            await api.DELETE('/api/loans/{id}/payment-changes/{changeId}', {
              params: { path: { id: loanId, changeId: change.changeId } },
            }),
          )
      }
    },
  })
}

/** Identifies one payment of a loan: its scheduled month and kind. */
export interface PaymentRef {
  loanId: string
  month: string
  kind: LoanPaymentKind
}

export function paymentRef(payment: LoanPayment): PaymentRef {
  return { loanId: payment.loanId, month: payment.month, kind: payment.kind }
}

export type PaymentAction =
  | { action: 'change'; ref: PaymentRef; request: LoanPaymentChangeRequest }
  | { action: 'paid'; ref: PaymentRef; request: LoanPaymentPaidRequest }
  | { action: 'unpaid'; ref: PaymentRef }

/** Changes one payment, ticks Paid (or corrects the paid date and account) or unticks it (FR-4, FR-5). */
export function usePaymentAction() {
  return useMutation({
    mutationFn: async (change: PaymentAction) => {
      const path = { id: change.ref.loanId, month: change.ref.month, kind: change.ref.kind }
      switch (change.action) {
        case 'change':
          return unwrap(
            await api.PUT('/api/loans/{id}/payments/{month}/{kind}', {
              params: { path },
              body: change.request,
            }),
          )
        case 'paid':
          return unwrap(
            await api.POST('/api/loans/{id}/payments/{month}/{kind}/paid', {
              params: { path },
              body: change.request,
            }),
          )
        case 'unpaid':
          return unwrap(
            await api.DELETE('/api/loans/{id}/payments/{month}/{kind}/paid', { params: { path } }),
          )
      }
    },
  })
}

export function useLoanCategories(includeHidden = false) {
  return useQuery({
    queryKey: [...categoriesKey, includeHidden],
    queryFn: async () =>
      unwrap(await api.GET('/api/loan-categories', { params: { query: { includeHidden } } })),
  })
}

export type LoanCategoryChange =
  | { action: 'create'; name: string; allowsResidualValue: boolean }
  | { action: 'update'; id: string; name: string; allowsResidualValue: boolean }
  | { action: 'hide' | 'show' | 'delete'; id: string }
  | { action: 'createSubcategory'; categoryId: string; name: string }
  | { action: 'renameSubcategory'; id: string; name: string }
  | { action: 'hideSubcategory' | 'showSubcategory' | 'deleteSubcategory'; id: string }

export function useChangeLoanCategory() {
  return useMutation({
    mutationFn: async (change: LoanCategoryChange) => {
      switch (change.action) {
        case 'create':
          return unwrap(
            await api.POST('/api/loan-categories', {
              body: { name: change.name, allowsResidualValue: change.allowsResidualValue },
            }),
          )
        case 'update':
          return unwrap(
            await api.PUT('/api/loan-categories/{id}', {
              params: { path: { id: change.id } },
              body: { name: change.name, allowsResidualValue: change.allowsResidualValue },
            }),
          )
        case 'hide':
          return unwrap(
            await api.POST('/api/loan-categories/{id}/hide', {
              params: { path: { id: change.id } },
            }),
          )
        case 'show':
          return unwrap(
            await api.POST('/api/loan-categories/{id}/show', {
              params: { path: { id: change.id } },
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/loan-categories/{id}', { params: { path: { id: change.id } } }),
          )
        case 'createSubcategory':
          return unwrap(
            await api.POST('/api/loan-categories/{id}/subcategories', {
              params: { path: { id: change.categoryId } },
              body: { name: change.name },
            }),
          )
        case 'renameSubcategory':
          return unwrap(
            await api.PUT('/api/loan-subcategories/{id}', {
              params: { path: { id: change.id } },
              body: { name: change.name },
            }),
          )
        case 'hideSubcategory':
          return unwrap(
            await api.POST('/api/loan-subcategories/{id}/hide', {
              params: { path: { id: change.id } },
            }),
          )
        case 'showSubcategory':
          return unwrap(
            await api.POST('/api/loan-subcategories/{id}/show', {
              params: { path: { id: change.id } },
            }),
          )
        case 'deleteSubcategory':
          return ensureOk(
            await api.DELETE('/api/loan-subcategories/{id}', {
              params: { path: { id: change.id } },
            }),
          )
      }
    },
  })
}
