import { ArrowDownLeft, Repeat } from 'lucide-react'
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
import { addActions } from '@/components/design/actions'
import { AddButton } from '@/components/design/AddButton'
import { meaningColors } from '@/components/design/colors'
import { CheckButton, ListRow } from '@/components/design/ListRow'
import { SectionCard } from '@/components/design/SectionCard'
import { StatusChip } from '@/components/design/StatusChip'
import { Badge } from '@/components/ui/badge'
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
  const received = data?.inputs.filter((i) => i.isReceived).length ?? 0
  const all = data?.inputs.length ?? 0

  return (
    <SectionCard
      icon={ArrowDownLeft}
      color={meaningColors.moneyIn}
      title={t.income}
      status={data && all > 0 ? t.receivedOf(received, all) : undefined}
      total={data ? formatCents(data.totals.totalCents) : undefined}
      defaultOpen={!data || all === 0 || received < all}
      actions={
        <AddButton
          to={`/month/${month}/inputs/new`}
          label={t.addInput}
          icon={addActions.income.icon}
          color={addActions.income.color}
        />
      }
    >
      <div className="flex flex-col gap-3">
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
      </div>
    </SectionCard>
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
    <ListRow
      label={entry.payer.name}
      data-expected={!entry.isReceived || undefined}
      dimmed={!entry.isReceived}
      check={
        <CheckButton
          checked={entry.isReceived}
          disabled={setReceived.isPending}
          onChange={(event) => toggle(event.target.checked)}
          label={`${t.fields.received}: ${entry.payer.name}`}
        />
      }
      title={
        <Link to={entryEditPath(month, entry)} className="hover:underline">
          {entry.payer.name}
        </Link>
      }
      date={formatDate(entry.receivedDate ?? entry.expectedDate)}
      chips={
        <>
          <StatusChip
            status={entry.isReceived ? 'done' : 'expected'}
            label={entry.isReceived ? t.totals.received : t.totals.expected}
          />
          {entry.isRecurring && (
            <Badge variant="secondary" className="gap-1">
              <Repeat className="size-3" aria-hidden />
              {entry.frequency ? t.frequencies[entry.frequency] : t.fields.recurring}
            </Badge>
          )}
        </>
      }
      amount={formatCents(entry.amountCents)}
      details={
        <span className="flex flex-col gap-0.5">
          <span>
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
        </span>
      }
    />
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
