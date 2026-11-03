import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cardsQueryKey } from './cards'
import { cashFlowQueryKey } from './cashFlow'
import { api, type Schemas } from './client'
import { dashboardQueryKey } from './dashboard'
import { ensureOk, unwrap } from './http'

export type RevolvingCard = Schemas['RevolvingCardResponse']
export type RevolvingCards = Schemas['RevolvingCardsResponse']
export type RevolvingCardDetails = Schemas['RevolvingCardDetailsResponse']
export type RevolvingBill = Schemas['RevolvingBillResponse']
export type RevolvingTerms = Schemas['CardRevolvingResponse']
export type ExtraPayment = Schemas['ExtraPaymentResponse']
export type ExtraPaymentRequest = Schemas['ExtraPaymentRequest']
export type DebtEntry = Schemas['DebtEntryResponse']
export type DebtEntryRequest = Schemas['DebtEntryRequest']
export type DebtEntryKind = Schemas['DebtEntryKind']
export type RevolvingCharge = Schemas['RevolvingChargeResponse']
export type RevolvingMovement = Schemas['RevolvingMovementResponse']
export type MonthRevolving = Schemas['MonthRevolvingResponse']
export type RevolvingBillPaidRequest = Schemas['RevolvingBillPaidRequest']
export type RevolvingBillCorrectionRequest = Schemas['RevolvingBillCorrectionRequest']

/**
 * Revolving queries live under the card prefix, so every change that refreshes cards (expenses,
 * purchases, card settings) also refreshes the debt.
 */
export const revolvingQueryKey = [...cardsQueryKey, 'revolving'] as const

/** Refreshes everything a revolving change can affect: cards, debt, balances, chart, report and the Dashboard. */
function useInvalidateRevolving() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: cardsQueryKey }),
      queryClient.invalidateQueries({ queryKey: cashFlowQueryKey }),
      queryClient.invalidateQueries({ queryKey: dashboardQueryKey }),
    ])
}

export function useRevolvingCards() {
  return useQuery({
    queryKey: [...revolvingQueryKey, 'list'],
    queryFn: async () => unwrap(await api.GET('/api/revolving-cards')),
  })
}

/** A revolving card with its bills; `to` (yyyy-MM) adds estimated bills up to that month. */
export function useRevolvingCard(id: string | undefined, to?: string) {
  return useQuery({
    queryKey: [...revolvingQueryKey, 'card', id, to ?? null],
    queryFn: async () =>
      unwrap(
        await api.GET('/api/revolving-cards/{id}', {
          params: { path: { id: id! }, query: to ? { to } : {} },
        }),
      ),
    enabled: id !== undefined,
  })
}

/** The debt of a card at the end of a date, for "Pay the whole debt". */
export function useRevolvingDebt(id: string | undefined, date: string) {
  return useQuery({
    queryKey: [...revolvingQueryKey, 'debt', id, date],
    queryFn: async () =>
      unwrap(
        await api.GET('/api/revolving-cards/{id}/debt', {
          params: { path: { id: id! }, query: { date } },
        }),
      ),
    enabled: id !== undefined && id !== '' && date !== '',
  })
}

export function useMonthRevolving(month: string) {
  return useQuery({
    queryKey: [...revolvingQueryKey, 'month', month],
    queryFn: async () =>
      unwrap(await api.GET('/api/revolving-bills', { params: { query: { month } } })),
  })
}

export type RevolvingBillAction =
  | { action: 'paid'; request: RevolvingBillPaidRequest }
  | { action: 'unpaid' }
  | { action: 'correct'; request: RevolvingBillCorrectionRequest }
  | { action: 'clearCorrection' }

export function useChangeRevolvingBill(cardId: string, month: string) {
  const invalidate = useInvalidateRevolving()
  return useMutation({
    mutationFn: async (change: RevolvingBillAction) => {
      const params = { path: { id: cardId, month } }
      switch (change.action) {
        case 'paid':
          return unwrap(
            await api.POST('/api/revolving-cards/{id}/bills/{month}/paid', {
              params,
              body: change.request,
            }),
          )
        case 'unpaid':
          return unwrap(
            await api.DELETE('/api/revolving-cards/{id}/bills/{month}/paid', { params }),
          )
        case 'correct':
          return unwrap(
            await api.PUT('/api/revolving-cards/{id}/bills/{month}/correction', {
              params,
              body: change.request,
            }),
          )
        case 'clearCorrection':
          return unwrap(
            await api.DELETE('/api/revolving-cards/{id}/bills/{month}/correction', { params }),
          )
      }
    },
    onSuccess: invalidate,
  })
}

export function useCreateExtraPayment() {
  const invalidate = useInvalidateRevolving()
  return useMutation({
    mutationFn: async ({ cardId, request }: { cardId: string; request: ExtraPaymentRequest }) =>
      unwrap(
        await api.POST('/api/revolving-cards/{id}/extra-payments', {
          params: { path: { id: cardId } },
          body: request,
        }),
      ),
    onSuccess: invalidate,
  })
}

export function useUpdateExtraPayment(id: string) {
  const invalidate = useInvalidateRevolving()
  return useMutation({
    mutationFn: async (request: ExtraPaymentRequest) =>
      unwrap(
        await api.PUT('/api/extra-payments/{id}', { params: { path: { id } }, body: request }),
      ),
    onSuccess: invalidate,
  })
}

export function useDeleteExtraPayment() {
  const invalidate = useInvalidateRevolving()
  return useMutation({
    mutationFn: async (id: string) =>
      ensureOk(await api.DELETE('/api/extra-payments/{id}', { params: { path: { id } } })),
    onSuccess: invalidate,
  })
}

export type DebtEntryAction =
  | { action: 'create'; request: DebtEntryRequest }
  | { action: 'update'; entryId: string; request: DebtEntryRequest }
  | { action: 'delete'; entryId: string }

export function useChangeDebtEntry(cardId: string) {
  const invalidate = useInvalidateRevolving()
  return useMutation({
    mutationFn: async (change: DebtEntryAction) => {
      switch (change.action) {
        case 'create':
          return unwrap(
            await api.POST('/api/revolving-cards/{id}/debt-entries', {
              params: { path: { id: cardId } },
              body: change.request,
            }),
          )
        case 'update':
          return unwrap(
            await api.PUT('/api/debt-entries/{id}', {
              params: { path: { id: change.entryId } },
              body: change.request,
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/debt-entries/{id}', {
              params: { path: { id: change.entryId } },
            }),
          )
      }
    },
    onSuccess: invalidate,
  })
}
