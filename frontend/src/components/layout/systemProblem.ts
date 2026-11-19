import { useHealth } from '@/api/health'
import { appTexts } from '@/components/common/texts'

/**
 * What is wrong with the API or the database, or null when both are healthy or the first check has
 * not finished yet (FR-8).
 */
export function useSystemProblem(): string | null {
  const { data, isPending, isError } = useHealth()
  if (isPending) return null
  if (isError) return appTexts.layout.apiUnreachable
  if (data.migrations === 'Failed') return appTexts.layout.databaseUpdateFailed
  if (data.database !== 'Healthy') {
    return data.database === 'Degraded'
      ? appTexts.layout.databaseDegraded
      : appTexts.layout.databaseUnhealthy
  }
  return null
}
