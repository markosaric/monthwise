import { ArrowDownLeft, ArrowUpRight, CircleAlert, Clock, Equal } from 'lucide-react'
import { Navigate, useParams } from 'react-router'
import { useMonthCardPayments } from '@/api/cardPurchases'
import { useMonthlyReport } from '@/api/cashFlow'
import { useMonthBills, useMonthExpenses } from '@/api/expenses'
import { useMonthLoanPayments } from '@/api/loans'
import { useMonthRevolving } from '@/api/revolving'
import { CardPaymentsSection } from '@/components/cardPurchases/CardPaymentsSection'
import { IncomeSection } from '@/components/cashflow/IncomeSection'
import { OverallBalanceCard } from '@/components/dashboard/OverallBalanceCard'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { MonthHeader } from '@/components/design/MonthHeader'
import { ProgressBar } from '@/components/design/ProgressBar'
import { RingChart } from '@/components/design/RingChart'
import { StatCard, StatGrid } from '@/components/design/StatCard'
import { BillsSection } from '@/components/expenses/BillsSection'
import { ExpensesSection } from '@/components/expenses/ExpensesSection'
import { LoanPaymentsSection } from '@/components/loans/LoanPaymentsSection'
import { RevolvingMonthSection } from '@/components/revolving/RevolvingMonthSection'
import { Card, CardContent } from '@/components/ui/card'
import { currentMonth, isYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { appTexts } from '@/components/common/texts'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { dashboardTexts as d } from '@/components/dashboard/texts'
import { expenseTexts } from '@/components/expenses/texts'
import { loanTexts } from '@/components/loans/texts'

/** The Month screen; `/month` is the current month, `/month/yyyy-MM` another one (design refresh FR-15). */
export function MonthPage() {
  const { yearMonth } = useParams()
  if (yearMonth !== undefined && !isYearMonth(yearMonth)) return <Navigate to="/month" replace />
  const month = yearMonth ?? currentMonth()

  return (
    <div className="flex max-w-5xl flex-col gap-4">
      <MonthHeader month={month} basePath="/month" />
      <MonthSummary month={month} />
      <IncomeSection month={month} />
      <BillsSection month={month} />
      <ExpensesSection month={month} />
      <LoanPaymentsSection month={month} />
      <CardPaymentsSection month={month} />
      <RevolvingMonthSection month={month} />
    </div>
  )
}

/**
 * Key figures, received and paid progress, and the month's outputs by kind (FR-15). The figures come
 * from the monthly report, the outputs by kind from the section totals.
 */
function MonthSummary({ month }: { month: string }) {
  const { data: report } = useMonthlyReport(month)
  const { data: bills } = useMonthBills(month)
  const { data: expenses } = useMonthExpenses(month)
  const { data: loanPayments } = useMonthLoanPayments(month)
  const { data: cardPayments } = useMonthCardPayments(month)
  const { data: revolving } = useMonthRevolving(month)
  if (!report) return null

  const inputs = report.totals
  const outputs = report.outputs.totals
  const overdue =
    (bills?.bills.filter((b) => b.isOverdue).length ?? 0) +
    (loanPayments?.payments.filter((p) => p.isOverdue).length ?? 0) +
    (cardPayments?.payments.filter((p) => p.isOverdue).length ?? 0) +
    (revolving?.bills.filter((b) => b.isOverdue).length ?? 0)

  return (
    <>
      <StatGrid label={d.keyFigures} five>
        <StatCard
          icon={ArrowDownLeft}
          color={meaningColors.moneyIn}
          label={d.inputs}
          detail={d.received(formatCents(inputs.receivedCents))}
          value={formatCents(inputs.totalCents)}
        />
        <StatCard
          icon={ArrowUpRight}
          color={meaningColors.moneyOut}
          label={d.outputs}
          detail={d.paid(formatCents(outputs.paidCents))}
          value={formatCents(outputs.totalCents)}
        />
        <StatCard
          icon={Equal}
          color={meaningColors.highlight}
          label={d.monthResult}
          value={formatCents(report.monthResultCents)}
          problem={report.monthResultCents < 0}
        />
        <StatCard
          icon={overdue > 0 ? CircleAlert : Clock}
          color={overdue > 0 ? meaningColors.problem : meaningColors.expense}
          label={d.stillToPay}
          detail={overdue > 0 ? d.overdueItems(overdue) : undefined}
          value={formatCents(outputs.expectedCents)}
          problem={overdue > 0}
        />
        <OverallBalanceCard month={month} />
      </StatGrid>
      <Card>
        <CardContent className="grid items-center gap-6 md:grid-cols-[1fr_auto]">
          <div className="flex flex-col gap-3">
            <Progress
              label={appTexts.month.inputsReceived}
              value={inputs.receivedCents}
              max={inputs.totalCents}
              color={meaningColors.moneyIn}
            />
            <Progress
              label={appTexts.month.outputsPaid}
              value={outputs.paidCents}
              max={outputs.totalCents}
              color={meaningColors.moneyOut}
            />
          </div>
          <RingChart
            label={appTexts.month.outputsByKind}
            slices={[
              {
                label: expenseTexts.bills.title,
                cents: bills?.totals.totalCents ?? 0,
                color: meaningColors.moneyOut,
              },
              {
                label: expenseTexts.expenses.title,
                cents: expenses?.totals.fromAccountsCents ?? 0,
                color: meaningColors.expense,
              },
              {
                label: loanTexts.expenses.title,
                cents: loanPayments?.totals.totalCents ?? 0,
                color: sectionColors.loans,
              },
              {
                label: cardPurchaseTexts.monthTitle,
                cents: (cardPayments?.totals.totalCents ?? 0) + (revolving?.totals.totalCents ?? 0),
                color: sectionColors.cards,
              },
            ]}
          />
        </CardContent>
      </Card>
    </>
  )
}

function Progress({
  label,
  value,
  max,
  color,
}: {
  label: string
  value: number
  max: number
  color: string
}) {
  return (
    <div className="grid grid-cols-[8rem_1fr] items-center gap-x-3 gap-y-1 text-sm max-sm:grid-cols-1">
      <span>{label}</span>
      <div className="flex items-center gap-3">
        <ProgressBar label={label} value={value} max={max} color={color} />
        <span className="shrink-0 text-xs whitespace-nowrap text-muted-foreground tabular-nums">
          {appTexts.month.valueOf(formatCents(value), formatCents(max))}
        </span>
      </div>
    </div>
  )
}
