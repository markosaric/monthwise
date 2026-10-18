import { useHealth } from '@/api/health'

/**
 * What is wrong with the API or the database, or null when both are healthy or the first check has
 * not finished yet (FR-8).
 */
export function useSystemProblem(): string | null {
  const { data, isPending, isError } = useHealth()
  if (isPending) return null
  if (isError) return 'API unreachable'
  if (data.database !== 'Healthy') {
    return data.database === 'Degraded' ? 'Database degraded' : 'Database unhealthy'
  }
  return null
}
