import { cn } from '@/lib/utils'

export type ChipStatus = 'done' | 'expected' | 'overdue'

/** Paid / received (green), expected (grey) or overdue (red) (FR-15). */
export function StatusChip({ status, label }: { status: ChipStatus; label: string }) {
  return (
    <span
      className={cn(
        'inline-flex h-5 shrink-0 items-center rounded-full px-2 text-xs font-medium whitespace-nowrap',
        status === 'done' && 'bg-green-100 text-green-800',
        status === 'expected' && 'bg-muted text-muted-foreground',
        status === 'overdue' && 'bg-red-100 text-red-700',
      )}
    >
      {label}
    </span>
  )
}
