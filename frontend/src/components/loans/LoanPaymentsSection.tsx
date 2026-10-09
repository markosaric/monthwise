import { useMonthLoanPayments, type LoanPaymentTotals } from '@/api/loans'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PaymentRow } from './PaymentRow'
import { loanTexts } from './texts'

const t = loanTexts.expenses

/** The Loan payments part of the Month screen: the month's loan payments, Paid check boxes and totals (FR-4, FR-5). */
export function LoanPaymentsSection({ month }: { month: string }) {
  const { data, isPending, isError } = useMonthLoanPayments(month)

  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle>{t.title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Loan payments could not be loaded.</p>}
        {data && data.payments.length === 0 && (
          <p className="text-sm text-muted-foreground">{t.empty}</p>
        )}
        {data && data.payments.length > 0 && (
          <>
            <ul className="flex flex-col divide-y">
              {data.payments.map((payment) => (
                <PaymentRow
                  key={`${payment.loanId}-${payment.month}-${payment.kind}`}
                  payment={payment}
                />
              ))}
            </ul>
            <PaymentTotals totals={data.totals} />
          </>
        )}
      </CardContent>
    </Card>
  )
}

export function PaymentTotals({ totals }: { totals: LoanPaymentTotals }) {
  return (
    <dl className="grid grid-cols-3 gap-2 border-t pt-3 text-sm">
      <Total label={t.totals.expected} cents={totals.expectedCents} muted />
      <Total label={t.totals.paid} cents={totals.paidCents} />
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
