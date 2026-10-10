import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cardsQueryKey } from './cards'
import { cashFlowQueryKey } from './cashFlow'
import { api, type Schemas } from './client'
import { expensesQueryKey } from './expenses'
import { ensureOk, unwrap } from './http'
import { loansQueryKey } from './loans'

export type Participant = Schemas['ParticipantResponse']
export type ParticipantRequest = Schemas['ParticipantRequest']
export type ShareRequest = Schemas['ShareRequest']
export type Share = Schemas['ShareResponse']
export type Contribution = Schemas['ContributionResponse']
export type ContributionRequest = Schemas['ContributionRequest']
export type InputContribution = Schemas['InputContributionResponse']
export type SharedCosts = Schemas['SharedCostsResponse']
export type ParticipantSharedCosts = Schemas['ParticipantSharedCostsResponse']
export type ReportSharedCosts = Schemas['ReportSharedCostsResponse']

const participantsKey = ['participants'] as const

/**
 * The Shared costs screen sits under the cash flow prefix, so every bill, loan and input change (which
 * all refresh the cash flow) refreshes it too.
 */
export const sharedCostsQueryKey = [...cashFlowQueryKey, 'shared-costs'] as const

/** Refreshes everything a contribution change can affect: inputs, bills, loan payments, balances, chart and report. */
function useInvalidateContributions() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: cashFlowQueryKey }),
      queryClient.invalidateQueries({ queryKey: expensesQueryKey }),
      queryClient.invalidateQueries({ queryKey: loansQueryKey }),
      queryClient.invalidateQueries({ queryKey: cardsQueryKey }),
    ])
}

// Participants (shared bills FR-1).

export function useParticipants(includeArchived = false) {
  return useQuery({
    queryKey: [...participantsKey, includeArchived],
    queryFn: async () =>
      unwrap(await api.GET('/api/participants', { params: { query: { includeArchived } } })),
  })
}

export function useParticipant(id: string | undefined) {
  return useQuery({
    queryKey: [...participantsKey, 'one', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/participants/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

export type ParticipantChange =
  | { action: 'create'; request: ParticipantRequest }
  | { action: 'update'; id: string; request: ParticipantRequest }
  | { action: 'archive' | 'restore'; id: string }
  | { action: 'delete'; id: string }

export function useChangeParticipant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (change: ParticipantChange): Promise<Participant | void> => {
      switch (change.action) {
        case 'create':
          return unwrap(await api.POST('/api/participants', { body: change.request }))
        case 'update':
          return unwrap(
            await api.PUT('/api/participants/{id}', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'archive':
          return unwrap(
            await api.POST('/api/participants/{id}/archive', {
              params: { path: { id: change.id } },
            }),
          )
        case 'restore':
          return unwrap(
            await api.POST('/api/participants/{id}/restore', {
              params: { path: { id: change.id } },
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/participants/{id}', { params: { path: { id: change.id } } }),
          )
      }
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: participantsKey }),
        // Participants are payers: renaming one renames the payer of their contributions.
        queryClient.invalidateQueries({ queryKey: cashFlowQueryKey }),
        queryClient.invalidateQueries({ queryKey: ['payers'] }),
        queryClient.invalidateQueries({ queryKey: expensesQueryKey }),
        queryClient.invalidateQueries({ queryKey: loansQueryKey }),
      ]),
  })
}

// The Shared costs screen (FR-8).

export function useSharedCosts(month: string) {
  return useQuery({
    queryKey: [...sharedCostsQueryKey, month],
    queryFn: async () =>
      unwrap(await api.GET('/api/shared-costs', { params: { query: { month } } })),
  })
}

// Sharing one loan payment (FR-3).

export function useChangeLoanPaymentShare() {
  const invalidate = useInvalidateContributions()
  return useMutation({
    mutationFn: async ({
      loanId,
      month,
      kind,
      share,
    }: {
      loanId: string
      month: string
      kind: Schemas['LoanPaymentKind']
      /** A share, `null` for not shared, or `'reset'` to follow the loan again. */
      share: ShareRequest | null | 'reset'
    }) => {
      const path = { id: loanId, month, kind }
      return share === 'reset'
        ? unwrap(
            await api.DELETE('/api/loans/{id}/payments/{month}/{kind}/share', { params: { path } }),
          )
        : unwrap(
            await api.PUT('/api/loans/{id}/payments/{month}/{kind}/share', {
              params: { path },
              body: { share },
            }),
          )
    },
    onSuccess: invalidate,
  })
}

// Contributions (FR-5, FR-6): of a bill, or of a loan payment.

/** Identifies the shared cost of a contribution. */
export type ContributionRef =
  | { kind: 'Bill'; billId: string }
  | { kind: 'LoanPayment'; loanId: string; month: string; paymentKind: Schemas['LoanPaymentKind'] }

/** The cost of a contribution as the API returns it. */
export function contributionRefOf(contribution: Contribution | InputContribution): ContributionRef {
  return contribution.kind === 'Bill'
    ? { kind: 'Bill', billId: contribution.billId! }
    : {
        kind: 'LoanPayment',
        loanId: contribution.loanId!,
        month: contribution.month!,
        paymentKind: contribution.paymentKind!,
      }
}

/** Where the shared cost of a contribution is shown: the bill's edit page or the loan. */
export function contributionCostPath(contribution: Contribution | InputContribution): string {
  return contribution.kind === 'Bill'
    ? `/bills/${contribution.billId}/edit`
    : `/loans/${contribution.loanId}`
}

export type ContributionChange =
  | { action: 'edit'; ref: ContributionRef; request: ContributionRequest }
  | { action: 'received'; ref: ContributionRef; receivedDate?: string; accountId?: string }
  | { action: 'expected' | 'reset'; ref: ContributionRef }

export function useChangeContribution() {
  const invalidate = useInvalidateContributions()
  return useMutation({
    mutationFn: async (change: ContributionChange) => {
      const { ref } = change
      const received = {
        receivedDate: change.action === 'received' ? (change.receivedDate ?? null) : null,
        accountId: change.action === 'received' ? (change.accountId ?? null) : null,
      }
      if (ref.kind === 'Bill') {
        const params = { path: { id: ref.billId } }
        switch (change.action) {
          case 'edit':
            return unwrap(
              await api.PUT('/api/bills/{id}/contribution', { params, body: change.request }),
            )
          case 'received':
            return unwrap(
              await api.POST('/api/bills/{id}/contribution/received', { params, body: received }),
            )
          case 'expected':
            return unwrap(await api.DELETE('/api/bills/{id}/contribution/received', { params }))
          case 'reset':
            return unwrap(await api.POST('/api/bills/{id}/contribution/reset', { params }))
        }
      }
      const params = { path: { id: ref.loanId, month: ref.month, kind: ref.paymentKind } }
      switch (change.action) {
        case 'edit':
          return unwrap(
            await api.PUT('/api/loans/{id}/payments/{month}/{kind}/contribution', {
              params,
              body: change.request,
            }),
          )
        case 'received':
          return unwrap(
            await api.POST('/api/loans/{id}/payments/{month}/{kind}/contribution/received', {
              params,
              body: received,
            }),
          )
        case 'expected':
          return unwrap(
            await api.DELETE('/api/loans/{id}/payments/{month}/{kind}/contribution/received', {
              params,
            }),
          )
        case 'reset':
          return unwrap(
            await api.POST('/api/loans/{id}/payments/{month}/{kind}/contribution/reset', {
              params,
            }),
          )
      }
    },
    onSuccess: invalidate,
  })
}
