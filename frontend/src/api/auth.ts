import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Schemas } from './client'
import { ensureOk, unwrap } from './http'

export type AuthState = Schemas['AuthStateResponse']
export type CurrentUser = Schemas['UserResponse']
export type SignInRequest = Schemas['SignInRequest']
export type SetupRequest = Schemas['SetupRequest']
export type PasswordChangeRequest = Schemas['PasswordChangeRequest']

/** Whether setup is needed and who is signed in (login FR-1, FR-2). */
export const authQueryKey = ['auth'] as const

export function useAuthState() {
  return useQuery({
    queryKey: authQueryKey,
    queryFn: async () => unwrap(await api.GET('/api/auth/state')),
    retry: false,
    // Loaded once and again only when invalidated: after signing in or out, or when a call answers
    // 401. A screen that mounts later never refetches it, also not after an error (API unreachable).
    staleTime: Infinity,
    retryOnMount: false,
  })
}

/** The signed-in user, once the sign-in screen let them in. */
export function useCurrentUser(): CurrentUser | undefined {
  return useAuthState().data?.user ?? undefined
}

/**
 * After signing in, setting up or signing out every query loads again for the new user, starting
 * with who is signed in.
 */
function useStartOver() {
  const queryClient = useQueryClient()
  return async () => {
    queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== authQueryKey[0] })
    await queryClient.invalidateQueries({ queryKey: authQueryKey })
  }
}

export function useSignIn() {
  const startOver = useStartOver()
  return useMutation({
    meta: { refreshAll: false },
    mutationFn: async (request: SignInRequest) =>
      ensureOk(await api.POST('/api/auth/sign-in', { body: request })),
    onSuccess: startOver,
  })
}

export function useSetup() {
  const startOver = useStartOver()
  return useMutation({
    meta: { refreshAll: false },
    mutationFn: async (request: SetupRequest) =>
      ensureOk(await api.POST('/api/auth/setup', { body: request })),
    onSuccess: startOver,
  })
}

export function useSignOut() {
  const startOver = useStartOver()
  return useMutation({
    meta: { refreshAll: false },
    mutationFn: async () => ensureOk(await api.POST('/api/auth/sign-out')),
    onSettled: startOver,
  })
}

/** Changes the signed-in user's own password (FR-7, FR-8). */
export function useChangePassword() {
  const queryClient = useQueryClient()
  return useMutation({
    meta: { refreshAll: false },
    mutationFn: async (request: PasswordChangeRequest) =>
      ensureOk(await api.POST('/api/auth/password', { body: request })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: authQueryKey }),
  })
}
