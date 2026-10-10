import { Repeat } from 'lucide-react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import {
  entryRef,
  entryEditPath,
  useMonthInputs,
  useSetReceived,
  type InputEntry,
  type InputTotals,
} from '@/api/cashFlow'
import { formatDate } from '@/components/cards/format'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ContributionGroup } from '@/components/sharedCosts/ContributionGroup'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { cashFlowTexts } from './texts'

const t = cashFlowTexts

/**
 * The Income part of the Month screen: list, totals and Received check boxes (FR-1, FR-3), with the
 * contributions of shared bills and loan payments grouped by contributor (shared bills FR-5).
 */
export function IncomeSection({ month }: { month: string }) {
  const { data, isPending, isError } = useMonthInputs(month)

  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle>{t.income}</CardTitle>
        <CardAction>
          <Button asChild size="sm">
            <Link to={`/month/${month}/inputs/new`}>{t.addInput}</Link>
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Inputs could not be loaded.</p>}
        {data && data.inputs.length === 0 && (
          <p className="text-sm text-muted-foreground">{t.emptyMonth}</p>
        )}
        {data && data.inputs.length > 0 && (
          <>
            <ul className="flex flex-col divide-y">
              {groupContributions(data.inputs).map((item) =>
                Array.isArray(item) ? (
                  <ContributionGroup key={`contributions-${item[0]!.payer.id}`} entries={item} />
                ) : (
                  <InputRow
                    key={item.id ?? `${item.recurringInputId}-${item.occurrenceDate}`}
                    month={month}
                    entry={item}
                  />
                ),
              )}
            </ul>
            <Totals totals={data.totals} />
          </>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * The list with the contributions of each contributor put together into one group, shown where that
 * contributor's first contribution is in the list.
 */
function groupContributions(entries: InputEntry[]): (InputEntry | InputEntry[])[] {
  const groups = new Map<string, InputEntry[]>()
  const items: (InputEntry | InputEntry[])[] = []
  for (const entry of entries) {
    if (!entry.contribution) {
      items.push(entry)
      continue
    }
    const group = groups.get(entry.payer.id)
    if (group) {
      group.push(entry)
    } else {
      const created = [entry]
      groups.set(entry.payer.id, created)
      items.push(created)
    }
  }
  return items
}

function InputRow({ month, entry }: { month: string; entry: InputEntry }) {
  const setReceived = useSetReceived()
  const checkboxId = `received-${entry.id ?? `${entry.recurringInputId}-${entry.occurrenceDate}`}`
  // A calculated occurrence can be ticked but not unticked: unticking a stored one makes it expected again.
  const lateOrEarly = entry.receivedDate !== null && entry.receivedDate !== entry.expectedDate

  async function toggle(received: boolean) {
    try {
      await setReceived.mutateAsync({ ref: entryRef(entry), received })
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Saving failed.')
    }
  }

  return (
    <li
      className={cn('flex items-start gap-3 py-3', !entry.isReceived && 'text-muted-foreground')}
      data-expected={!entry.isReceived || undefined}
    >
      <input
        id={checkboxId}
        type="checkbox"
        className="mt-1 size-4 accent-primary"
        checked={entry.isReceived}
        disabled={setReceived.isPending}
        onChange={(event) => toggle(event.target.checked)}
        aria-label={`${t.fields.received}: ${entry.payer.name}`}
      />
      <Link
        to={entryEditPath(month, entry)}
        className="flex min-w-0 flex-1 flex-col gap-0.5 hover:underline"
      >
        <span className="flex flex-wrap items-center gap-2">
          <span className={cn('font-medium', entry.isReceived && 'text-foreground')}>
            {entry.payer.name}
          </span>
          {entry.isRecurring && (
            <Badge variant="secondary" className="gap-1">
              <Repeat className="size-3" aria-hidden />
              {entry.frequency ? t.frequencies[entry.frequency] : t.fields.recurring}
            </Badge>
          )}
        </span>
        <span className="text-sm">
          {entry.category.name} · {entry.account.name}
          {entry.description && ` · ${entry.description}`}
        </span>
        <span className="text-xs">
          {entry.receivedDate ? (
            <>
              {t.totals.received} {formatDate(entry.receivedDate)}
              {lateOrEarly && ` (${t.receivedLate} ${formatDate(entry.expectedDate)})`}
            </>
          ) : (
            <>
              {t.totals.expected} {formatDate(entry.expectedDate)}
            </>
          )}
          {` · ${t.fields.issueDate.split(' (')[0]} ${formatDate(entry.issueDate)}`}
        </span>
      </Link>
      <span className={cn('font-medium tabular-nums', entry.isReceived && 'text-foreground')}>
        {formatCents(entry.amountCents)}
      </span>
    </li>
  )
}

function Totals({ totals }: { totals: InputTotals }) {
  return (
    <dl className="grid grid-cols-3 gap-2 border-t pt-3 text-sm">
      <Total label={t.totals.expected} cents={totals.expectedCents} muted />
      <Total label={t.totals.received} cents={totals.receivedCents} />
      <Total label={t.totals.total} cents={totals.totalCents} />
    </dl>
  )
}

function Total({ label, cents, muted = false }: { label: string; cents: number; muted?: boolean }) {
  return (
    <div className="flex flex-col">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className={cn('font-medium tabular-nums', muted && 'text-muted-foreground')}>
        {formatCents(cents)}
      </dd>
    </div>
  )
}
