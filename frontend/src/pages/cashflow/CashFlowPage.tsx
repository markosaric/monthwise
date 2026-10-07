import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useCashFlowChart, useInputCategories, usePayers, type ChartQuery } from '@/api/cashFlow'
import { useCards } from '@/api/cards'
import { CashFlowChart, CashFlowTable, ChartLegend } from '@/components/cashflow/CashFlowChart'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { addMonths, currentMonth, firstDayOf, lastDayOf } from '@/lib/dates'
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

/** The Cash flow dashboard: inputs over time, current month by default (FR-12). */
export function CashFlowPage() {
  const navigate = useNavigate()
  const [period, setPeriod] = useState<Period>('month')
  const [accountId, setAccountId] = useState('')
  const [payerId, setPayerId] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [asTable, setAsTable] = useState(false)
  const { data: cards = [] } = useCards('Default')
  const { data: payers = [] } = usePayers()
  const { data: categories = [] } = useInputCategories(true)
  const {
    data: chart,
    isPending,
    isError,
  } = useCashFlowChart({
    ...periodQuery(period),
    accountId,
    payerId,
    categoryId,
  })

  return (
    <>
      <PageHeader title={t.title} description={t.description} />
      <div className="flex max-w-4xl flex-col gap-4">
        <div className="grid gap-3 sm:grid-cols-4">
          <Filter
            id="period"
            label={t.period}
            value={period}
            onChange={(v) => setPeriod(v as Period)}
          >
            {(Object.keys(t.periods) as Period[]).map((p) => (
              <option key={p} value={p}>
                {t.periods[p]}
              </option>
            ))}
          </Filter>
          <Filter
            id="account"
            label={cashFlowTexts.fields.account}
            value={accountId}
            onChange={setAccountId}
          >
            <option value="">{t.allAccounts}</option>
            {cards.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Filter>
          <Filter
            id="payer"
            label={cashFlowTexts.fields.payer}
            value={payerId}
            onChange={setPayerId}
          >
            <option value="">{t.allPayers}</option>
            {payers.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Filter>
          <Filter
            id="category"
            label={cashFlowTexts.fields.category}
            value={categoryId}
            onChange={setCategoryId}
          >
            <option value="">{t.allCategories}</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Filter>
        </div>

        <Card>
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-wrap items-center gap-3">
              <ChartLegend />
              <span className="ml-auto flex flex-wrap gap-2">
                {period === 'month' && (
                  <Button asChild size="sm" variant="outline">
                    <Link to={`/cash-flow/${currentMonth()}`}>{t.openReport}</Link>
                  </Button>
                )}
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
          </CardContent>
        </Card>
      </div>
    </>
  )
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
