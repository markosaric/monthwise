import { Check, ChevronDown } from 'lucide-react'
import { useId, useState, type ChangeEvent, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * The one-tap Paid / Received check of a row (FR-15): a round check that turns green. It stays a
 * real checkbox, so it works with the keyboard and screen readers.
 */
export function CheckButton({
  checked,
  disabled,
  onChange,
  label,
}: {
  checked: boolean
  disabled?: boolean
  onChange: (event: ChangeEvent<HTMLInputElement>) => void
  label: string
}) {
  return (
    <span className="relative grid size-6 shrink-0 place-items-center">
      <input
        type="checkbox"
        checked={checked}
        disabled={disabled}
        onChange={onChange}
        aria-label={label}
        className="peer size-6 cursor-pointer appearance-none rounded-full border-2 border-muted-foreground/40 bg-card checked:border-money-in checked:bg-money-in hover:border-money-in focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50"
      />
      <Check
        aria-hidden
        className="pointer-events-none absolute size-3.5 text-white opacity-0 peer-checked:opacity-100"
      />
    </span>
  )
}

/**
 * A short list row (FR-15): check, name with its date, status chip and amount on one line; the
 * other details (category, account, notes, shares, actions) only when the row is expanded.
 */
export function ListRow({
  check,
  title,
  date,
  chips,
  amount,
  details,
  dimmed = false,
  label,
  ...data
}: {
  check?: ReactNode
  title: ReactNode
  date?: ReactNode
  chips?: ReactNode
  amount: ReactNode
  details?: ReactNode
  dimmed?: boolean
  /** Names the row in the Show / Hide details button. */
  label: string
  [dataAttribute: `data-${string}`]: string | boolean | undefined
}) {
  const [open, setOpen] = useState(false)
  const detailsId = useId()
  return (
    <li className="py-2.5" {...data}>
      <div className="flex items-center gap-3">
        {check}
        <div className="flex min-w-0 flex-1 flex-wrap items-center gap-x-2 gap-y-1">
          <span className={cn('font-medium', dimmed && 'text-muted-foreground')}>{title}</span>
          {date && <span className="text-xs text-muted-foreground">{date}</span>}
          {chips}
        </div>
        <span
          className={cn('text-right font-semibold tabular-nums', dimmed && 'text-muted-foreground')}
        >
          {amount}
        </span>
        {details && (
          <button
            type="button"
            onClick={() => setOpen((o) => !o)}
            aria-expanded={open}
            aria-controls={detailsId}
            aria-label={open ? `Hide details: ${label}` : `Show details: ${label}`}
            className="grid size-7 shrink-0 place-items-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground"
          >
            <ChevronDown className={cn('size-4 transition-transform', open && 'rotate-180')} />
          </button>
        )}
      </div>
      {details && (
        <div
          id={detailsId}
          hidden={!open}
          className={cn('pt-1.5 text-sm text-muted-foreground', check ? 'pl-9' : 'pl-0')}
        >
          {details}
        </div>
      )}
    </li>
  )
}
