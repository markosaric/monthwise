import { AlarmClock, Hash, HandCoins, Plus } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { useBorrowerTotals, useLendings, type LendingFilter } from '@/api/lending'
import { appTexts } from '@/components/common/texts'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { StatCard, StatGrid } from '@/components/design/StatCard'
import { LendingCard } from '@/components/lending/LendingCard'
import { lendingTexts } from '@/components/lending/texts'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PageHeader } from './PageHeader'

const t = lendingTexts
const filters: LendingFilter[] = ['Open', 'Returned']

/**
 * The Lending screen (FR-1): lendings not yet returned, or the returned ones, with the total still
 * owed and the overdue part, a card per lending, and the totals per borrower (FR-6).
 */
export function LendingPage() {
  const [filter, setFilter] = useState<LendingFilter>('Open')
  const { data, isPending, isError } = useLendings(filter)
  const lendings = data?.lendings ?? []

  return (
    <div className="flex max-w-6xl flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <PageHeader title={t.title} flush />
        <div
          role="group"
          aria-label={appTexts.common.show}
          className="ml-auto inline-flex rounded-lg border bg-card p-0.5"
        >
          {filters.map((f) => (
            <button
              key={f}
              type="button"
              aria-pressed={filter === f}
              onClick={() => setFilter(f)}
              className={cn(
                'rounded-md px-3 py-1 text-sm',
                filter === f ? 'bg-foreground text-background' : 'hover:bg-muted',
              )}
            >
              {t.filters[f]}
            </button>
          ))}
        </div>
        <Button asChild size="sm">
          <Link to="/lending/new">
            <Plus className="size-4" aria-hidden />
            {t.add}
          </Link>
        </Button>
      </div>

      {data && filter === 'Open' && lendings.length > 0 && (
        <StatGrid label={appTexts.common.keyFigures}>
          <StatCard
            icon={HandCoins}
            color={sectionColors.lending}
            label={t.figures.outstanding}
            value={formatCents(data.outstandingCents)}
          />
          <StatCard
            icon={AlarmClock}
            color={data.overdueCents > 0 ? meaningColors.problem : sectionColors.lending}
            label={t.figures.overdue}
            value={formatCents(data.overdueCents)}
            problem={data.overdueCents > 0}
          />
          <StatCard
            icon={Hash}
            color={sectionColors.lending}
            label={t.figures.open}
            value={lendings.length}
          />
        </StatGrid>
      )}

      {isPending && <p className="text-sm text-muted-foreground">{appTexts.common.loading}</p>}
      {isError && <p className="text-sm text-destructive">{appTexts.loadErrors.lendings}</p>}
      {data && lendings.length === 0 && (
        <div className="flex flex-col items-start gap-3">
          <p className="text-sm text-muted-foreground">
            {filter === 'Open' ? t.empty : t.emptyReturned}
          </p>
          {filter === 'Open' && (
            <Button asChild size="sm">
              <Link to="/lending/new">{t.add}</Link>
            </Button>
          )}
        </div>
      )}
      {lendings.length > 0 && (
        <ul className="grid gap-3 md:grid-cols-2">
          {lendings.map((lending) => (
            <LendingCard key={lending.id} lending={lending} />
          ))}
        </ul>
      )}
      <BorrowerTotals />
    </div>
  )
}

/** Lent, returned and outstanding per borrower (FR-6). */
function BorrowerTotals() {
  const { data: borrowers = [] } = useBorrowerTotals()
  if (borrowers.length === 0) return null
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-semibold">{t.borrowers.title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-muted-foreground">
              <tr>
                <th className="py-1 pr-3 font-medium">{t.fields.borrower}</th>
                <th className="py-1 pr-3 text-right font-medium">{t.labels.lent}</th>
                <th className="py-1 pr-3 text-right font-medium">{t.labels.returned}</th>
                <th className="py-1 text-right font-medium">{t.labels.outstanding}</th>
              </tr>
            </thead>
            <tbody>
              {borrowers.map((b) => (
                <tr
                  key={b.payerId}
                  data-overdue={b.hasOverdue || undefined}
                  className={cn('border-t', b.hasOverdue && 'text-red-700')}
                >
                  <td className="py-1.5 pr-3">{b.name}</td>
                  <td className="py-1.5 pr-3 text-right tabular-nums">
                    {formatCents(b.lentCents)}
                  </td>
                  <td className="py-1.5 pr-3 text-right tabular-nums">
                    {formatCents(b.returnedCents)}
                  </td>
                  <td className="py-1.5 text-right font-semibold tabular-nums">
                    {formatCents(b.outstandingCents)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="text-xs text-muted-foreground">{t.borrowers.hint}</p>
      </CardContent>
    </Card>
  )
}
