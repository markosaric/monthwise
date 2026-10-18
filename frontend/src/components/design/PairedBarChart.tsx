import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { formatCents } from '@/lib/money'

export interface BarSeries {
  key: string
  label: string
  color: string
}

const compactEur = new Intl.NumberFormat('de-DE', {
  style: 'currency',
  currency: 'EUR',
  notation: 'compact',
  maximumFractionDigits: 1,
})

/**
 * Bars side by side per row (e.g. inputs and outputs per month), with a legend naming each colour
 * and the exact amounts in the tooltip (FR-3). Values are cents.
 */
export function PairedBarChart<Row extends { label: string }>({
  rows,
  series,
  label,
  height = 'h-56',
}: {
  rows: Row[]
  series: BarSeries[]
  label: string
  height?: string
}) {
  return (
    <div>
      <div className={`${height} w-full`} role="img" aria-label={label}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} margin={{ top: 4, right: 4, bottom: 0, left: 0 }} barGap={3}>
            <CartesianGrid vertical={false} stroke="var(--border)" />
            <XAxis
              dataKey="label"
              tickLine={false}
              axisLine={{ stroke: 'var(--border)' }}
              fontSize={12}
            />
            <YAxis
              tickLine={false}
              axisLine={false}
              fontSize={12}
              width={56}
              tickFormatter={(v: number) => compactEur.format(v / 100)}
            />
            <Tooltip
              cursor={{ fill: 'var(--muted)' }}
              formatter={(value, name) => [formatCents(Number(value)), name]}
            />
            {series.map((s) => (
              <Bar
                key={s.key}
                dataKey={s.key}
                name={s.label}
                fill={s.color}
                radius={[5, 5, 0, 0]}
                isAnimationActive={false}
              />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>
      <ul className="mt-2 flex flex-wrap gap-4 text-sm text-muted-foreground" aria-label="Legend">
        {series.map((s) => (
          <li key={s.key} className="flex items-center gap-2">
            <span className="size-3 rounded-sm" style={{ background: s.color }} aria-hidden />
            {s.label}
          </li>
        ))}
      </ul>
    </div>
  )
}
