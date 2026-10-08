import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { CashFlowChart as ChartData, ChartGranularity } from '@/api/cashFlow'
import { formatDate } from '@/components/cards/format'
import { formatDayShort, formatYearMonth, monthOf } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { cashFlowTexts } from './texts'

const t = cashFlowTexts.cashFlow
const compactEur = new Intl.NumberFormat('de-DE', {
  style: 'currency',
  currency: 'EUR',
  notation: 'compact',
  maximumFractionDigits: 1,
})

interface Row {
  start: string
  label: string
  received: number
  expected: number
  outputPaid: number
  outputExpected: number
}

function toRows(chart: ChartData): Row[] {
  return chart.buckets.map((b) => ({
    start: b.start,
    label: bucketLabel(b.start, chart.granularity ?? 'Day'),
    received: b.receivedCents,
    expected: b.expectedCents,
    outputPaid: b.outputPaidCents,
    outputExpected: b.outputExpectedCents,
  }))
}

function bucketLabel(start: string, granularity: ChartGranularity): string {
  return granularity === 'Day' ? formatDayShort(start) : formatYearMonth(monthOf(start), 'short')
}

const series = [
  { key: 'received', stack: 'inputs', color: 'var(--chart-received)', label: t.received },
  { key: 'expected', stack: 'inputs', color: 'var(--chart-expected)', label: t.expected },
  { key: 'outputPaid', stack: 'outputs', color: 'var(--chart-output-paid)', label: t.outputPaid },
  {
    key: 'outputExpected',
    stack: 'outputs',
    color: 'var(--chart-output-expected)',
    label: t.outputExpected,
  },
] as const

/**
 * Legend: inputs in one hue and outputs (loan payments) in another; received and paid strong,
 * expected lighter.
 */
export function ChartLegend() {
  return (
    <ul className="flex flex-wrap gap-4 text-sm text-muted-foreground" aria-label="Legend">
      {series.map((s) => (
        <li key={s.key} className="flex items-center gap-2">
          <span className="size-3 rounded-sm" style={{ background: s.color }} aria-hidden />
          {s.label}
        </li>
      ))}
    </ul>
  )
}

/**
 * Received and expected inputs, and paid and expected outputs, per day or month as two stacks of
 * bars (FR-12, Appendix 1). Clicking a month bar
 * calls `onSelectMonth`.
 */
export function CashFlowChart({
  chart,
  onSelectMonth,
}: {
  chart: ChartData
  onSelectMonth?: (yearMonth: string) => void
}) {
  const rows = toRows(chart)
  const monthly = chart.granularity === 'Month'
  return (
    <div
      className="h-72 w-full"
      role="img"
      aria-label={`${t.title}: ${series.map((s) => s.label).join(', ')}`}
    >
      <ResponsiveContainer width="100%" height="100%">
        <BarChart
          data={rows}
          margin={{ top: 8, right: 8, bottom: 0, left: 0 }}
          barCategoryGap={monthly ? '25%' : '15%'}
          onClick={(state) => {
            const index = state?.activeTooltipIndex
            const row = typeof index === 'number' ? rows[index] : rows[Number(index)]
            if (monthly && row && onSelectMonth) onSelectMonth(monthOf(row.start))
          }}
          style={{ cursor: monthly && onSelectMonth ? 'pointer' : undefined }}
        >
          <CartesianGrid vertical={false} stroke="var(--border)" />
          <XAxis
            dataKey="label"
            tickLine={false}
            axisLine={{ stroke: 'var(--border)' }}
            tick={{ fill: 'var(--muted-foreground)', fontSize: 12 }}
            interval="preserveStartEnd"
            minTickGap={12}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            width={64}
            tick={{ fill: 'var(--muted-foreground)', fontSize: 12 }}
            tickFormatter={(cents: number) => compactEur.format(cents / 100)}
          />
          <Tooltip
            cursor={{ fill: 'var(--muted)', opacity: 0.6 }}
            content={({ active, payload }) => {
              const row = payload?.[0]?.payload as Row | undefined
              if (!active || !row) return null
              return (
                <div className="rounded-md border bg-popover px-3 py-2 text-sm text-popover-foreground shadow-sm">
                  <div className="font-medium">
                    {monthly ? formatYearMonth(monthOf(row.start)) : formatDate(row.start)}
                  </div>
                  {series.map((s) => (
                    <TooltipLine key={s.key} color={s.color} label={s.label} cents={row[s.key]} />
                  ))}
                </div>
              )
            }}
          />
          {series.map((s) => (
            <Bar
              key={s.key}
              dataKey={s.key}
              stackId={s.stack}
              fill={s.color}
              stroke="var(--card)"
              strokeWidth={2}
              radius={[4, 4, 0, 0]}
              isAnimationActive={false}
            />
          ))}
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

function TooltipLine({ color, label, cents }: { color: string; label: string; cents: number }) {
  return (
    <div className="flex items-center gap-2">
      <span className="size-2.5 rounded-sm" style={{ background: color }} aria-hidden />
      <span className="text-muted-foreground">{label}</span>
      <span className="ml-auto pl-3 tabular-nums">{formatCents(cents)}</span>
    </div>
  )
}

/** The same numbers as a table, for screen readers and exact reading. */
export function CashFlowTable({ chart }: { chart: ChartData }) {
  const rows = toRows(chart).filter((r) => series.some((s) => r[s.key] !== 0))
  if (rows.length === 0) return <p className="text-sm text-muted-foreground">{t.empty}</p>
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b text-left text-muted-foreground">
          <th className="py-2 font-normal">{chart.granularity === 'Month' ? 'Month' : 'Day'}</th>
          {series.map((s) => (
            <th key={s.key} className="py-2 text-right font-normal">
              {s.label}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.start} className="border-b last:border-0">
            <td className="py-2">
              {chart.granularity === 'Month'
                ? formatYearMonth(monthOf(row.start))
                : formatDate(row.start)}
            </td>
            {series.map((s) => (
              <td
                key={s.key}
                className={cn(
                  'py-2 text-right tabular-nums',
                  s.key.toLowerCase().includes('expected') && 'text-muted-foreground',
                )}
              >
                {formatCents(row[s.key])}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  )
}
