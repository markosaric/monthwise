import type { LucideIcon } from 'lucide-react'
import { Tooltip } from 'radix-ui'
import { useEffect, useId, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { cn } from '@/lib/utils'
import { IconTile } from './IconTile'
import { QUICK_ACTION_LABEL_DELAY_MS } from './QuickActionButton'
import { useLongPress } from './useLongPress'

/**
 * A key figure (design refresh FR-2 and Appendix 3): a coloured icon, an optional change pill, the
 * value in large bold type and an optional detail under it. The label is not shown on the card: it
 * is a tooltip on the icon (after about a second of pointing, on keyboard focus, on a long press)
 * and screen readers read it before the value. With `to` the whole card opens that screen.
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
  const [open, setOpen] = useState(false)
  const labelId = useId()
  const longPress = useLongPress(() => setOpen(true))
  // Focus that follows a click or tap shows no tooltip; focus reached with the keyboard does.
  const pointerFocus = useRef(false)
  useEffect(() => {
    const keyboard = () => (pointerFocus.current = false)
    document.addEventListener('keydown', keyboard)
    return () => document.removeEventListener('keydown', keyboard)
  }, [])

  const iconWithTooltip = (
    <Tooltip.Root open={open} onOpenChange={setOpen}>
      <Tooltip.Trigger asChild>
        <span
          {...longPress}
          // A card that opens nothing has no focusable element, so its icon takes the focus (FR-2);
          // on a linked card the link itself does. The tooltip opens to the right of the icon, where it
          // does not cover the number (NFR-1).
          {...(to
            ? {}
            : {
                tabIndex: 0,
                'aria-labelledby': labelId,
                className:
                  'rounded-lg focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
              })}
        >
          <IconTile icon={icon} color={color} />
        </span>
      </Tooltip.Trigger>
      <Tooltip.Portal>
        <Tooltip.Content
          side="right"
          sideOffset={6}
          className="z-50 rounded-md bg-foreground px-2 py-1 text-xs font-medium text-background"
        >
          {label}
        </Tooltip.Content>
      </Tooltip.Portal>
    </Tooltip.Root>
  )

  const body = (
    <>
      <span id={labelId} className="sr-only">
        {label}{' '}
      </span>
      <div className="mb-3 flex items-center justify-between gap-2">
        {iconWithTooltip}
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
      {detail && <div className="mt-0.5 text-sm text-muted-foreground">{detail}</div>}
    </>
  )
  const className =
    'block rounded-xl bg-card p-4 text-card-foreground shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8'
  return to ? (
    <Link
      to={to}
      onPointerDown={() => (pointerFocus.current = true)}
      onFocus={() => !pointerFocus.current && setOpen(true)}
      onBlur={() => setOpen(false)}
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

/** The row of up to four key figures (five with `five`) at the top of a screen. */
export function StatGrid({
  children,
  label,
  five = false,
}: {
  children: ReactNode
  label: string
  five?: boolean
}) {
  // The key figure labels appear after the same delay as the quick action labels (FR-2).
  return (
    <Tooltip.Provider delayDuration={QUICK_ACTION_LABEL_DELAY_MS}>
      <section
        aria-label={label}
        className={cn('grid grid-cols-2 gap-3', five ? 'lg:grid-cols-5' : 'lg:grid-cols-4')}
      >
        {children}
      </section>
    </Tooltip.Provider>
  )
}
