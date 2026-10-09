import { useMonthCardPayments } from '@/api/cardPurchases'
import { PaymentTotals } from '@/components/loans/LoanPaymentsSection'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { CardPaymentRow } from './CardPaymentRow'
import { cardPurchaseTexts } from './texts'

const t = cardPurchaseTexts

/**
 * The card payments of the Month screen (installments FR-8): one summed-up line per card, which
 * opens that card's payment for the month, with its Paid check box and the totals.
 */
export function CardPaymentsSection({ month }: { month: string }) {
  const { data, isPending, isError } = useMonthCardPayments(month)

  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle>{t.monthTitle}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Card payments could not be loaded.</p>}
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
      </CardContent>
    </Card>
  )
}
