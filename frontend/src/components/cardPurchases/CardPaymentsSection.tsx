import { useMonthCardPayments } from '@/api/cardPurchases'
import { PaymentTotals } from '@/components/loans/LoanPaymentsSection'
import { CreditCard } from 'lucide-react'
import { sectionColors } from '@/components/design/colors'
import { SectionCard } from '@/components/design/SectionCard'
import { formatCents } from '@/lib/money'
import { CardPaymentRow } from './CardPaymentRow'
import { cardPurchaseTexts } from './texts'
import { appTexts } from '@/components/common/texts'
import { expenseTexts } from '@/components/expenses/texts'

const t = cardPurchaseTexts

/**
 * The card payments of the Month screen (installments FR-8): one summed-up line per card, which
 * opens that card's payment for the month, with its Paid check box and the totals.
 */
export function CardPaymentsSection({ month }: { month: string }) {
  const { data, isPending, isError } = useMonthCardPayments(month)
  const paid = data?.payments.filter((p) => p.isPaid).length ?? 0
  const all = data?.payments.length ?? 0

  return (
    <SectionCard
      icon={CreditCard}
      color={sectionColors.cards}
      title={t.monthTitle}
      status={data && all > 0 ? expenseTexts.bills.paidOf(paid, all) : undefined}
      total={data ? formatCents(data.totals.totalCents) : undefined}
      defaultOpen={!data || all === 0 || paid < all}
    >
      <div className="flex flex-col gap-3">
        {isPending && <p className="text-sm text-muted-foreground">{appTexts.common.loading}</p>}
        {isError && <p className="text-sm text-destructive">{appTexts.loadErrors.cardPayments}</p>}
        {data && data.payments.length === 0 && (
          <p className="text-sm text-muted-foreground">{t.monthEmpty}</p>
        )}
        {data && data.payments.length > 0 && (
          <>
            <ul className="flex flex-col divide-y">
              {data.payments.map((payment) => (
                <CardPaymentRow key={`${payment.cardId}-${payment.month}`} payment={payment} />
              ))}
            </ul>
            <PaymentTotals totals={data.totals} />
          </>
        )}
      </div>
    </SectionCard>
  )
}
