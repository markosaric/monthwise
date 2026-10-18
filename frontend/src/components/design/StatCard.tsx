import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { cn } from '@/lib/utils'
import { IconTile } from './IconTile'

/**
 * A key figure (FR-2): a coloured icon, an optional change pill, the value in large bold type and a
 * short label. With `to` the whole card opens that screen.
 */
export function StatCard({
  icon,
  color,
  label,
  value,
  detail,
  pill,
  to,
  problem = false,
  muted = false,
}: {
  icon: LucideIcon
  color: string
  label: string
  value: ReactNode
  detail?: ReactNode
  pill?: ReactNode
  to?: string
  problem?: boolean
  muted?: boolean
}) {
  const body = (
    <>
      <div className="mb-3 flex items-center justify-between gap-2">
        <IconTile icon={icon} color={color} />
        {pill}
      </div>
      <div
        className={cn(
          'font-heading text-2xl leading-tight font-bold tabular-nums',
          problem && 'text-problem',
          muted && 'text-muted-foreground',
        )}
      >
        {value}
      </div>
      <div className="mt-0.5 text-sm text-muted-foreground">
        {label}
        {detail && <span> · {detail}</span>}
      </div>
    </>
  )
  const className =
    'block rounded-xl bg-card p-4 text-card-foreground shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8'
  return to ? (
    <Link
      to={to}
      className={cn(
        className,
        'hover:ring-foreground/20 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
      )}
    >
      {body}
    </Link>
  ) : (
    <div className={className}>{body}</div>
  )
}

/** The row of up to four key figures at the top of a screen. */
export function StatGrid({ children, label }: { children: ReactNode; label: string }) {
  return (
    <section aria-label={label} className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      {children}
    </section>
  )
}
