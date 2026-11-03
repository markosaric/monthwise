import { CreditCard } from 'lucide-react'
import { Link } from 'react-router'
import { useMonthRevolving } from '@/api/revolving'
import { sectionColors } from '@/components/design/colors'
import { SectionCard } from '@/components/design/SectionCard'
import { PaymentTotals } from '@/components/loans/LoanPaymentsSection'
import { Button } from '@/components/ui/button'
import { formatCents } from '@/lib/money'
import { ExtraPaymentRow } from './ExtraPaymentRow'
import { RevolvingBillRow } from './RevolvingBillRow'
import { revolvingTexts } from './texts'

const t = revolvingTexts

/**
 * The Revolving cards part of the Month screen (revolving FR-11): the monthly bills due or paid in the
 * month with their Paid check boxes, the extra payments, and the totals. Hidden when there is nothing.
 */
export function RevolvingMonthSection({ month }: { month: string }) {
  const { data } = useMonthRevolving(month)
  if (!data || (data.bills.length === 0 && data.extraPayments.length === 0)) return null
  const paid = data.bills.filter((b) => b.isPaid).length
  const all = data.bills.length

  return (
    <SectionCard
      icon={CreditCard}
      color={sectionColors.cards}
      title={t.month.title}
      status={all > 0 ? `${paid}/${all} ${t.month.paid.toLowerCase()}` : undefined}
      total={formatCents(data.totals.totalCents)}
      defaultOpen={paid < all}
    >
      <div className="flex flex-col gap-3">
        <ul className="flex flex-col divide-y">
          {data.bills.map((bill) => (
            <RevolvingBillRow key={`${bill.cardId}-${bill.month}`} bill={bill} />
          ))}
          {data.extraPayments.map((payment) => (
            <ExtraPaymentRow key={payment.id} payment={payment} />
          ))}
        </ul>
        <PaymentTotals totals={data.totals} />
        <Button asChild size="sm" variant="outline" className="self-start">
          <Link to="/pay-revolving">{t.quickAction}</Link>
        </Button>
      </div>
    </SectionCard>
  )
}
