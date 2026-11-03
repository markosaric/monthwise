import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import {
  useChangeDebtEntry,
  useRevolvingCard,
  type DebtEntry,
  type RevolvingCardDetails,
  type RevolvingMovement,
} from '@/api/revolving'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { CardStatusBadge } from '@/components/cards/CardStatusBadge'
import { formatDate } from '@/components/cards/format'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { ListRow } from '@/components/design/ListRow'
import { ProgressBar } from '@/components/design/ProgressBar'
import { DebtEntryDialog } from '@/components/revolving/DebtEntryDialog'
import { ExtraPaymentRow } from '@/components/revolving/ExtraPaymentRow'
import { RevolvingBillRow } from '@/components/revolving/RevolvingBillRow'
import { revolvingTexts } from '@/components/revolving/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { addMonths, currentMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { formatBasisPoints } from '@/lib/percent'
import { PageHeader } from '@/pages/PageHeader'

const t = revolvingTexts

/**
 * A revolving card (revolving FR-10): debt, limit and remaining limit, the terms, the debt over time,
 * the bills by month with Pay, Untick and Correct, and the charges, extra payments and debt entries,
 * with the Extra payment and Correct debt actions.
 */
export function RevolvingCardPage() {
  const { cardId } = useParams()
  const [withEstimates, setWithEstimates] = useState(false)
  const { data, isPending, isError } = useRevolvingCard(
    cardId,
    withEstimates ? addMonths(currentMonth(), 12) : undefined,
  )

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">{t.card.notFound}</p>
  return (
    <RevolvingCardView
      details={data}
      withEstimates={withEstimates}
      onShowEstimates={() => setWithEstimates(true)}
    />
  )
}

type Dialog = { kind: 'correct' } | { kind: 'edit' | 'delete'; entry: DebtEntry } | null

