import { useMutation, useQuery } from '@tanstack/react-query'
import { api, type Schemas } from './client'
import { unwrap } from './http'

export { ApiError } from './http'

export type Card = Schemas['CardResponse']
export type CardRequest = Schemas['CardRequest']
export type CardStatus = Schemas['CardStatus']
export type CardType = Schemas['CardType']
export type CardListFilter = NonNullable<Schemas['CardListFilter']>
export type CardListEntry = Schemas['CardListEntryResponse']
export type DashboardCards = Schemas['DashboardCardsResponse']
export type SensitiveNumberKind = Schemas['SensitiveNumberKind']
export type ListKind = 'issuers' | 'banks'

export const cardTypes: CardType[] = ['Debit', 'Charge', 'Credit', 'Prepaid', 'Revolving']

/** All card queries share this prefix. Saved changes refresh it through the shared rule in queryClient.ts. */
export const cardsQueryKey = ['cards'] as const

export function useCards(filter: CardListFilter) {
  return useQuery({
    queryKey: [...cardsQueryKey, 'list', filter],
    queryFn: async () =>
      unwrap(await api.GET('/api/cards', { params: { query: { show: filter } } })),
  })
}

export function useCard(id: string | undefined) {
  return useQuery({
    queryKey: [...cardsQueryKey, 'detail', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/cards/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

export function useDashboardCards() {
  return useQuery({
    queryKey: [...cardsQueryKey, 'dashboard'],
    queryFn: async () => unwrap(await api.GET('/api/cards/dashboard')),
  })
}

/**
 * Fetches one full number when the user presses Reveal. Deliberately not a cached query:
 * the value lives only in the component that shows it.
 */
export async function revealCardNumber(id: string, kind: SensitiveNumberKind): Promise<string> {
  const result = await api.GET('/api/cards/{id}/numbers/{kind}', {
    params: { path: { id, kind } },
    cache: 'no-store',
  })
  return unwrap(result).value
}

export function useCreateCard() {
  return useMutation({
    mutationFn: async ({ request, replaces }: { request: CardRequest; replaces?: string }) =>
      unwrap(
        await api.POST('/api/cards', {
          body: request,
          params: { query: replaces ? { replaces } : {} },
        }),
      ),
  })
}

export function useUpdateCard(id: string) {
  return useMutation({
    mutationFn: async (request: CardRequest) =>
      unwrap(await api.PUT('/api/cards/{id}', { params: { path: { id } }, body: request })),
  })
}

export type CardAction =
  | { action: 'block'; reason: string | null; replacementCardId?: string | null }
  | { action: 'unblock' }
  | { action: 'expire'; reason: string | null; replacementCardId: string | null }
  | { action: 'archive' }

export function useCardAction(id: string) {
  return useMutation({
    mutationFn: async (change: CardAction) => {
      const params = { path: { id } }
      switch (change.action) {
        case 'block':
          return unwrap(
            await api.POST('/api/cards/{id}/block', {
              params,
              body: { reason: change.reason, replacementCardId: change.replacementCardId ?? null },
            }),
          )
        case 'unblock':
          return unwrap(await api.POST('/api/cards/{id}/unblock', { params }))
        case 'expire':
          return unwrap(
            await api.POST('/api/cards/{id}/expire', {
              params,
              body: { reason: change.reason, replacementCardId: change.replacementCardId },
            }),
          )
        case 'archive':
          return unwrap(await api.POST('/api/cards/{id}/archive', { params }))
      }
    },
  })
}

const listPaths = { issuers: '/api/card-issuers', banks: '/api/banks' } as const

export function useCardList(kind: ListKind) {
  return useQuery({
    queryKey: ['card-lists', kind],
    queryFn: async () => unwrap(await api.GET(listPaths[kind])),
    staleTime: 5 * 60_000,
  })
}

export function useAddCardListEntry(kind: ListKind) {
  return useMutation({
    mutationFn: async (name: string) => unwrap(await api.POST(listPaths[kind], { body: { name } })),
  })
}
