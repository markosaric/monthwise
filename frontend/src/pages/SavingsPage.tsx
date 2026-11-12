import { PiggyBank } from 'lucide-react'
import { sectionColors } from '@/components/design/colors'
import { IconTile } from '@/components/design/IconTile'
import { PageHeader } from './PageHeader'

/** The Savings screen is planned and not built yet (planned-features FR-4). */
export function SavingsPage() {
  return (
    <>
      <PageHeader title="Savings" description="Account balances over time." />
      <section
        aria-label="Planned"
        className="flex items-center gap-3 rounded-xl border-l-4 bg-card p-4 text-card-foreground shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8"
        style={{ borderLeftColor: sectionColors.savings }}
      >
        <IconTile icon={PiggyBank} color={sectionColors.savings} />
        <p className="text-sm">
          This screen is planned and not built yet. It will show how your savings account balances
          change over time.
        </p>
      </section>
    </>
  )
}
