import { MutationObserver, type QueryClient } from '@tanstack/react-query'
import { createQueryClient } from './queryClient'

const shownData = [
  ['dashboard'],
  ['cash-flow', 'month', '2026-10'],
  ['expenses', 'bills', '2026-10'],
  ['loans', 'month', '2026-10'],
  ['card-purchases', 'cards'],
  ['cards', 'overall-balance', '2026-10'],
  ['card-lists', 'banks'],
  ['participants', false],
] as const
const keptData = [['health'], ['data-transfer', 'overview']] as const

function seed(queryClient: QueryClient) {
  for (const key of [...shownData, ...keptData]) queryClient.setQueryData(key, 'loaded')
}

const isInvalidated = (queryClient: QueryClient, key: readonly unknown[]) =>
  queryClient.getQueryState(key)?.isInvalidated

function mutate(
  queryClient: QueryClient,
  mutationFn: () => Promise<unknown>,
  meta?: { refreshAll?: boolean },
) {
  return new MutationObserver(queryClient, { mutationFn, meta }).mutate().catch(() => undefined)
}

describe('refresh after a saved change (screen refresh FR-1 to FR-4)', () => {
  it('refreshes all shown data after any successful change', async () => {
    const queryClient = createQueryClient()
    seed(queryClient)

    await mutate(queryClient, async () => 'saved')

    for (const key of shownData) expect(isInvalidated(queryClient, key)).toBe(true)
  })

  it('keeps the status check and the backup data out of the refresh', async () => {
    const queryClient = createQueryClient()
    seed(queryClient)

    await mutate(queryClient, async () => 'saved')

    for (const key of keptData) expect(isInvalidated(queryClient, key)).toBe(false)
  })

  it('refreshes nothing when the change fails', async () => {
    const queryClient = createQueryClient()
    seed(queryClient)

    await mutate(queryClient, async () => {
      throw new Error('failed')
    })

    for (const key of shownData) expect(isInvalidated(queryClient, key)).toBe(false)
  })

  it('refreshes nothing for a change that opts out', async () => {
    const queryClient = createQueryClient()
    seed(queryClient)

    await mutate(queryClient, async () => 'saved', { refreshAll: false })

    for (const key of shownData) expect(isInvalidated(queryClient, key)).toBe(false)
  })
})
