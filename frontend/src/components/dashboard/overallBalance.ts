import type { OverallBalance } from '@/api/cashFlow'
import { dashboardTexts } from './texts'

const t = dashboardTexts.overallBalance

/** What the figure covers and how reliable it is (FR-3 to FR-5), joined with " · " after the label. */
export function overallBalanceDetail(data: OverallBalance): string | undefined {
  if (data.balanceCents === null) return t.noPrimary
  const parts = [
    ...(data.primaryAccountCount > 1 ? [t.primaryAccounts(data.primaryAccountCount)] : []),
    ...(data.outputsWithoutAccountCount > 0
      ? [t.withoutAccount(data.outputsWithoutAccountCount)]
      : []),
    ...(data.missingStartingBalance ? [t.missingStartingBalance] : []),
  ]
  return parts.length > 0 ? parts.join(' · ') : undefined
}
