import { CalendarClock, CreditCard, Hourglass, Wallet } from 'lucide-react'
import { Link } from 'react-router'
import {
  useInstallmentCards,
  useMonthCardPayments,
  type InstallmentCard,
} from '@/api/cardPurchases'
import { useDashboardCards } from '@/api/cards'
import { AccountsSection } from '@/components/cards/AccountsSection'
import { CardStatusBadge } from '@/components/cards/CardStatusBadge'
import { formatDate } from '@/components/cards/format'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { meaningColors, sectionColors, tint } from '@/components/design/colors'
import { PairedBarChart } from '@/components/design/PairedBarChart'
import { StatCard, StatGrid } from '@/components/design/StatCard'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { currentMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { PageHeader } from './PageHeader'

const t = cardPurchaseTexts

/**
 * The Cards screen: the accounts with their current and expected end-of-month balances (moved here
 * from the Dashboard, design refresh FR-12), then the cards that allow installments with their next
 * card payment and what is left to charge (installments FR-7).
 */
export function CardsPage() {
  const accounts = useDashboardCards()
  const { data: cards, isPending, isError } = useInstallmentCards()
  const { data: payments } = useMonthCardPayments(currentMonth())
  const canAdd = cards?.some((c) => c.canBeUsed && !c.needsBillingDetails) ?? false

  const listed = accounts.data?.cards ?? []
  const balances = (accounts.data?.balances ?? []).filter((b) =>
    listed.some((c) => c.id === b.accountId),
  )
  const current = balances.reduce((sum, b) => sum + b.currentBalanceCents, 0)
  const expected = balances.reduce((sum, b) => sum + b.expectedBalanceCents, 0)
  const installmentsLeft = (cards ?? []).reduce((sum, c) => sum + c.remainingCents, 0)

  return (
    <div className="flex max-w-6xl flex-col gap-4">
      <PageHeader title={t.screen} flush />
      <StatGrid label="Key figures">
        <StatCard
          icon={Wallet}
          color={sectionColors.cards}
          label="Current balance"
          value={formatCents(current)}
          problem={current < 0}
        />
        <StatCard
          icon={CalendarClock}
          color={sectionColors.cards}
          label="Expected at end of month"
          value={formatCents(expected)}
          muted
        />
        <StatCard
          icon={CreditCard}
          color={meaningColors.moneyOut}
          label="Card payments this month"
          value={formatCents(payments?.totals.totalCents ?? 0)}
        />
        <StatCard
          icon={Hourglass}
          color={meaningColors.expense}
          label="Installments left"
          value={formatCents(installmentsLeft)}
        />
      </StatGrid>

      <div className="grid gap-4 lg:grid-cols-2">
        <AccountsSection
          data={accounts.data}
          isPending={accounts.isPending}
          isError={accounts.isError}
        />
        {balances.length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle className="text-base font-semibold">
                Current and expected balance
              </CardTitle>
            </CardHeader>
            <CardContent>
              <PairedBarChart
                label="Current and expected end-of-month balance per account"
                rows={balances.map((b) => ({
                  label: listed.find((c) => c.id === b.accountId)?.name ?? '',
                  current: b.currentBalanceCents,
                  expected: b.expectedBalanceCents,
                }))}
                series={[
                  { key: 'current', label: 'Current', color: sectionColors.cards },
                  {
                    key: 'expected',
                    label: 'Expected at end of month',
                    color: tint(sectionColors.cards, 40),
                  },
                ]}
              />
            </CardContent>
          </Card>
        )}
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">Cards with installments</CardTitle>
          <CardAction className="flex flex-wrap gap-2">
            <Button asChild size="sm" variant="ghost">
              <Link to="/settings/cards">{t.manageCards}</Link>
            </Button>
            {canAdd && (
              <Button asChild size="sm">
                <Link to="/cards/purchases/new">{t.addPurchase}</Link>
              </Button>
            )}
          </CardAction>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
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
        </CardContent>
      </Card>
    </div>
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
