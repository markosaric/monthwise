import { CalendarClock, Hash, Landmark, Plus, Repeat } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { useLoans, type Loan, type LoanListFilter } from '@/api/loans'
import { formatDate, formatMonth } from '@/components/cards/format'
import { meaningColors, sectionColors, tint } from '@/components/design/colors'
import { IconTile } from '@/components/design/IconTile'
import { ProgressBar } from '@/components/design/ProgressBar'
import { StatCard, StatGrid } from '@/components/design/StatCard'
import { loanTexts } from '@/components/loans/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PageHeader } from './PageHeader'

const t = loanTexts
const filters: LoanListFilter[] = ['Active', 'Closed']

/**
 * The Loans screen: loans that are not closed, or the closed ones (FR-1), with key figures, a card
 * per loan with its progress, and paid versus remaining per loan (design refresh FR-18).
 */
export function LoansPage() {
  const [filter, setFilter] = useState<LoanListFilter>('Active')
  const { data: loans, isPending, isError } = useLoans(filter)
  const active = filter === 'Active' && loans && loans.length > 0 ? loans : []
  const next = active
    .filter((l) => l.nextPaymentDate)
    .sort((a, b) => a.nextPaymentDate!.localeCompare(b.nextPaymentDate!))[0]

  return (
    <div className="flex max-w-6xl flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <PageHeader title={t.title} flush />
        <div
          role="group"
          aria-label="Show"
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
          <Link to="/loans/new">
            <Plus className="size-4" aria-hidden />
            {t.addLoan}
          </Link>
        </Button>
      </div>

      {active.length > 0 && (
        <StatGrid label="Key figures">
          <StatCard
            icon={Hash}
            color={sectionColors.loans}
            label={t.figures.activeLoans}
            value={active.length}
          />
          <StatCard
            icon={Repeat}
            color={sectionColors.loans}
            label={t.widget.totalMonthly}
            value={formatCents(active.reduce((sum, l) => sum + l.currentMonthlyPaymentCents, 0))}
          />
          <StatCard
            icon={Landmark}
            color={sectionColors.loans}
            label={t.figures.stillToRepay}
            value={formatCents(active.reduce((sum, l) => sum + l.remainingAmountCents, 0))}
          />
          <StatCard
            icon={CalendarClock}
            color={sectionColors.loans}
            label={t.labels.nextPayment}
            detail={next?.nextPaymentDate ? formatDate(next.nextPaymentDate) : undefined}
            value={next ? formatCents(next.currentMonthlyPaymentCents) : '–'}
          />
        </StatGrid>
      )}

      {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
      {isError && <p className="text-sm text-destructive">Loans could not be loaded.</p>}
      {loans && loans.length === 0 && (
        <div className="flex flex-col items-start gap-3">
          <p className="text-sm text-muted-foreground">
            {filter === 'Active' ? t.empty : t.emptyClosed}
          </p>
          {filter === 'Active' && (
            <Button asChild size="sm">
              <Link to="/loans/new">{t.addLoan}</Link>
            </Button>
          )}
        </div>
      )}
      {loans && loans.length > 0 && (
        <ul className="grid gap-3 md:grid-cols-2">
          {loans.map((loan) => (
            <LoanCard key={loan.id} loan={loan} />
          ))}
        </ul>
      )}
      {active.length > 0 && <PaidAndRemaining loans={active} />}
    </div>
  )
}

/** One loan: name, monthly payment, next payment and a progress bar of payments made (FR-18). */
function LoanCard({ loan }: { loan: Loan }) {
  const highlighted = loan.hasOverduePayment || loan.needsAccount
  const color = highlighted ? meaningColors.problem : sectionColors.loans
  return (
    <li data-highlighted={highlighted || undefined}>
      <Link
        to={`/loans/${loan.id}`}
        className={cn(
          'flex h-full flex-col gap-3 rounded-xl bg-card p-4 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8 hover:ring-foreground/20 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
          highlighted && 'bg-red-50/40 ring-red-200',
        )}
      >
        <span className="flex items-start gap-3">
          <IconTile icon={Landmark} color={color} solid />
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="flex flex-wrap items-center gap-2">
              <span className="font-semibold">{loan.name}</span>
              {loan.status !== 'Active' && (
                <Badge variant="secondary">{t.statuses[loan.status]}</Badge>
              )}
              {loan.hasOverduePayment && <Badge variant="destructive">{t.labels.overdue}</Badge>}
              {loan.needsAccount && <Badge variant="destructive">{t.labels.needsAccount}</Badge>}
            </span>
            <span className="text-sm text-muted-foreground">
              {loan.subcategory.name}
              {loan.nextPaymentDate &&
                ` · ${t.labels.nextPayment.toLowerCase()} ${formatDate(loan.nextPaymentDate)}`}
              {loan.closedOn && ` · ${t.details.closedOn} ${formatDate(loan.closedOn)}`}
            </span>
          </span>
          <span className="text-right">
            <span className="block font-heading text-lg font-bold tabular-nums">
              {formatCents(loan.currentMonthlyPaymentCents)}
            </span>
            <span className="text-xs text-muted-foreground">{t.figures.perMonth}</span>
          </span>
        </span>
        <ProgressBar
          label={`${loan.name}: ${t.figures.paidOf(loan.paymentsPaid, loan.paymentCount)}`}
          value={loan.paymentsPaid}
          max={loan.paymentCount}
          color={color}
        />
        <span className="flex flex-wrap justify-between gap-2 text-xs text-muted-foreground">
          <span>{t.figures.paidOf(loan.paymentsPaid, loan.paymentCount)}</span>
          <span>
            {formatCents(loan.remainingAmountCents)} {t.figures.toRepay}
            {` · ${t.labels.endMonth} ${formatMonth(loan.endMonth)}`}
          </span>
        </span>
      </Link>
    </li>
  )
}

/** Paid and remaining amount per loan as horizontal bars, with the exact amounts (FR-3, FR-18). */
function PaidAndRemaining({ loans }: { loans: Loan[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-semibold">{t.figures.paidAndRemaining}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <ul className="flex flex-col gap-2.5">
          {loans.map((loan) => {
            const total = loan.paidAmountCents + loan.remainingAmountCents
            const paidPercent = total === 0 ? 0 : (loan.paidAmountCents / total) * 100
            return (
              <li
                key={loan.id}
                className="grid items-center gap-x-3 gap-y-1 text-sm sm:grid-cols-[10rem_1fr_auto]"
              >
                <span className="truncate">{loan.name}</span>
                <span className="flex h-3.5 overflow-hidden rounded-md bg-muted" aria-hidden>
                  <span style={{ width: `${paidPercent}%`, background: sectionColors.loans }} />
                  <span className="flex-1" style={{ background: tint(sectionColors.loans, 25) }} />
                </span>
                <span className="text-xs whitespace-nowrap text-muted-foreground tabular-nums">
                  {formatCents(loan.paidAmountCents)} {t.figures.paid} ·{' '}
                  {formatCents(loan.remainingAmountCents)} {t.figures.remaining}
                </span>
              </li>
            )
          })}
        </ul>
        <ul className="flex gap-4 text-sm text-muted-foreground" aria-label="Legend">
          <li className="flex items-center gap-2">
            <span
              className="size-3 rounded-sm"
              style={{ background: sectionColors.loans }}
              aria-hidden
            />
            {t.figures.paidLegend}
          </li>
          <li className="flex items-center gap-2">
            <span
              className="size-3 rounded-sm"
              style={{ background: tint(sectionColors.loans, 25) }}
              aria-hidden
            />
            {t.figures.remainingLegend}
          </li>
        </ul>
      </CardContent>
    </Card>
  )
}
