import type { LucideIcon } from 'lucide-react'
import { Link } from 'react-router'
import { cn } from '@/lib/utils'
import { tint } from './colors'

const className =
  'inline-flex h-8 items-center gap-1.5 rounded-lg border bg-card pr-2.5 pl-1 text-sm font-medium hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none'

function Icon({ icon: Glyph, color }: { icon: LucideIcon; color: string }) {
  return (
    <span
      aria-hidden
      className="grid size-6 place-items-center rounded-md"
      style={{ background: tint(color), color }}
    >
      <Glyph className="size-3.5" />
    </span>
  )
}

/**
 * An add button in a section header (FR-15): the icon and colour of its quick action (FR-14) with
 * its label. A link with `to`, otherwise a button.
 */
export function AddButton({
  label,
  icon,
  color,
  to,
  onClick,
  className: extra,
}: {
  label: string
  icon: LucideIcon
  color: string
  to?: string
  onClick?: () => void
  className?: string
}) {
  return to ? (
    <Link to={to} className={cn(className, extra)}>
      <Icon icon={icon} color={color} />
      {label}
    </Link>
  ) : (
    <button type="button" onClick={onClick} className={cn(className, extra)}>
      <Icon icon={icon} color={color} />
      {label}
    </button>
  )
}
