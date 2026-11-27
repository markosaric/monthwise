import { useMutation, useQuery } from '@tanstack/react-query'
import { api, type Schemas } from './client'
import { ensureOk, unwrap } from './http'

export type User = Schemas['UserResponse']
export type UserRole = Schemas['UserRole']
export type UserList = Schemas['UserListResponse']
export type NewUserRequest = Schemas['NewUserRequest']
export type UserRequest = Schemas['UserRequest']

/** Every user query shares this prefix. Saved changes refresh them through the shared rule in queryClient.ts. */
export const usersQueryKey = ['users'] as const

/** The Users screen: every user for a household admin, only oneself for a member (login FR-6). */
export function useUsers() {
  return useQuery({
    queryKey: [...usersQueryKey, 'list'],
    queryFn: async () => unwrap(await api.GET('/api/users')),
  })
}

export function useUser(id: string | undefined) {
  return useQuery({
    queryKey: [...usersQueryKey, 'one', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/users/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

export type UserChange =
  | { action: 'create'; request: NewUserRequest }
  | { action: 'update'; id: string; request: UserRequest }
  | { action: 'delete'; id: string }
  | { action: 'resetPassword'; id: string; temporaryPassword: string }

export function useChangeUser() {
  return useMutation({
    mutationFn: async (change: UserChange): Promise<User | void> => {
      switch (change.action) {
        case 'create':
          return unwrap(await api.POST('/api/users', { body: change.request }))
        case 'update':
          return unwrap(
            await api.PUT('/api/users/{id}', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/users/{id}', { params: { path: { id: change.id } } }),
          )
        case 'resetPassword':
          return unwrap(
            await api.POST('/api/users/{id}/password', {
              params: { path: { id: change.id } },
              body: { temporaryPassword: change.temporaryPassword },
            }),
          )
      }
    },
  })
}
