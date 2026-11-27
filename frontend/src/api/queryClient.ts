import {
  MutationCache,
  QueryCache,
  QueryClient,
  type QueryClientConfig,
} from '@tanstack/react-query'
import { authQueryKey } from './auth'
import { dataQueryKey } from './dataTransfer'
import { healthQueryKey } from './health'
import { ApiError } from './http'

declare module '@tanstack/react-query' {
  interface Register {
    mutationMeta: {
      /**
       * `false` keeps a mutation out of the shared refresh below; it then refreshes what it needs
       * itself (import, export and the export reminder).
       */
      refreshAll?: boolean
    }
  }
}

/**
 * Queries a saved change never refreshes: the status check keeps its own timing and the backup data
 * only changes through its own mutations (screen refresh FR-4).
 */
const keptQueries = new Set<unknown>([healthQueryKey[0], dataQueryKey[0], authQueryKey[0]])

/** A call refused because the session ended (signed out elsewhere, deactivated, password reset: login FR-6, FR-7). */
const sessionEnded = (error: unknown) => error instanceof ApiError && error.status === 401

/**
 * The query client of the app. After every successful mutation it refreshes all shown data, so
 * every screen shows each saved change at once and no feature lists which screens a change affects
 * (screen refresh FR-1, FR-2). Queries that are not on screen are marked stale and load again when
 * their screen opens. The refresh is awaited, so a mutation stays pending until the new data is in.
 * A failed mutation refreshes nothing (FR-3).
 */
export function createQueryClient(
  config: Omit<QueryClientConfig, 'mutationCache' | 'queryCache'> = {},
) {
  // When the session has ended, asking who is signed in again shows the sign-in screen (login FR-2).
  const checkSession = (error: unknown) => {
    if (sessionEnded(error)) void queryClient.invalidateQueries({ queryKey: authQueryKey })
  }
  const queryClient: QueryClient = new QueryClient({
    ...config,
    queryCache: new QueryCache({
      onError: (error, query) => {
        if (query.queryKey[0] !== authQueryKey[0]) checkSession(error)
      },
    }),
    mutationCache: new MutationCache({
      onError: checkSession,
      onSuccess: (_data, _variables, _context, mutation) => {
        if (mutation.meta?.refreshAll === false) return
        return queryClient.invalidateQueries({
          predicate: (query) => !keptQueries.has(query.queryKey[0]),
        })
      },
    }),
  })
  return queryClient
}
