import {
  ArrowDownLeft,
  ArrowUpRight,
  ChevronRight,
  Divide,
  Equal,
  SlidersHorizontal,
  X,
} from 'lucide-react'
import { Popover } from 'radix-ui'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useInstallmentCards } from '@/api/cardPurchases'
import {
  useCashFlowChart,
  useInputCategories,
  usePayers,
  type CashFlowChart as CashFlowChartData,
  type ChartQuery,
} from '@/api/cashFlow'
import { useCards } from '@/api/cards'
import { useExpenseCategories } from '@/api/expenses'
import { useLoanCategories } from '@/api/loans'
import { CashFlowChart, CashFlowTable, ChartLegend } from '@/components/cashflow/CashFlowChart'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { StatCard, StatGrid } from '@/components/design/StatCard'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import {
  addMonths,
  currentMonth,
  firstDayOf,
  formatYearMonth,
  lastDayOf,
  monthOf,
} from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PageHeader } from '@/pages/PageHeader'

const t = cashFlowTexts.cashFlow
type Period = keyof typeof t.periods

function periodQuery(period: Period): Pick<ChartQuery, 'from' | 'to' | 'granularity'> {
  const month = currentMonth()
  switch (period) {
    case 'month':
      return { from: firstDayOf(month), to: lastDayOf(month), granularity: 'Day' }
    case 'last12':
      return { from: firstDayOf(addMonths(month, -11)), to: lastDayOf(month), granularity: 'Month' }
    case 'year':
      return {
        from: `${month.slice(0, 4)}-01-01`,
        to: `${month.slice(0, 4)}-12-31`,
        granularity: 'Month',
      }
  }
}