function RevolvingCardView({
  details,
  withEstimates,
  onShowEstimates,
}: {
  details: RevolvingCardDetails
  withEstimates: boolean
  onShowEstimates: () => void
}) {
  const { card, bills, extraPayments, debtEntries, charges, movements } = details
  const [dialog, setDialog] = useState<Dialog>(null)
  const entryAction = useChangeDebtEntry(card.id)
  const readOnly = card.status === 'Archived'
  const close = (open: boolean) => !open && setDialog(null)
  const terms = card.terms

  return (
    <>
      <PageHeader title={card.name} description={card.cardNumberMasked ?? undefined} />
      <div className="flex max-w-3xl flex-col gap-6">
        <div className="flex flex-wrap items-center gap-2">
          <CardStatusBadge status={card.status} />
          {card.isOverLimit && <Badge variant="destructive">{t.overLimit}</Badge>}
          {card.needsAccount && <Badge variant="destructive">{t.bill.needsAccount}</Badge>}
        </div>

        <Card>
          <CardContent className="flex flex-col gap-2">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <span className="font-heading text-2xl font-bold tabular-nums">
                {formatCents(card.debtCents)}
              </span>
              <span className="text-sm text-muted-foreground">
                {t.card.limit} {formatCents(card.debtLimitCents)} · {t.card.remaining}{' '}
                <span className={card.isOverLimit ? 'font-medium text-problem' : undefined}>
                  {formatCents(card.remainingLimitCents)}
                </span>
              </span>
            </div>
            <ProgressBar
              label={`${t.card.debt} / ${t.card.limit}`}
              value={card.debtCents}
              max={card.debtLimitCents}
              color={card.isOverLimit ? meaningColors.problem : sectionColors.cards}
            />
            {card.needsAccount && <p className="text-sm text-destructive">{t.needsAccount}</p>}
            {terms && (
              <dl className="grid grid-cols-1 gap-x-6 gap-y-1 pt-2 text-sm sm:grid-cols-[auto_1fr]">
                <dt className="text-muted-foreground">{t.card.minimum}</dt>
                <dd>
                  {t.card.termsLine(
                    formatBasisPoints(terms.minPaymentBasisPoints),
                    formatCents(terms.minPaymentAmountCents),
                  )}
                </dd>
                <dt className="text-muted-foreground">{t.card.interest}</dt>
                <dd>
                  {formatBasisPoints(terms.interestBasisPoints)} {t.card.perYear}
                  {terms.minPaymentInterestBasisPoints != null &&
                    ` (${t.card.interestPart} ${formatBasisPoints(terms.minPaymentInterestBasisPoints)})`}
                </dd>
                <dt className="text-muted-foreground">{t.card.billAndDue}</dt>
                <dd>{t.card.days(terms.billDay, terms.dueDay)}</dd>
                <dt className="text-muted-foreground">{t.card.payingAccount}</dt>
                <dd>
                  {terms.payingAccount.name}
                  {terms.paysFromAccountDirectly && ` · ${t.card.connected}`}
                </dd>
              </dl>
            )}
          </CardContent>
        </Card>

        {!readOnly && (
          <div className="flex flex-wrap gap-2">
            {card.debtCents > 0 && (
              <Button asChild>
                <Link to={`/cards/revolving/${card.id}/pay`}>{t.payment.title}</Link>
              </Button>
            )}
            <Button variant="outline" onClick={() => setDialog({ kind: 'correct' })}>
              {t.entry.correctDebt}
            </Button>
            <Button asChild variant="ghost">
              <Link to={`/settings/cards/${card.id}`}>{t.card.settings}</Link>
            </Button>
          </div>
        )}

        {movements.length > 1 && <DebtChart movements={movements} />}

        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold">{t.card.bills}</CardTitle>
            {!withEstimates && (
              <CardAction>
                <Button size="sm" variant="ghost" onClick={onShowEstimates}>
                  {t.card.showMore}
                </Button>
              </CardAction>
            )}
          </CardHeader>
          <CardContent>
            {bills.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t.card.noBills}</p>
            ) : (
              <ul className="flex flex-col divide-y">
                {[...bills]
                  .sort((a, b) => a.month.localeCompare(b.month))
                  .map((bill) => (
                    <RevolvingBillRow
                      key={bill.month}
                      bill={bill}
                      showCard={false}
                      readOnly={readOnly}
                    />
                  ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold">{t.card.extraPayments}</CardTitle>
          </CardHeader>
          <CardContent>
            {extraPayments.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t.card.noExtraPayments}</p>
            ) : (
              <ul className="flex flex-col divide-y">
                {extraPayments.map((payment) => (
                  <ExtraPaymentRow
                    key={payment.id}
                    payment={payment}
                    showCard={false}
                    readOnly={readOnly}
                  />
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold">{t.card.charges}</CardTitle>
          </CardHeader>
          <CardContent>
            {charges.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t.card.noCharges}</p>
            ) : (
              <ul className="flex flex-col divide-y">
                {charges.map((charge) => (
                  <ListRow
                    key={`${charge.kind}-${charge.sourceId}`}
                    label={charge.description}
                    title={charge.description}
                    date={formatDate(charge.date)}
                    chips={
                      charge.category ? (
                        <span className="text-xs text-muted-foreground">
                          {charge.category.name}
                        </span>
                      ) : undefined
                    }
                    amount={formatCents(charge.amountCents)}
                  />
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold">{t.card.debtEntries}</CardTitle>
          </CardHeader>
          <CardContent>
            {debtEntries.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t.card.noDebtEntries}</p>
            ) : (
              <ul className="flex flex-col divide-y">
                {debtEntries.map((entry) => {
                  const transfer = entry.kind === 'TransferIn' || entry.kind === 'TransferOut'
                  return (
                    <ListRow
                      key={entry.id}
                      label={`${t.kinds[entry.kind]} ${entry.date}`}
                      title={t.kinds[entry.kind]}
                      date={formatDate(entry.date)}
                      amount={formatCents(entry.amountCents)}
                      details={
                        <span className="flex flex-col gap-0.5">
                          <span>
                            {t.entry.difference(formatCents(entry.differenceCents))}
                            {entry.linkedCard && ` · ${entry.linkedCard.name}`}
                            {entry.note && ` · ${entry.note}`}
                          </span>
                          {!readOnly && !transfer && (
                            <span className="flex flex-wrap gap-1 pt-1">
                              <Button
                                size="xs"
                                variant="ghost"
                                onClick={() => setDialog({ kind: 'edit', entry })}
                              >
                                {t.entry.editTitle}
                              </Button>
                              <Button
                                size="xs"
                                variant="ghost"
                                onClick={() => setDialog({ kind: 'delete', entry })}
                              >
                                {t.entry.delete}
                              </Button>
                            </span>
                          )}
                        </span>
                      }
                    />
                  )
                })}
              </ul>
            )}
          </CardContent>
        </Card>

        <Link to="/cards" className="text-sm text-muted-foreground underline">
          Back to cards
        </Link>
      </div>

      {dialog?.kind === 'correct' && <DebtEntryDialog cardId={card.id} onOpenChange={close} />}
      {dialog?.kind === 'edit' && (
        <DebtEntryDialog cardId={card.id} entry={dialog.entry} onOpenChange={close} />
      )}
      {dialog?.kind === 'delete' && (
        <CardActionDialog
          open
          onOpenChange={close}
          title={`${t.entry.delete}: ${t.kinds[dialog.entry.kind]}?`}
          description={formatDate(dialog.entry.date)}
          confirmLabel={t.entry.delete}
          destructive
          onConfirm={async () => {
            await entryAction.mutateAsync({ action: 'delete', entryId: dialog.entry.id })
            toast.success(t.entry.deleted)
          }}
        />
      )}
    </>
  )
}

const compactEur = new Intl.NumberFormat('de-DE', {
  style: 'currency',
  currency: 'EUR',
  notation: 'compact',
  maximumFractionDigits: 1,
})

/** The debt at the end of every day it changed (revolving FR-10). */
function DebtChart({ movements }: { movements: RevolvingMovement[] }) {
  const byDate = new Map<string, number>()
  for (const movement of movements) byDate.set(movement.date, movement.debtAfterCents)
  const rows = [...byDate].map(([date, debt]) => ({ date, debt }))
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-semibold">{t.card.debtOverTime}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-48 w-full" role="img" aria-label={t.card.debtOverTime}>
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={rows} margin={{ top: 4, right: 8, bottom: 0, left: 0 }}>
              <CartesianGrid vertical={false} stroke="var(--border)" />
              <XAxis
                dataKey="date"
                tickLine={false}
                axisLine={{ stroke: 'var(--border)' }}
                fontSize={12}
                tickFormatter={(value: string) => formatDate(value).slice(0, 5)}
              />
              <YAxis
                tickLine={false}
                axisLine={false}
                fontSize={12}
                width={56}
                tickFormatter={(v: number) => compactEur.format(v / 100)}
              />
              <Tooltip
                labelFormatter={(label) => formatDate(String(label))}
                formatter={(value) => [formatCents(Number(value)), t.card.debt]}
              />
              <Line
                type="stepAfter"
                dataKey="debt"
                stroke={sectionColors.cards}
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  )
}
