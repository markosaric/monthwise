import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Link, Navigate, useParams } from 'react-router'
import { IncomeSection } from '@/components/cashflow/IncomeSection'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { ExpensesSection } from '@/components/loans/ExpensesSection'
import { Button } from '@/components/ui/button'
import { addMonths, currentMonth, formatYearMonth, isYearMonth } from '@/lib/dates'
import { PageHeader } from './PageHeader'

const t = cashFlowTexts

/** The Month screen; `/month` is the current month, `/month/yyyy-MM` another one. */
export function MonthPage() {
  const { yearMonth } = useParams()
  if (yearMonth !== undefined && !isYearMonth(yearMonth)) return <Navigate to="/month" replace />
  const month = yearMonth ?? currentMonth()

  return (
    <>
      <PageHeader
        title={formatYearMonth(month)}
        description="Income and loan payments of the month: received and paid status, and totals."
      />
      <MonthNavigation month={month} basePath="/month" />
      <div className="flex flex-col gap-6">
        <IncomeSection month={month} />
        <ExpensesSection month={month} />
      </div>
    </>
  )
}

/** Previous / this / next month buttons. */
export function MonthNavigation({ month, basePath }: { month: string; basePath: string }) {
  return (
    <nav aria-label="Month" className="mb-4 flex flex-wrap items-center gap-2">
      <Button asChild variant="outline" size="sm">
        <Link to={`${basePath}/${addMonths(month, -1)}`} aria-label={t.previousMonth}>
          <ChevronLeft className="size-4" aria-hidden />
          {formatYearMonth(addMonths(month, -1), 'short')}
        </Link>
      </Button>
      {month !== currentMonth() && (
        <Button asChild variant="ghost" size="sm">
          <Link to={`${basePath}/${currentMonth()}`}>{t.thisMonth}</Link>
        </Button>
      )}
      <Button asChild variant="outline" size="sm">
        <Link to={`${basePath}/${addMonths(month, 1)}`} aria-label={t.nextMonth}>
          {formatYearMonth(addMonths(month, 1), 'short')}
          <ChevronRight className="size-4" aria-hidden />
        </Link>
      </Button>
    </nav>
  )
}