/** The Cash flow screen: inputs and outputs over time, current month by default (FR-12, design refresh FR-16). */
export function CashFlowPage() {
  const navigate = useNavigate()
  const [period, setPeriod] = useState<Period>('month')
  const [accountId, setAccountId] = useState('')
  const [payerId, setPayerId] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [loanCategoryId, setLoanCategoryId] = useState('')
  const [cardId, setCardId] = useState('')
  const [expenseCategoryId, setExpenseCategoryId] = useState('')
  const [asTable, setAsTable] = useState(false)
  const { data: cards = [] } = useCards('Default')
  const { data: payers = [] } = usePayers()
  const { data: categories = [] } = useInputCategories(true)
  const { data: loanCategories = [] } = useLoanCategories(true)
  const { data: installmentCards = [] } = useInstallmentCards()
  const { data: expenseCategories = [] } = useExpenseCategories(true)
  const {
    data: chart,
    isPending,
    isError,
  } = useCashFlowChart({
    ...periodQuery(period),
    accountId,
    payerId,
    categoryId,
    loanCategoryId,
    cardId,
    expenseCategoryId,
  })

  const filters: FilterDef[] = [
    {
      id: 'account',
      label: cashFlowTexts.fields.account,
      value: accountId,
      onChange: setAccountId,
      all: t.allAccounts,
      options: cards,
    },
    {
      id: 'payer',
      label: cashFlowTexts.fields.payer,
      value: payerId,
      onChange: setPayerId,
      all: t.allPayers,
      options: payers,
    },
    {
      id: 'category',
      label: cashFlowTexts.fields.category,
      value: categoryId,
      onChange: setCategoryId,
      all: t.allCategories,
      options: categories,
    },
    {
      id: 'loan-category',
      label: t.loanCategory,
      value: loanCategoryId,
      onChange: setLoanCategoryId,
      all: t.allLoanCategories,
      options: loanCategories,
    },
    {
      id: 'card',
      label: t.card,
      value: cardId,
      onChange: setCardId,
      all: t.allCardPayments,
      options: installmentCards,
    },
    {
      id: 'expense-category',
      label: t.expenseCategory,
      value: expenseCategoryId,
      onChange: setExpenseCategoryId,
      all: t.allExpenseCategories,
      options: expenseCategories,
    },
  ]
  const active = filters.filter((f) => f.value !== '')
  const months = chart ? monthTotals(chart) : []
  const inputs = months.reduce((sum, m) => sum + m.inputs, 0)
  const outputs = months.reduce((sum, m) => sum + m.outputs, 0)

  return (
    <div className="flex max-w-6xl flex-col gap-4">
      <PageHeader title={t.title} flush />
      {chart && (
        <StatGrid label={t.keyFigures}>
          <StatCard
            icon={ArrowDownLeft}
            color={meaningColors.moneyIn}
            label={t.inputs}
            value={formatCents(inputs)}
          />
          <StatCard
            icon={ArrowUpRight}
            color={meaningColors.moneyOut}
            label={t.outputs}
            value={formatCents(outputs)}
          />
          <StatCard
            icon={Equal}
            color={sectionColors.cashflow}
            label={t.result}
            value={formatCents(inputs - outputs)}
            problem={inputs - outputs < 0}
          />
          <StatCard
            icon={Divide}
            color={sectionColors.cashflow}
            label={t.averagePerMonth}
            value={formatCents(
              months.length === 0 ? 0 : Math.round((inputs - outputs) / months.length),
            )}
          />
        </StatGrid>
      )}

      <div className="flex flex-wrap items-center gap-2">
        <div
          role="group"
          aria-label={t.period}
          className="inline-flex rounded-lg border bg-card p-0.5"
        >
          {(Object.keys(t.periods) as Period[]).map((p) => (
            <button
              key={p}
              type="button"
              aria-pressed={period === p}
              onClick={() => setPeriod(p)}
              className={cn(
                'rounded-md px-3 py-1 text-sm',
                period === p ? 'bg-foreground text-background' : 'hover:bg-muted',
              )}
            >
              {t.periods[p]}
            </button>
          ))}
        </div>
        <Popover.Root>
          <Popover.Trigger asChild>
            <Button variant="outline" size="sm">
              <SlidersHorizontal className="size-4" aria-hidden />
              {t.filters}
              {active.length > 0 && ` · ${active.length}`}
            </Button>
          </Popover.Trigger>
          <Popover.Portal>
            <Popover.Content
              align="start"
              sideOffset={6}
              className="z-50 grid w-[min(36rem,calc(100vw-2rem))] gap-3 rounded-xl border bg-popover p-4 text-popover-foreground shadow-lg sm:grid-cols-2"
            >
              {filters.map((f) => (
                <Filter key={f.id} id={f.id} label={f.label} value={f.value} onChange={f.onChange}>
                  <option value="">{f.all}</option>
                  {f.options.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.name}
                    </option>
                  ))}
                </Filter>
              ))}
            </Popover.Content>
          </Popover.Portal>
        </Popover.Root>
        {active.map((f) => (
          <span
            key={f.id}
            className="inline-flex items-center gap-1 rounded-full bg-teal-50 py-0.5 pr-1 pl-2.5 text-xs text-teal-800"
          >
            {f.label}: {f.options.find((o) => o.id === f.value)?.name}
            <button
              type="button"
              onClick={() => f.onChange('')}
              aria-label={t.removeFilter(f.label)}
              className="grid size-5 place-items-center rounded-full hover:bg-teal-100"
            >
              <X className="size-3" aria-hidden />
            </button>
          </span>
        ))}
      </div>

      <Card>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-3">
            <h2 className="text-base font-semibold">{t.chart}</h2>
            <span className="ml-auto flex flex-wrap gap-2">
              <Button size="sm" variant="ghost" onClick={() => setAsTable(!asTable)}>
                {asTable ? t.showChart : t.showTable}
              </Button>
            </span>
          </div>
          {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
          {isError && <p className="text-sm text-destructive">The chart could not be loaded.</p>}
          {chart &&
            (asTable ? (
              <CashFlowTable chart={chart} />
            ) : (
              <CashFlowChart
                chart={chart}
                onSelectMonth={(month) => navigate(`/cash-flow/${month}`)}
              />
            ))}
          <ChartLegend />
        </CardContent>
      </Card>

      {months.length > 0 && (
        <Card>
          <CardContent>
            <h2 className="mb-2 text-base font-semibold">{t.months}</h2>
            <ul className="flex flex-col divide-y">
              {[...months].reverse().map((m) => (
                <li key={m.month}>
                  <Link
                    to={`/cash-flow/${m.month}`}
                    className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2.5 text-sm hover:bg-muted/50"
                  >
                    <span className="font-medium">{formatYearMonth(m.month)}</span>
                    <span className="text-xs text-muted-foreground">
                      {t.monthLine(formatCents(m.inputs), formatCents(m.outputs))}
                    </span>
                    <span
                      className="ml-auto flex items-center gap-1 font-semibold tabular-nums"
                      style={{
                        color:
                          m.inputs - m.outputs < 0
                            ? meaningColors.problem
                            : meaningColors.moneyInText,
                      }}
                    >
                      {m.inputs - m.outputs > 0 && '+'}
                      {formatCents(m.inputs - m.outputs)}
                      <ChevronRight className="size-4 text-muted-foreground" aria-hidden />
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

interface FilterDef {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  all: string
  options: { id: string; name: string }[]
}

/** Inputs and outputs per month of the chart's period. */
function monthTotals(chart: CashFlowChartData) {
  const byMonth = new Map<string, { month: string; inputs: number; outputs: number }>()
  for (const b of chart.buckets) {
    const month = monthOf(b.start)
    const total = byMonth.get(month) ?? { month, inputs: 0, outputs: 0 }
    total.inputs += b.receivedCents + b.expectedCents
    total.outputs += b.outputPaidCents + b.outputExpectedCents
    byMonth.set(month, total)
  }
  return [...byMonth.values()]
}

function Filter({
  id,
  label,
  value,
  onChange,
  children,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={`filter-${id}`}>{label}</Label>
      <NativeSelect id={`filter-${id}`} value={value} onChange={(e) => onChange(e.target.value)}>
        {children}
      </NativeSelect>
    </div>
  )
}
