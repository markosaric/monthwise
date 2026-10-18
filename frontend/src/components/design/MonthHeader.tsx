import { ChevronLeft, ChevronRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { Button } from '@/components/ui/button'
import { addMonths, currentMonth, formatYearMonth } from '@/lib/dates'

const t = cashFlowTexts

const arrowClass =
  'grid size-8 place-items-center rounded-lg border bg-card text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none'

/**
 * A month screen's heading with the previous and next month arrows right next to it, and This
 * month when another month is shown (FR-15). `title` defaults to the month's name.
 */
export function MonthHeader({
  month,
  basePath,
  title,
  aside,
}: {
  month: string
  basePath: string
  title?: string
  aside?: ReactNode
}) {
  const previous = addMonths(month, -1)
  const next = addMonths(month, 1)
  return (
    <header className="flex flex-wrap items-center gap-2">
      <nav aria-label="Month" className="flex items-center gap-2">
        <Link
          to={`${basePath}/${previous}`}
          aria-label={t.previousMonth}
          title={formatYearMonth(previous)}
          className={arrowClass}
        >
          <ChevronLeft className="size-4" aria-hidden />
        </Link>
        <h1 className="text-2xl font-bold tracking-tight">{title ?? formatYearMonth(month)}</h1>
        <Link
          to={`${basePath}/${next}`}
          aria-label={t.nextMonth}
          title={formatYearMonth(next)}
          className={arrowClass}
        >
          <ChevronRight className="size-4" aria-hidden />
        </Link>
        {month !== currentMonth() && (
          <Button asChild variant="ghost" size="sm">
            <Link to={`${basePath}/${currentMonth()}`}>{t.thisMonth}</Link>
          </Button>
        )}
      </nav>
      {aside && <span className="ml-auto text-sm text-muted-foreground">{aside}</span>}
    </header>
  )
}
