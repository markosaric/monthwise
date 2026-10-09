import { Link } from 'react-router'
import type { AccountBalance } from '@/api/cashFlow'
import type { Card } from '@/api/cards'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { Badge } from '@/components/ui/badge'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { CardStatusBadge } from './CardStatusBadge'
import { formatMonth } from './format'
import { cardTexts } from './texts'

/**
 * One card in the Settings list or the Dashboard widget; links to its details. On the Dashboard
 * (`compact`) it shows only name, primary mark, status, masked numbers and the current and expected
 * end-of-month balance (card settings Appendix 1, input cash flow FR-11).
 */
export function CardSummary({
  card,
  balance,
  compact = false,
}: {
  card: Card
  balance?: AccountBalance
  compact?: boolean
}) {
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
      {compact ? (
        numbers && <div className="font-mono text-sm text-muted-foreground">{numbers}</div>
      ) : (
        <>
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
          {card.billing && (
            <div className="text-sm text-muted-foreground">
              {cardPurchaseTexts.payment.statement} {card.billing.statementDay}. ·{' '}
              {cardPurchaseTexts.payment.due} {card.billing.paymentDay}. ·{' '}
              {card.billing.payingAccount.name}
            </div>
          )}
          {card.needsBillingDetails && (
            <div className="text-sm text-destructive">{cardPurchaseTexts.needsBillingDetails}</div>
          )}
        </>
      )}
      {balance && <BalanceLine balance={balance} />}
    </Link>
  )
}

function BalanceLine({ balance }: { balance: AccountBalance }) {
  const t = cashFlowTexts.balance
  return (
    <div className="flex flex-col gap-0.5 pt-1">
      <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1 text-sm">
        <span>
          <span className="text-muted-foreground">{t.current} </span>
          <span
            className={cn(
              'font-medium tabular-nums',
              balance.currentBalanceCents < 0 && 'text-destructive',
            )}
          >
            {formatCents(balance.currentBalanceCents)}
          </span>
        </span>
        <span className="text-muted-foreground">
          {t.expected}{' '}
          <span className="tabular-nums opacity-80">
            {formatCents(balance.expectedBalanceCents)}
          </span>
        </span>
      </div>
      {!balance.hasStartingBalance && (
        <span className="text-xs text-muted-foreground">{t.noStarting}</span>
      )}
    </div>
  )
}
