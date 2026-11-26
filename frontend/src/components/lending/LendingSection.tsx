import { HandCoins } from 'lucide-react'
import { Link } from 'react-router'
import { useMonthLending, type MonthLendingEntry } from '@/api/lending'
import { formatDate } from '@/components/cards/format'
import { appTexts } from '@/components/common/texts'
import { AddButton } from '@/components/design/AddButton'
import { sectionColors } from '@/components/design/colors'
import { ListRow } from '@/components/design/ListRow'
import { SectionCard } from '@/components/design/SectionCard'
import { StatusChip } from '@/components/design/StatusChip'
import { formatCents } from '@/lib/money'
import { lendingTexts } from './texts'

const t = lendingTexts

/**
 * The Lending part of the Month screen (FR-9): money lent in the month, repayments received and the
 * expected returns due, overdue ones highlighted, with the month's totals.
 */
export function LendingSection({ month }: { month: string }) {
  const { data, isPending, isError } = useMonthLending(month)
  const count = data ? data.lent.length + data.returned.length + data.expectedBack.length : 0
  const groups: { key: string; title: string; entries: MonthLendingEntry[]; sign: string }[] = data
    ? [
        { key: 'lent', title: t.month.lent, entries: data.lent, sign: '−' },
        { key: 'returned', title: t.month.returned, entries: data.returned, sign: '+' },
        { key: 'expected', title: t.month.expectedBack, entries: data.expectedBack, sign: '+' },
      ]
    : []

  return (
    <SectionCard
      icon={HandCoins}
      color={sectionColors.lending}
      title={t.month.title}
      defaultOpen={count > 0}
      actions={
        <AddButton to="/lending/new" label={t.add} icon={HandCoins} color={sectionColors.lending} />
      }
    >
      <div className="flex flex-col gap-3">
        {isPending && <p className="text-sm text-muted-foreground">{appTexts.common.loading}</p>}
        {isError && <p className="text-sm text-destructive">{appTexts.loadErrors.lendings}</p>}
        {data && count === 0 && <p className="text-sm text-muted-foreground">{t.month.empty}</p>}
        {groups
          .filter((g) => g.entries.length > 0)
          .map((group) => (
            <div key={group.key}>
              <h3 className="text-sm font-medium">{group.title}</h3>
              <ul className="flex flex-col divide-y">
                {group.entries.map((entry) => (
                  <LendingEntryRow
                    key={`${group.key}-${entry.repaymentId ?? entry.lendingId}`}
                    entry={entry}
                    sign={group.sign}
                    expected={group.key === 'expected'}
                  />
                ))}
              </ul>
            </div>
          ))}
        {data && count > 0 && (
          <dl className="grid grid-cols-3 gap-2 border-t pt-3 text-sm">
            {(
              [
                [t.month.totals.lent, data.lentCents],
                [t.month.totals.returned, data.returnedCents],
                [t.month.totals.expectedBack, data.expectedBackCents],
              ] as const
            ).map(([label, cents]) => (
              <div key={label} className="flex flex-col">
                <dt className="text-muted-foreground">{label}</dt>
                <dd className="font-medium tabular-nums">{formatCents(cents)}</dd>
              </div>
            ))}
          </dl>
        )}
      </div>
    </SectionCard>
  )
}

function LendingEntryRow({
  entry,
  sign,
  expected,
}: {
  entry: MonthLendingEntry
  sign: string
  expected: boolean
}) {
  return (
    <ListRow
      label={entry.borrower.name}
      data-overdue={entry.isOverdue || undefined}
      title={
        <Link to="/lending" className="hover:underline">
          {entry.borrower.name}
        </Link>
      }
      date={formatDate(entry.date)}
      chips={
        entry.isOverdue ? (
          <StatusChip status="overdue" label={t.states.Overdue} />
        ) : expected ? (
          <StatusChip status="expected" label={t.states.Open} />
        ) : undefined
      }
      amount={
        <span className={entry.isOverdue ? 'text-problem' : undefined}>
          {sign}
          {formatCents(entry.amountCents)}
        </span>
      }
      details={<span>{entry.account.name}</span>}
    />
  )
}
