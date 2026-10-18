import { ChevronDown, type LucideIcon } from 'lucide-react'
import { useId, useState, type ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { IconTile } from './IconTile'

/**
 * A part of a screen in its own card with a coloured left edge and icon (FR-15). The header shows
 * the title, a short status, optional actions and the total; the content can be collapsed, and a
 * section whose items are all done starts collapsed.
 */
export function SectionCard({
  icon,
  color,
  title,
  status,
  total,
  actions,
  defaultOpen = true,
  children,
}: {
  icon: LucideIcon
  color: string
  title: string
  status?: ReactNode
  total?: ReactNode
  actions?: ReactNode
  defaultOpen?: boolean
  children: ReactNode
}) {
  // Follows `defaultOpen` (which can change once the data has loaded) until the user toggles it.
  const [toggled, setToggled] = useState<boolean | null>(null)
  const open = toggled ?? defaultOpen
  const contentId = useId()
  return (
    <section
      aria-label={title}
      className="rounded-xl border-l-4 bg-card p-4 text-card-foreground shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8"
      style={{ borderLeftColor: color }}
    >
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
        <IconTile icon={icon} color={color} size="sm" />
        <h2 className="text-lg font-semibold">{title}</h2>
        {status && <span className="text-sm text-muted-foreground">{status}</span>}
        {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
        <span className="ml-auto flex items-center gap-2">
          {total && <span className="font-semibold tabular-nums">{total}</span>}
          <button
            type="button"
            onClick={() => setToggled(!open)}
            aria-expanded={open}
            aria-controls={contentId}
            aria-label={open ? `Collapse ${title}` : `Expand ${title}`}
            className="grid size-7 place-items-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground"
          >
            <ChevronDown className={cn('size-4 transition-transform', !open && '-rotate-90')} />
          </button>
        </span>
      </div>
      <div id={contentId} hidden={!open} className="mt-3">
        {children}
      </div>
    </section>
  )
}
