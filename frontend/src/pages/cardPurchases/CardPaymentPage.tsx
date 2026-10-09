import { Link, Navigate, useParams } from 'react-router'
import { useCardPayment, type CardPaymentDetails, type CardPaymentLine } from '@/api/cardPurchases'
import { formatDate } from '@/components/cards/format'
import { CardPaymentRow } from '@/components/cardPurchases/CardPaymentRow'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatYearMonth, isYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PageHeader } from '@/pages/PageHeader'

const t = cardPurchaseTexts

/**
 * The drill-down of a card payment: every purchase, installment, credit, and bill or expense paid with
 * the card in it (installments FR-8; bills and expenses FR-6).
 */
export function CardPaymentPage() {
  const { cardId, month } = useParams()
  const valid = isYearMonth(month)
  const { data, isPending, isError } = useCardPayment(cardId, valid ? month : undefined)
  if (!valid) return <Navigate to={`/cards/${cardId}`} replace />

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError)
    return <p className="text-sm text-destructive">This card payment could not be loaded.</p>
  return <CardPaymentView details={data} />
}

function CardPaymentView({ details }: { details: CardPaymentDetails }) {
  const { payment, lines } = details
  return (
    <>
      <PageHeader
        title={`${t.payment.details}: ${payment.cardName}, ${formatYearMonth(payment.month)}`}
      />
      <div className="flex max-w-3xl flex-col gap-6">
        <ul className="flex flex-col divide-y rounded-lg border px-3">
          <CardPaymentRow payment={payment} showCard={false} />
        </ul>
        <Card>
          <CardHeader>
            <CardTitle>{t.payment.lines}</CardTitle>
          </CardHeader>
          <CardContent>
            {lines.length === 0 && (
              <p className="text-sm text-muted-foreground">{t.payment.noLines}</p>
            )}
            <ul className="flex flex-col divide-y">
              {lines.map((line) => (
                <li
                  key={`${line.sourceId}-${line.kind}-${line.installmentNumber}`}
                  className="flex items-start gap-3 py-3"
                >
                  <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                    <span className="flex flex-wrap items-center gap-2">
                      <Link to={sourceLink(line)} className="font-medium hover:underline">
                        {line.description}
                      </Link>
                      {(line.kind === 'Credit' ||
                        line.kind === 'Bill' ||
                        line.kind === 'Expense') && (
                        <Badge variant="outline">{t.lineKinds[line.kind]}</Badge>
                      )}
                      {line.category && <Badge variant="secondary">{line.category.name}</Badge>}
                    </span>
                    <span className="text-sm text-muted-foreground">
                      {formatDate(line.date)}
                      {line.kind === 'FullPurchase' && ` · ${t.lineKinds.FullPurchase}`}
                      {(line.kind === 'Installment' || line.kind === 'Credit') &&
                        ` · ${t.lineKinds.Installment} ${line.installmentNumber} ${t.purchase.of} ${line.installmentTotal}`}
                    </span>
                  </div>
                  <span
                    className={cn(
                      'font-medium tabular-nums',
                      line.amountCents < 0 && 'text-emerald-700 dark:text-emerald-400',
                    )}
                  >
                    {formatCents(line.amountCents)}
                  </span>
                </li>
              ))}
            </ul>
            <div className="flex items-baseline justify-between border-t pt-3 text-sm">
              <span className="text-muted-foreground">{t.payment.total}</span>
              <span className="text-lg font-semibold tabular-nums">
                {formatCents(payment.amountCents)}
              </span>
            </div>
          </CardContent>
        </Card>
        <Link to={`/cards/${payment.cardId}`} className="text-sm text-muted-foreground underline">
          Back to {payment.cardName}
        </Link>
      </div>
    </>
  )
}

/** Where a line opens: its purchase, or the bill or expense paid with the card. */
function sourceLink(line: CardPaymentLine): string {
  switch (line.kind) {
    case 'Bill':
      return `/bills/${line.sourceId}/edit`
    case 'Expense':
      return `/expenses/${line.sourceId}/edit`
    default:
      return `/cards/purchases/${line.sourceId}`
  }
}
