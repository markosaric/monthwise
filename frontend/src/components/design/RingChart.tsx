import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'
import { formatCents } from '@/lib/money'

export interface RingSlice {
  label: string
  cents: number
  color: string
}

/**
 * A ring chart with a legend that names every colour and shows its exact amount (FR-3), so the
 * numbers stay on the screen next to the picture.
 */
export function RingChart({ slices, label }: { slices: RingSlice[]; label: string }) {
  const shown = slices.filter((s) => s.cents > 0)
  const total = shown.reduce((sum, s) => sum + s.cents, 0)
  return (
    <div className="flex flex-wrap items-center gap-5">
      <div className="size-32 shrink-0" role="img" aria-label={label}>
        {total === 0 ? (
          <div className="size-full rounded-full border-[18px] border-muted" />
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie
                data={shown}
                dataKey="cents"
                nameKey="label"
                innerRadius="62%"
                outerRadius="100%"
                stroke="none"
                isAnimationActive={false}
              >
                {shown.map((s) => (
                  <Cell key={s.label} fill={s.color} />
                ))}
              </Pie>
              <Tooltip formatter={(value) => formatCents(Number(value))} />
            </PieChart>
          </ResponsiveContainer>
        )}
      </div>
      <ul className="grid min-w-44 flex-1 gap-1.5 text-sm">
        {slices.map((s) => (
          <li key={s.label} className="flex items-center gap-2">
            <span
              className="size-2.5 shrink-0 rounded-full"
              style={{ background: s.color }}
              aria-hidden
            />
            <span>{s.label}</span>
            <span className="ml-auto pl-3 font-medium tabular-nums">{formatCents(s.cents)}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}
