import { useQuery } from '@tanstack/react-query'
import { api, type Schemas } from './client'

export type HealthResponse = Schemas['HealthResponse']

export const healthQueryKey = ['health'] as const

/** API and database status. A 503 still carries a body, so it is returned rather than thrown. */
export async function fetchHealth(): Promise<HealthResponse> {
  const { data, error } = await api.GET('/api/health')
  const body = data ?? error
  if (!body) {
    throw new Error('The API did not return a health status.')
  }
  return body
}

export function useHealth() {
  return useQuery({
    queryKey: healthQueryKey,
    queryFn: fetchHealth,
    refetchInterval: 30_000,
    retry: false,
  })
}
