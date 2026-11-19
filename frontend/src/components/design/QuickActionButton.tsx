import type { LucideIcon } from 'lucide-react'
import { Tooltip } from 'radix-ui'
import { useState } from 'react'
import { Link } from 'react-router'
import { tint } from './colors'
import { useLongPress } from './useLongPress'
import { appTexts } from '@/components/common/texts'

/** How long the pointer must rest on a quick action before its label appears (FR-14). */
export const QUICK_ACTION_LABEL_DELAY_MS = 1000

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
  const longPress = useLongPress(() => setOpen(true))

  return (
    <Tooltip.Root open={open} onOpenChange={setOpen}>
      <Tooltip.Trigger asChild>
        <Link
          to={to}
          aria-label={label}
          {...longPress}
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
      <nav aria-label={appTexts.common.quickActions} className="flex gap-3 overflow-x-auto pb-1">
        {children}
      </nav>
    </Tooltip.Provider>
  )
}
