import { Link } from 'react-router'
import type { Card } from '@/api/cards'
import { Badge } from '@/components/ui/badge'
import { formatCents } from '@/lib/money'
import { CardStatusBadge } from './CardStatusBadge'
import { formatMonth } from './format'
import { cardTexts } from './texts'

/** One card in the Settings list or the Dashboard widget; links to its details. */
export function CardSummary({ card }: { card: Card }) {
  const numbers = [card.cardNumberMasked, card.ibanMasked].filter(Boolean).join(' · ')
  return (
    <Link
      to={`/settings/cards/${card.id}`}
      className="flex flex-col gap-1 rounded-lg border p-3 hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{card.name}</span>
        {card.isPrimary && <Badge variant="secondary">{cardTexts.primary}</Badge>}
        <span className="ml-auto">
          <CardStatusBadge status={card.status} />
        </span>
      </div>
      <div className="text-sm text-muted-foreground">
        {card.issuer.name} · {card.bank.name} · {cardTexts.types[card.type]}
      </div>
      <div className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
        <span className="font-mono">{numbers}</span>
        <span>
          {cardTexts.fields.validThrough} {formatMonth(card.validThrough)}
        </span>
        {card.debtLimitCents !== null && <span>Limit {formatCents(card.debtLimitCents)}</span>}
      </div>
    </Link>
  )
}
