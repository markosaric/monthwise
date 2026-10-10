import { ChevronDown, ChevronRight } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import type { InputEntry } from '@/api/cashFlow'
import { contributionRefOf, useChangeContribution } from '@/api/sharedCosts'
import { messageOf } from '@/components/loans/messageOf'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { ContributionRow } from './ContributionRow'
import { sharedCostTexts } from './texts'

const t = sharedCostTexts.contribution

/**
 * All contributions of one contributor in the month as one line in Income: the contributor, how many
 * contributions, the summed amount and one Received check box for all of them, with a Show button that
 * opens the single contributions, each with its own details and check box.
 */
export function ContributionGroup({ entries }: { entries: InputEntry[] }) {
  const change = useChangeContribution()
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const box = useRef<HTMLInputElement>(null)
  const payer = entries[0]!.payer
  const received = entries.filter((e) => e.isReceived)
  const all = received.length === entries.length
  const some = received.length > 0 && !all
  const total = entries.reduce((sum, e) => sum + e.amountCents, 0)

  useEffect(() => {
    if (box.current) box.current.indeterminate = some
  }, [some])

  /** Ticks every contribution that is not received yet, or unticks all of them. */
  async function toggle(receive: boolean) {
    const targets = entries.filter((e) => e.isReceived !== receive)
    const withoutAccount = receive ? targets.filter((e) => e.contribution!.needsAccount) : []
    setBusy(true)
    try {
      for (const entry of targets.filter((e) => !withoutAccount.includes(e))) {
        await change.mutateAsync({
          action: receive ? 'received' : 'expected',
          ref: contributionRefOf(entry.contribution!),
        })
      }
      if (withoutAccount.length > 0) {
        setOpen(true)
        toast.error(t.groupNeedsAccount(withoutAccount.length))
      }
    } catch (error) {
      toast.error(messageOf(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <li className="flex flex-col">
      <div className={cn('flex items-start gap-3 py-3', !all && 'text-muted-foreground')}>
        <input
          ref={box}
          type="checkbox"
          className="mt-1 size-4 accent-primary"
          checked={all}
          disabled={busy}
          onChange={(event) => toggle(event.target.checked)}
          aria-label={t.groupReceived(payer.name)}
        />
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <span className="flex flex-wrap items-center gap-2">
            <span className={cn('font-medium', all && 'text-foreground')}>{payer.name}</span>
            <Badge variant="secondary">{t.group(entries.length)}</Badge>
          </span>
          {some && (
            <span className="text-xs">{t.groupPartly(received.length, entries.length)}</span>
          )}
          <span className="pt-1">
            <Button
              size="xs"
              variant="ghost"
              aria-expanded={open}
              onClick={() => setOpen((o) => !o)}
            >
              {open ? (
                <ChevronDown className="size-3" aria-hidden />
              ) : (
                <ChevronRight className="size-3" aria-hidden />
              )}
              {open ? t.hideSingle : t.showSingle(entries.length)}
            </Button>
          </span>
        </div>
        <span className={cn('font-medium tabular-nums', all && 'text-foreground')}>
          {formatCents(total)}
        </span>
      </div>
      {open && (
        <ul className="flex flex-col divide-y border-t border-dashed">
          {entries.map((entry) => (
            <ContributionRow key={entry.id ?? contributionKey(entry)} entry={entry} nested />
          ))}
        </ul>
      )}
    </li>
  )
}

/** A calculated contribution has no id: it is identified by its bill or loan payment. */
function contributionKey(entry: InputEntry): string {
  const c = entry.contribution!
  return `contribution-${c.billId ?? `${c.loanId}-${c.month}-${c.paymentKind}`}`
}
