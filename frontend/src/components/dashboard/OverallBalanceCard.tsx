import { Wallet } from 'lucide-react'
import { useOverallBalance } from '@/api/cashFlow'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { StatCard } from '@/components/design/StatCard'
import { formatCents } from '@/lib/money'
import { overallBalanceDetail } from './overallBalance'
import { dashboardTexts } from './texts'

const t = dashboardTexts.overallBalance

/**
 * The last key figure on the Dashboard and the Month screen (overall balance tile FR-1 to FR-6): the
 * balance of all primary accounts at the end of the month once everything is received and paid. It
 * opens the Cards screen and has no change pill.
 */
export function OverallBalanceCard({ month }: { month: string }) {
  const { data } = useOverallBalance(month)
  const balance = data?.balanceCents ?? null
  return (
    <StatCard
      icon={Wallet}
      color={balance !== null && balance < 0 ? meaningColors.problem : sectionColors.cards}
      label={t.label}
      value={data === undefined ? '…' : balance === null ? '–' : formatCents(balance)}
      detail={data ? overallBalanceDetail(data) : undefined}
      problem={balance !== null && balance < 0}
      to="/cards"
    />
  )
}
