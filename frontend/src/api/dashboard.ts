import { useQuery } from '@tanstack/react-query'
import { api, type Schemas } from './client'
import { unwrap } from './http'

export type Dashboard = Schemas['DashboardResponse']
export type DashboardItem = Schemas['DashboardItemResponse']

export const dashboardQueryKey = ['dashboard'] as const

/**
 * The Dashboard of the current month (design refresh FR-10). It is fetched again every time the
 * Dashboard opens, so it always shows what was recorded on other screens.
 */
export function useDashboard() {
  return useQuery({
    queryKey: dashboardQueryKey,
    queryFn: async () => unwrap(await api.GET('/api/dashboard')),
    refetchOnMount: 'always',
  })
}
