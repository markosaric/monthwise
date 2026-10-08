import { Link } from 'react-router'
import { useLoansDashboard } from '@/api/loans'
import { formatDate } from '@/components/cards/format'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { loanTexts } from './texts'

const t = loanTexts

/**
 * Dashboard widget: loans that are not closed or paid off, with the totals of their monthly
 * payments and remaining amounts. Overdue payments and loans needing an account are highlighted.
 * Not shown when no loans exist (FR-13).
 */
export function LoansWidget() {
  const { data } = useLoansDashboard()
  if (!data?.hasAnyLoans) return null

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>{t.widget.title}</CardTitle>
        <CardAction>
          <Button asChild variant="ghost" size="sm">
            <Link to="/loans">{t.actions.viewAll}</Link>
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {data.loans.length === 0 && <CardDescription>{t.widget.noActive}</CardDescription>}
        {data.loans.length > 0 && (
          <>
            <ul className="flex flex-col divide-y">
              {data.loans.map((loan) => {
                const highlighted = loan.hasOverduePayment || loan.needsAccount
                return (
                  <li key={loan.id} data-highlighted={highlighted || undefined}>
                    <Link
                      to={`/loans/${loan.id}`}
                      className={cn(
                        'flex flex-wrap items-baseline gap-x-3 gap-y-0.5 py-2 hover:underline',
                        highlighted && 'text-destructive',
                      )}
                    >
                      <span className="font-medium">{loan.name}</span>
                      <span className="text-sm text-muted-foreground">{loan.category.name}</span>
                      {loan.hasOverduePayment && (
                        <Badge variant="destructive">{t.labels.overdue}</Badge>
                      )}
                      {loan.needsAccount && (
                        <Badge variant="destructive">{t.labels.needsAccount}</Badge>
                      )}
                      <span className="ml-auto font-medium tabular-nums">
                        {formatCents(loan.currentMonthlyPaymentCents)}
                      </span>
                      <span className="basis-full text-xs text-muted-foreground">
                        {loan.nextPaymentDate &&
                          `${t.labels.nextPayment} ${formatDate(loan.nextPaymentDate)} · `}
                        {t.labels.paymentsLeft} {loan.paymentsLeft} {t.labels.of}{' '}
                        {loan.paymentCount} · {t.labels.remaining}{' '}
                        {formatCents(loan.remainingAmountCents)}
                      </span>
                    </Link>
                  </li>
                )
              })}
            </ul>
            <dl className="grid grid-cols-2 gap-2 border-t pt-3 text-sm">
              <div>
                <dt className="text-muted-foreground">{t.widget.totalMonthly}</dt>
                <dd className="font-medium tabular-nums">
                  {formatCents(data.totalMonthlyPaymentCents)}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">{t.widget.totalRemaining}</dt>
                <dd className="font-medium tabular-nums">
                  {formatCents(data.totalRemainingCents)}
                </dd>
              </div>
            </dl>
          </>
        )}
      </CardContent>
    </Card>
  )
}
