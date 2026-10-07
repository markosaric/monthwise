import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
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

export const cardTypes: CardType[] = ['Debit', 'Charge', 'Credit', 'Prepaid']

/** All card queries share this prefix, so one invalidation refreshes Settings and the Dashboard. */
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

function useInvalidateCards() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: cardsQueryKey })
}

export function useCreateCard() {
  const invalidate = useInvalidateCards()
  return useMutation({
    mutationFn: async ({ request, replaces }: { request: CardRequest; replaces?: string }) =>
      unwrap(
        await api.POST('/api/cards', {
          body: request,
          params: { query: replaces ? { replaces } : {} },
        }),
      ),
    onSuccess: invalidate,
  })
}

export function useUpdateCard(id: string) {
  const invalidate = useInvalidateCards()
  return useMutation({
    mutationFn: async (request: CardRequest) =>
      unwrap(await api.PUT('/api/cards/{id}', { params: { path: { id } }, body: request })),
    onSuccess: invalidate,
  })
}

export type CardAction =
  | { action: 'block'; reason: string | null }
  | { action: 'unblock' }
  | { action: 'expire'; reason: string | null; replacementCardId: string | null }
  | { action: 'archive' }

export function useCardAction(id: string) {
  const invalidate = useInvalidateCards()
  return useMutation({
    mutationFn: async (change: CardAction) => {
      const params = { path: { id } }
      switch (change.action) {
        case 'block':
          return unwrap(
            await api.POST('/api/cards/{id}/block', { params, body: { reason: change.reason } }),
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
    onSuccess: invalidate,
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
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (name: string) => unwrap(await api.POST(listPaths[kind], { body: { name } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['card-lists', kind] }),
  })
}
