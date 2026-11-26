import { useMutation, useQuery } from '@tanstack/react-query'
import { api, type Schemas } from './client'
import { ensureOk, unwrap } from './http'

export type Lending = Schemas['LendingResponse']
export type LendingState = Schemas['LendingState']
export type LendingRequest = Schemas['LendingRequest']
export type Repayment = Schemas['RepaymentResponse']
export type RepaymentRequest = Schemas['RepaymentRequest']
export type LendingList = Schemas['LendingListResponse']
export type LendingFilter = NonNullable<Schemas['LendingFilter']>
export type BorrowerTotals = Schemas['BorrowerTotalsResponse']
export type MonthLending = Schemas['MonthLendingResponse']
export type MonthLendingEntry = Schemas['MonthLendingEntryResponse']
export type ReportLending = Schemas['ReportLendingResponse']

/**
 * The id of the Lending group in the monthly report's inputs and outputs by category (money lending
 * FR-10). It is not an input or expense category.
 */
export const lendingGroupId = '0199b0c0-0000-7000-8000-00000000a001'

/** Every lending query shares this prefix. Saved changes refresh them through the shared rule in queryClient.ts. */
export const lendingQueryKey = ['lendings'] as const

/** The Lending screen: open and overdue lendings, or returned ones (FR-1). */
export function useLendings(state: LendingFilter = 'Open') {
  return useQuery({
    queryKey: [...lendingQueryKey, 'list', state],
    queryFn: async () => unwrap(await api.GET('/api/lendings', { params: { query: { state } } })),
  })
}

export function useLending(id: string | undefined) {
  return useQuery({
    queryKey: [...lendingQueryKey, 'one', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/lendings/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

/** What each borrower was lent, returned and still owes (FR-6). */
export function useBorrowerTotals() {
  return useQuery({
    queryKey: [...lendingQueryKey, 'borrowers'],
    queryFn: async () => unwrap(await api.GET('/api/lendings/borrowers')),
  })
}

/** The month's lending for the Month screen (FR-9). */
export function useMonthLending(month: string) {
  return useQuery({
    queryKey: [...lendingQueryKey, 'month', month],
    queryFn: async () =>
      unwrap(await api.GET('/api/lendings/month', { params: { query: { month } } })),
  })
}

export type LendingChange =
  | { action: 'create'; request: LendingRequest }
  | { action: 'update'; id: string; request: LendingRequest }
  | { action: 'delete'; id: string }
  | { action: 'addRepayment'; id: string; request: RepaymentRequest }
  | { action: 'updateRepayment'; id: string; repaymentId: string; request: RepaymentRequest }
  | { action: 'deleteRepayment'; id: string; repaymentId: string }

export function useChangeLending() {
  return useMutation({
    mutationFn: async (change: LendingChange): Promise<Lending | void> => {
      switch (change.action) {
        case 'create':
          return unwrap(await api.POST('/api/lendings', { body: change.request }))
        case 'update':
          return unwrap(
            await api.PUT('/api/lendings/{id}', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/lendings/{id}', { params: { path: { id: change.id } } }),
          )
        case 'addRepayment':
          return unwrap(
            await api.POST('/api/lendings/{id}/repayments', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'updateRepayment':
          return unwrap(
            await api.PUT('/api/lendings/{id}/repayments/{repaymentId}', {
              params: { path: { id: change.id, repaymentId: change.repaymentId } },
              body: change.request,
            }),
          )
        case 'deleteRepayment':
          return unwrap(
            await api.DELETE('/api/lendings/{id}/repayments/{repaymentId}', {
              params: { path: { id: change.id, repaymentId: change.repaymentId } },
            }),
          )
      }
    },
  })
}
