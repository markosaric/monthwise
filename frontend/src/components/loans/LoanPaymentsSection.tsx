import { useMonthLoanPayments, type LoanPaymentTotals } from '@/api/loans'
import { Landmark } from 'lucide-react'
import { sectionColors } from '@/components/design/colors'
import { SectionCard } from '@/components/design/SectionCard'
import { OwnPartTotal } from '@/components/sharedCosts/SharedMarkers'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PaymentRow } from './PaymentRow'
import { loanTexts } from './texts'

const t = loanTexts.expenses

/** The Loan payments part of the Month screen: the month's loan payments, Paid check boxes and totals (FR-4, FR-5). */
export function LoanPaymentsSection({ month }: { month: string }) {
  const { data, isPending, isError } = useMonthLoanPayments(month)
  const paid = data?.payments.filter((p) => p.isPaid).length ?? 0
  const all = data?.payments.length ?? 0

  return (
    <SectionCard
      icon={Landmark}
      color={sectionColors.loans}
      title={t.title}
      status={data && all > 0 ? t.paidOf(paid, all) : undefined}
      total={data ? formatCents(data.totals.totalCents) : undefined}
      defaultOpen={!data || all === 0 || paid < all}
    >
      <div className="flex flex-col gap-3">
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
            <OwnPartTotal cents={data.ownPartCents} shown={data.payments.some((p) => p.share)} />
          </>
        )}
      </div>
    </SectionCard>
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
