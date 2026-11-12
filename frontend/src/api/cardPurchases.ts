import { useMutation, useQuery } from '@tanstack/react-query'
import { api, type Schemas } from './client'
import { ensureOk, unwrap } from './http'

export type InstallmentCard = Schemas['InstallmentCardResponse']
export type CardPayment = Schemas['CardPaymentResponse']
export type CardPaymentDetails = Schemas['CardPaymentDetailsResponse']
export type CardPaymentLine = Schemas['CardPaymentLineResponse']
export type CardPaymentLineKind = Schemas['CardPaymentLineKind']
export type CardPurchase = Schemas['CardPurchaseResponse']
export type CardPurchaseDetails = Schemas['CardPurchaseDetailsResponse']
export type CardPurchaseRequest = Schemas['CardPurchaseRequest']
export type Installment = Schemas['InstallmentResponse']
export type MonthCardPayments = Schemas['MonthCardPaymentsResponse']
export type CardPaymentPaidRequest = Schemas['CardPaymentPaidRequest']

/** The numbers of installments a card can allow at most (installments B5). */
export const maxInstallmentsChoices = [12, 24, 36] as const

/** Every card purchase query shares this prefix. Saved changes refresh it through the shared rule in queryClient.ts. */
export const cardPurchasesQueryKey = ['card-purchases'] as const

export function useInstallmentCards() {
  return useQuery({
    queryKey: [...cardPurchasesQueryKey, 'cards'],
    queryFn: async () => unwrap(await api.GET('/api/card-purchases/cards')),
  })
}

export function useInstallmentCard(cardId: string | undefined) {
  return useQuery({
    queryKey: [...cardPurchasesQueryKey, 'card', cardId],
    queryFn: async () =>
      unwrap(await api.GET('/api/cards/{id}/installments', { params: { path: { id: cardId! } } })),
    enabled: cardId !== undefined,
  })
}

export function useCardPurchases(cardId: string | undefined) {
  return useQuery({
    queryKey: [...cardPurchasesQueryKey, 'card', cardId, 'purchases'],
    queryFn: async () =>
      unwrap(await api.GET('/api/cards/{id}/purchases', { params: { path: { id: cardId! } } })),
    enabled: cardId !== undefined,
  })
}

export function useCardPayments(cardId: string | undefined) {
  return useQuery({
    queryKey: [...cardPurchasesQueryKey, 'card', cardId, 'payments'],
    queryFn: async () =>
      unwrap(await api.GET('/api/cards/{id}/card-payments', { params: { path: { id: cardId! } } })),
    enabled: cardId !== undefined,
  })
}

export function useCardPayment(cardId: string | undefined, month: string | undefined) {
  return useQuery({
    queryKey: [...cardPurchasesQueryKey, 'card', cardId, 'payments', month],
    queryFn: async () =>
      unwrap(
        await api.GET('/api/cards/{id}/card-payments/{month}', {
          params: { path: { id: cardId!, month: month! } },
        }),
      ),
    enabled: cardId !== undefined && month !== undefined,
  })
}

export function useMonthCardPayments(month: string) {
  return useQuery({
    queryKey: [...cardPurchasesQueryKey, 'month', month],
    queryFn: async () =>
      unwrap(await api.GET('/api/card-payments', { params: { query: { month } } })),
  })
}

export function useCardPurchase(id: string | undefined) {
  return useQuery({
    queryKey: [...cardPurchasesQueryKey, 'purchase', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/card-purchases/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

export function useCreateCardPurchase() {
  return useMutation({
    mutationFn: async (request: CardPurchaseRequest) =>
      unwrap(await api.POST('/api/card-purchases', { body: request })),
  })
}

export function useUpdateCardPurchase(id: string) {
  return useMutation({
    mutationFn: async (request: CardPurchaseRequest) =>
      unwrap(
        await api.PUT('/api/card-purchases/{id}', { params: { path: { id } }, body: request }),
      ),
  })
}

export function useDeleteCardPurchase(id: string) {
  return useMutation({
    mutationFn: async () =>
      ensureOk(await api.DELETE('/api/card-purchases/{id}', { params: { path: { id } } })),
  })
}

export type ReversalAction =
  | { action: 'purchase'; reason: string | null }
  | { action: 'installment'; number: number; reason: string | null }

/** Reverses the whole purchase or one installment (FR-5). */
export function useReverseCardPurchase(id: string) {
  return useMutation({
    mutationFn: async (change: ReversalAction) =>
      change.action === 'purchase'
        ? unwrap(
            await api.POST('/api/card-purchases/{id}/reverse', {
              params: { path: { id } },
              body: { reason: change.reason },
            }),
          )
        : unwrap(
            await api.POST('/api/card-purchases/{id}/installments/{number}/reverse', {
              params: { path: { id, number: change.number } },
              body: { reason: change.reason },
            }),
          ),
  })
}

export type CardPaymentAction =
  | { action: 'paid'; cardId: string; month: string; request: CardPaymentPaidRequest }
  | { action: 'unpaid'; cardId: string; month: string }

/** Ticks Paid (or corrects the paid date and account) or unticks a card payment (FR-8). */
export function useCardPaymentAction() {
  return useMutation({
    mutationFn: async (change: CardPaymentAction) => {
      const path = { id: change.cardId, month: change.month }
      return change.action === 'paid'
        ? unwrap(
            await api.POST('/api/cards/{id}/card-payments/{month}/paid', {
              params: { path },
              body: change.request,
            }),
          )
        : unwrap(
            await api.DELETE('/api/cards/{id}/card-payments/{month}/paid', { params: { path } }),
          )
    },
  })
}
