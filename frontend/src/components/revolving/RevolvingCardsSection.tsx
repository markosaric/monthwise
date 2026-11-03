import { Link } from 'react-router'
import type { RevolvingCard, RevolvingCards } from '@/api/revolving'
import { CardStatusBadge } from '@/components/cards/CardStatusBadge'
import { formatDate } from '@/components/cards/format'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { ProgressBar } from '@/components/design/ProgressBar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { revolvingTexts } from './texts'

const t = revolvingTexts

/**
 * The revolving cards of the Cards screen (revolving FR-10): each card with its debt, limit, remaining
 * limit (highlighted when over) and next bill. Hidden when there are no revolving cards.
 */
export function RevolvingCardsSection({ data }: { data: RevolvingCards | undefined }) {
  if (!data || data.cards.length === 0) return null
  const hasDebt = data.cards.some((c) => c.debtCents > 0)
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-semibold">{t.section}</CardTitle>
        {hasDebt && (
          <CardAction>
            <Button asChild size="sm">
              <Link to="/pay-revolving">{t.quickAction}</Link>
            </Button>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        <ul className="flex flex-col gap-2">
          {data.cards.map((card) => (
            <RevolvingCardItem key={card.id} card={card} />
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

function RevolvingCardItem({ card }: { card: RevolvingCard }) {
  return (
    <li>
      <Link
        to={`/cards/revolving/${card.id}`}
        className="flex flex-col gap-1.5 rounded-lg border p-3 hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
      >
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{card.name}</span>
          {card.cardNumberMasked && (
            <span className="font-mono text-sm text-muted-foreground">{card.cardNumberMasked}</span>
          )}
          {card.isOverLimit && <Badge variant="destructive">{t.overLimit}</Badge>}
          {card.needsAccount && <Badge variant="destructive">{t.bill.needsAccount}</Badge>}
          <span className="ml-auto">
            <CardStatusBadge status={card.status} />
          </span>
        </span>
        <span className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
          <span>
            <span className="text-muted-foreground">{t.card.debt} </span>
            <span className="font-medium tabular-nums">{formatCents(card.debtCents)}</span>
          </span>
          <span>
            <span className="text-muted-foreground">{t.card.limit} </span>
            <span className="tabular-nums">{formatCents(card.debtLimitCents)}</span>
          </span>
          <span>
            <span className="text-muted-foreground">{t.card.remaining} </span>
            <span className={cn('tabular-nums', card.isOverLimit && 'font-medium text-problem')}>
              {formatCents(card.remainingLimitCents)}
            </span>
          </span>
        </span>
        <ProgressBar
          value={card.debtCents}
          max={card.debtLimitCents}
          color={card.isOverLimit ? meaningColors.problem : sectionColors.cards}
          label={`${t.card.debt} / ${t.card.limit}`}
        />
        <span className="text-sm">
          <span className="text-muted-foreground">{t.card.nextBill} </span>
          {card.nextBill ? (
            <span className="tabular-nums">
              {t.card.minimum} {formatCents(card.nextBill.minimumCents)} · {t.card.due}{' '}
              {formatDate(card.nextBill.dueDate)}
              {card.nextBill.isOverdue && (
                <Badge variant="destructive" className="ml-2">
                  {t.bill.overdue}
                </Badge>
              )}
            </span>
          ) : (
            <span className="text-muted-foreground">{t.card.noNextBill}</span>
          )}
        </span>
      </Link>
    </li>
  )
}
