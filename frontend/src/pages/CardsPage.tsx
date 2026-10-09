import { Link } from 'react-router'
import { useInstallmentCards, type InstallmentCard } from '@/api/cardPurchases'
import { CardStatusBadge } from '@/components/cards/CardStatusBadge'
import { formatDate } from '@/components/cards/format'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatCents } from '@/lib/money'
import { PageHeader } from './PageHeader'

const t = cardPurchaseTexts

/** The Cards screen (installments FR-7): cards that allow installments, their next card payment and what is left to charge. */
export function CardsPage() {
  const { data: cards, isPending, isError } = useInstallmentCards()
  const canAdd = cards?.some((c) => c.canBeUsed && !c.needsBillingDetails) ?? false

  return (
    <>
      <PageHeader title={t.screen} description={t.screenDescription} />
      <div className="flex max-w-3xl flex-col gap-4">
        <div className="flex flex-wrap items-center gap-2">
          <Button asChild size="sm" variant="ghost">
            <Link to="/settings/cards">{t.manageCards}</Link>
          </Button>
          {canAdd && (
            <Button asChild size="sm" className="ml-auto">
              <Link to="/cards/purchases/new">{t.addPurchase}</Link>
            </Button>
          )}
        </div>
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Cards could not be loaded.</p>}
        {cards?.length === 0 && (
          <div className="flex flex-col items-start gap-3 rounded-lg border border-dashed p-4">
            <p className="text-sm text-muted-foreground">{t.noCards}</p>
            <Button asChild size="sm">
              <Link to="/settings/cards">{t.manageCards}</Link>
            </Button>
          </div>
        )}
        <ul className="flex flex-col gap-2">
          {cards?.map((card) => (
            <CardItem key={card.id} card={card} />
          ))}
        </ul>
      </div>
    </>
  )
}

function CardItem({ card }: { card: InstallmentCard }) {
  return (
    <li>
      <Link
        to={`/cards/${card.id}`}
        className="flex flex-col gap-1 rounded-lg border p-3 hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
      >
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{card.name}</span>
          {card.cardNumberMasked && (
            <span className="font-mono text-sm text-muted-foreground">{card.cardNumberMasked}</span>
          )}
          {card.needsAccount && <Badge variant="destructive">{t.labels.needsAccount}</Badge>}
          <span className="ml-auto">
            <CardStatusBadge status={card.status} />
          </span>
        </span>
        {card.needsBillingDetails ? (
          <span className="text-sm text-destructive">{t.needsBillingDetails}</span>
        ) : (
          <>
            <span className="text-sm text-muted-foreground">
              {t.payment.statement} {card.statementDay}. · {t.payment.due} {card.paymentDay}.
              {card.payingAccount && ` · ${card.payingAccount.name}`}
            </span>
            <span className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
              <span>
                <span className="text-muted-foreground">{t.card.nextPayment} </span>
                {card.nextPayment ? (
                  <span className="font-medium tabular-nums">
                    {formatCents(card.nextPayment.amountCents)} ·{' '}
                    {formatDate(card.nextPayment.paymentDate)}
                  </span>
                ) : (
                  <span className="text-muted-foreground">{t.card.noNextPayment}</span>
                )}
              </span>
              {card.remainingCents > 0 && (
                <span>
                  <span className="text-muted-foreground">{t.card.remaining} </span>
                  <span className="tabular-nums">{formatCents(card.remainingCents)}</span>
                  <span className="text-muted-foreground">
                    {' '}
                    ({t.card.purchasesLeft(card.purchasesWithInstallmentsLeft)})
                  </span>
                </span>
              )}
            </span>
          </>
        )}
      </Link>
    </li>
  )
}
