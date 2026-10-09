import { Link, useParams } from 'react-router'
import {
  useCardPayments,
  useCardPurchases,
  useInstallmentCard,
  type CardPurchase,
  type InstallmentCard,
} from '@/api/cardPurchases'
import { CardStatusBadge } from '@/components/cards/CardStatusBadge'
import { formatDate } from '@/components/cards/format'
import { CardPaymentRow } from '@/components/cardPurchases/CardPaymentRow'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCents } from '@/lib/money'
import { PageHeader } from '@/pages/PageHeader'

const t = cardPurchaseTexts

/** One card of the Cards screen: its billing details, card payments by month and purchases (installments FR-7). */
export function InstallmentCardPage() {
  const { cardId } = useParams()
  const { data: card, isPending, isError } = useInstallmentCard(cardId)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This card could not be loaded.</p>
  return <InstallmentCardView card={card} />
}

function InstallmentCardView({ card }: { card: InstallmentCard }) {
  const { data: payments } = useCardPayments(card.id)
  const { data: purchases } = useCardPurchases(card.id)
  const canAdd = card.canBeUsed && !card.needsBillingDetails

  return (
    <>
      <PageHeader title={card.name} />
      <div className="flex max-w-3xl flex-col gap-6">
        <div className="flex flex-wrap items-center gap-2">
          <CardStatusBadge status={card.status} />
          {card.needsAccount && <Badge variant="destructive">{t.labels.needsAccount}</Badge>}
        </div>
        {card.needsBillingDetails ? (
          <div className="flex flex-col items-start gap-3 rounded-lg border border-dashed p-4">
            <p className="text-sm">{t.needsBillingDetails}</p>
            <Button asChild size="sm">
              <Link to={`/settings/cards/${card.id}/edit`}>{t.setBillingDetails}</Link>
            </Button>
          </div>
        ) : (
          <dl className="grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-[auto_1fr]">
            <dt className="text-muted-foreground">{t.billing.statementDay}</dt>
            <dd>{card.statementDay}.</dd>
            <dt className="text-muted-foreground">{t.billing.paymentDay}</dt>
            <dd>{card.paymentDay}.</dd>
            <dt className="text-muted-foreground">{t.billing.payingAccount}</dt>
            <dd>
              {card.payingAccount?.name}
              {card.paysFromAccountDirectly && ` · ${t.billing.paysFromAccountDirectly}`}
            </dd>
            <dt className="text-muted-foreground">{t.billing.maxInstallments}</dt>
            <dd>{card.maxInstallments}</dd>
            <dt className="text-muted-foreground">{t.card.remaining}</dt>
            <dd className="tabular-nums">{formatCents(card.remainingCents)}</dd>
          </dl>
        )}
        {card.needsAccount && <p className="text-sm text-destructive">{t.needsAccount}</p>}
        <div className="flex flex-wrap gap-2">
          {canAdd && (
            <Button asChild>
              <Link to={`/cards/purchases/new?card=${card.id}`}>{t.addPurchase}</Link>
            </Button>
          )}
          <Button asChild variant="outline">
            <Link to={`/settings/cards/${card.id}`}>{t.manageCards}</Link>
          </Button>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>{t.card.payments}</CardTitle>
          </CardHeader>
          <CardContent>
            {payments?.length === 0 && (
              <p className="text-sm text-muted-foreground">{t.card.noPayments}</p>
            )}
            <ul className="flex flex-col divide-y">
              {payments?.map((payment) => (
                <CardPaymentRow key={payment.month} payment={payment} showCard={false} />
              ))}
            </ul>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t.card.purchases}</CardTitle>
          </CardHeader>
          <CardContent>
            {purchases?.length === 0 && (
              <p className="text-sm text-muted-foreground">{t.card.noPurchases}</p>
            )}
            <ul className="flex flex-col divide-y">
              {purchases?.map((purchase) => (
                <PurchaseItem key={purchase.id} purchase={purchase} />
              ))}
            </ul>
          </CardContent>
        </Card>
        <Link to="/cards" className="text-sm text-muted-foreground underline">
          Back to {t.screen.toLowerCase()}
        </Link>
      </div>
    </>
  )
}

function PurchaseItem({ purchase }: { purchase: CardPurchase }) {
  const reversed = purchase.reversedOn !== null
  return (
    <li className="flex items-start gap-3 py-3">
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="flex flex-wrap items-center gap-2">
          <Link
            to={`/cards/purchases/${purchase.id}`}
            className={
              reversed
                ? 'text-muted-foreground line-through hover:underline'
                : 'font-medium hover:underline'
            }
          >
            {purchase.description}
          </Link>
          {reversed && <Badge variant="outline">{t.purchase.reversed}</Badge>}
          {purchase.category && <Badge variant="secondary">{purchase.category.name}</Badge>}
        </span>
        <span className="text-sm text-muted-foreground">
          {formatDate(purchase.purchaseDate)} ·{' '}
          {purchase.inInstallments
            ? `${purchase.installmentsCharged} ${t.purchase.of} ${purchase.installmentCount} ${t.purchase.charged}`
            : t.purchase.fullPurchase}
          {purchase.remainingCents > 0 &&
            ` · ${t.purchase.remaining} ${formatCents(purchase.remainingCents)}`}
        </span>
      </div>
      <span className="font-medium tabular-nums">{formatCents(purchase.amountCents)}</span>
    </li>
  )
}
