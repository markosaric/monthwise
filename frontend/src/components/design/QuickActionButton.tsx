import type { LucideIcon } from 'lucide-react'
import { Tooltip } from 'radix-ui'
import { useRef, useState } from 'react'
import { Link } from 'react-router'
import { tint } from './colors'

/** How long the pointer must rest on a quick action before its label appears (FR-14). */
export const QUICK_ACTION_LABEL_DELAY_MS = 1000
/** A press this long on a touch screen shows the label instead of opening the form. */
const LONG_PRESS_MS = 500

/**
 * A Dashboard quick action (FR-14): only a coloured icon; the label appears as a tooltip after the
 * pointer rests on it for about a second, at once on keyboard focus, and is always the accessible
 * name. Wrap a row of them in `QuickActionRow`.
 */
export function QuickActionButton({
  to,
  label,
  icon: Icon,
  color,
}: {
  to: string
  label: string
  icon: LucideIcon
  color: string
}) {
  const [open, setOpen] = useState(false)
  const timer = useRef<number | undefined>(undefined)
  const longPressed = useRef(false)
  const endPress = () => window.clearTimeout(timer.current)

  return (
    <Tooltip.Root open={open} onOpenChange={setOpen}>
      <Tooltip.Trigger asChild>
        <Link
          to={to}
          aria-label={label}
          onPointerDown={(e) => {
            if (e.pointerType !== 'touch') return
            longPressed.current = false
            timer.current = window.setTimeout(() => {
              longPressed.current = true
              setOpen(true)
            }, LONG_PRESS_MS)
          }}
          onPointerUp={endPress}
          onPointerCancel={endPress}
          onClick={(e) => {
            if (!longPressed.current) return
            e.preventDefault()
            longPressed.current = false
          }}
          onContextMenu={(e) => e.preventDefault()}
          className="grid size-11 shrink-0 place-items-center rounded-xl transition-transform hover:-translate-y-0.5 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none max-md:rounded-full max-md:size-12"
          style={{ background: tint(color), color }}
        >
          <Icon className="size-5" aria-hidden />
        </Link>
      </Tooltip.Trigger>
      <Tooltip.Portal>
        <Tooltip.Content
          side="bottom"
          sideOffset={6}
          className="z-50 rounded-md bg-foreground px-2 py-1 text-xs font-medium text-background"
        >
          {label}
        </Tooltip.Content>
      </Tooltip.Portal>
    </Tooltip.Root>
  )
}

/** The row of quick actions; one tooltip provider so labels show after the same delay. */
export function QuickActionRow({ children }: { children: React.ReactNode }) {
  return (
    <Tooltip.Provider delayDuration={QUICK_ACTION_LABEL_DELAY_MS}>
      <nav aria-label="Quick actions" className="flex gap-3 overflow-x-auto pb-1">
        {children}
      </nav>
    </Tooltip.Provider>
  )
}
