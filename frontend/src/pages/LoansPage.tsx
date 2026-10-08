import { useState } from 'react'
import { Link } from 'react-router'
import { useLoans, type Loan, type LoanListFilter } from '@/api/loans'
import { formatDate, formatMonth } from '@/components/cards/format'
import { loanTexts } from '@/components/loans/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PageHeader } from './PageHeader'

const t = loanTexts
const filters: LoanListFilter[] = ['Active', 'Closed']

/** The Loans screen: loans that are not closed, or the closed ones (FR-1). */
export function LoansPage() {
  const [filter, setFilter] = useState<LoanListFilter>('Active')
  const { data: loans, isPending, isError } = useLoans(filter)

  return (
    <>
      <PageHeader title={t.title} description={t.description} />
      <div className="flex max-w-3xl flex-col gap-4">
        <div className="flex flex-wrap items-center gap-2">
          <div role="group" aria-label="Show" className="flex gap-1">
            {filters.map((f) => (
              <Button
                key={f}
                size="sm"
                variant={filter === f ? 'secondary' : 'ghost'}
                aria-pressed={filter === f}
                onClick={() => setFilter(f)}
              >
                {t.filters[f]}
              </Button>
            ))}
          </div>
          <Button asChild size="sm" className="ml-auto">
            <Link to="/loans/new">{t.addLoan}</Link>
          </Button>
        </div>
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
          <ul className="flex flex-col gap-2">
            {loans.map((loan) => (
              <LoanListItem key={loan.id} loan={loan} />
            ))}
          </ul>
        )}
      </div>
    </>
  )
}

function LoanListItem({ loan }: { loan: Loan }) {
  const highlighted = loan.hasOverduePayment || loan.needsAccount
  return (
    <li>
      <Link
        to={`/loans/${loan.id}`}
        className={cn(
          'flex flex-col gap-1 rounded-lg border p-3 hover:bg-muted',
          highlighted && 'border-destructive',
        )}
      >
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{loan.name}</span>
          {loan.status !== 'Active' && <Badge variant="secondary">{t.statuses[loan.status]}</Badge>}
          {loan.hasOverduePayment && <Badge variant="destructive">{t.labels.overdue}</Badge>}
          {loan.needsAccount && <Badge variant="destructive">{t.labels.needsAccount}</Badge>}
          <span className="ml-auto font-medium tabular-nums">
            {formatCents(loan.currentMonthlyPaymentCents)}
          </span>
        </span>
        <span className="text-sm text-muted-foreground">
          {loan.category.name} · {loan.subcategory.name} · {loan.lender.name} ·{' '}
          {t.methods[loan.paymentMethod]}
          {loan.account && ` · ${loan.account.name}`}
        </span>
        <span className="text-xs text-muted-foreground">
          {t.fields.paymentDay} {loan.currentPaymentDay}
          {loan.nextPaymentDate && ` · ${t.labels.nextPayment} ${formatDate(loan.nextPaymentDate)}`}
          {` · ${t.labels.paymentsLeft} ${loan.paymentsLeft} ${t.labels.of} ${loan.paymentCount}`}
          {` · ${t.labels.endMonth} ${formatMonth(loan.endMonth)}`}
          {` · ${t.labels.remaining} ${formatCents(loan.remainingAmountCents)}`}
          {loan.residualValueCents !== null &&
            ` · ${t.labels.residualValue} ${formatCents(loan.residualValueCents)}`}
          {loan.closedOn && ` · ${t.details.closedOn} ${formatDate(loan.closedOn)}`}
        </span>
      </Link>
    </li>
  )
}
