import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import {
  useCardPurchase,
  useDeleteCardPurchase,
  useReverseCardPurchase,
  type CardPurchaseDetails,
  type Installment,
} from '@/api/cardPurchases'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { formatDate } from '@/components/cards/format'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { messageOf } from '@/components/loans/messageOf'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PageHeader } from '@/pages/PageHeader'

const t = cardPurchaseTexts

/** A purchase with its installments, and Reverse, Edit and Delete (installments FR-4 to FR-6). */
export function PurchaseDetailsPage() {
  const { id } = useParams()
  const { data, isPending, isError } = useCardPurchase(id)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This purchase could not be loaded.</p>
  return <PurchaseDetailsView details={data} />
}

type Dialog =
  { kind: 'reverse' | 'delete' } | { kind: 'reverseInstallment'; installment: Installment } | null

function PurchaseDetailsView({ details }: { details: CardPurchaseDetails }) {
  const navigate = useNavigate()
  const { purchase, installments } = details
  const reverse = useReverseCardPurchase(purchase.id)
  const remove = useDeleteCardPurchase(purchase.id)
  const [dialog, setDialog] = useState<Dialog>(null)
  const reversed = purchase.reversedOn !== null
  const onOpenChange = (open: boolean) => !open && setDialog(null)

  return (
    <>
      <PageHeader title={purchase.description} />
      <div className="flex max-w-3xl flex-col gap-6">
        <div className="flex flex-wrap items-center gap-2">
          {reversed && <Badge variant="outline">{t.purchase.reversed}</Badge>}
          {purchase.category && <Badge variant="secondary">{purchase.category.name}</Badge>}
        </div>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-[auto_1fr]">
          <Row label={t.fields.card}>
            <Link className="underline" to={`/cards/${purchase.card.id}`}>
              {purchase.card.name}
            </Link>
          </Row>
          <Row label={t.fields.purchaseDate}>{formatDate(purchase.purchaseDate)}</Row>
          <Row label={t.fields.amount}>{formatCents(purchase.amountCents)}</Row>
          <Row label={t.fields.inInstallments}>
            {purchase.inInstallments
              ? `${purchase.installmentCount}${purchase.postponeFirst ? ` · ${t.purchase.postponed}` : ''}`
              : t.purchase.fullPurchase}
          </Row>
          <Row label={t.purchase.remaining}>
            {formatCents(purchase.remainingCents)}
            {purchase.inInstallments &&
              ` (${purchase.installmentsCharged} ${t.purchase.charged}, ${purchase.installmentsLeft} ${t.purchase.left})`}
          </Row>
          {reversed && (
            <Row label={t.purchase.reversed}>
              {formatDate(purchase.reversedOn!)}
              {purchase.reversalReason && ` · ${purchase.reversalReason}`}
            </Row>
          )}
        </dl>
        {!reversed && (
          <div className="flex flex-wrap gap-2">
            <Button asChild>
              <Link to={`/cards/purchases/${purchase.id}/edit`}>{t.actions.edit}</Link>
            </Button>
            <Button variant="outline" onClick={() => setDialog({ kind: 'reverse' })}>
              {t.actions.reverse}
            </Button>
            <Button variant="destructive" onClick={() => setDialog({ kind: 'delete' })}>
              {t.actions.delete}
            </Button>
          </div>
        )}

        <Card>
          <CardHeader>
            <CardTitle>{t.purchase.installments}</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col divide-y">
              {installments.map((installment) => (
                <InstallmentRow
                  key={installment.number}
                  installment={installment}
                  cardId={purchase.card.id}
                  canReverse={
                    !reversed && purchase.inInstallments && installment.reversedOn === null
                  }
                  onReverse={() => setDialog({ kind: 'reverseInstallment', installment })}
                />
              ))}
            </ul>
          </CardContent>
        </Card>
        <Link to={`/cards/${purchase.card.id}`} className="text-sm text-muted-foreground underline">
          Back to {purchase.card.name}
        </Link>
      </div>

      <CardActionDialog
        open={dialog?.kind === 'reverse'}
        onOpenChange={onOpenChange}
        title={t.dialogs.reverseTitle(purchase.description)}
        description={t.dialogs.reverseDescription}
        confirmLabel={t.actions.reverse}
        withReason
        destructive
        onConfirm={async (reason) => {
          await run(() => reverse.mutateAsync({ action: 'purchase', reason }))
          toast.success(t.reversed)
        }}
      />
      {dialog?.kind === 'reverseInstallment' && (
        <CardActionDialog
          open
          onOpenChange={onOpenChange}
          title={t.dialogs.reverseInstallmentTitle(
            purchase.description,
            dialog.installment.number,
            dialog.installment.total,
          )}
          description={t.dialogs.reverseInstallmentDescription}
          confirmLabel={t.actions.reverseInstallment}
          withReason
          destructive
          onConfirm={async (reason) => {
            await run(() =>
              reverse.mutateAsync({
                action: 'installment',
                number: dialog.installment.number,
                reason,
              }),
            )
            toast.success(t.reversed)
          }}
        />
      )}
      <CardActionDialog
        open={dialog?.kind === 'delete'}
        onOpenChange={onOpenChange}
        title={t.dialogs.deleteTitle(purchase.description)}
        description={t.dialogs.deleteDescription}
        confirmLabel={t.actions.delete}
        destructive
        onConfirm={async () => {
          await run(() => remove.mutateAsync())
          toast.success(t.deleted)
          navigate(`/cards/${purchase.card.id}`, { replace: true })
        }}
      >
        {purchase.hasChargedInstallments && (
          <p className="text-sm font-medium">{t.dialogs.chargedWarning}</p>
        )}
      </CardActionDialog>
    </>
  )
}

/** Runs a change and rethrows its first API message, so the dialog shows it. */
async function run(change: () => Promise<unknown>) {
  try {
    await change()
  } catch (error) {
    throw new Error(messageOf(error))
  }
}

function InstallmentRow({
  installment,
  cardId,
  canReverse,
  onReverse,
}: {
  installment: Installment
  cardId: string
  canReverse: boolean
  onReverse: () => void
}) {
  const reversed = installment.reversedOn !== null
  return (
    <li
      className={cn(
        'flex items-start gap-3 py-3',
        !installment.isCharged && 'text-muted-foreground',
      )}
    >
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="flex flex-wrap items-center gap-2">
          <Link
            to={`/cards/${cardId}/payments/${installment.paymentMonth}`}
            className={cn('font-medium hover:underline', reversed && 'line-through')}
          >
            {installment.number} {t.purchase.of} {installment.total} ·{' '}
            {formatYearMonth(installment.paymentMonth)}
          </Link>
          {reversed && <Badge variant="outline">{t.purchase.reversed}</Badge>}
        </span>
        <span className="text-xs">
          {t.payment.statement} {formatDate(installment.statementDate)} · {t.payment.due}{' '}
          {formatDate(installment.paymentDate)}
        </span>
        {reversed && (
          <span className="text-xs">
            {t.purchase.reversed} {formatDate(installment.reversedOn!)}
            {installment.reversalReason && ` · ${installment.reversalReason}`} ·{' '}
            {installment.creditMonth
              ? `${t.purchase.creditIn} ${formatYearMonth(installment.creditMonth)}`
              : t.purchase.removed}
          </span>
        )}
        {canReverse && (
          <span className="pt-1">
            <Button size="xs" variant="ghost" onClick={onReverse}>
              {t.actions.reverseInstallment}
            </Button>
          </span>
        )}
      </div>
      <span className="font-medium tabular-nums">{formatCents(installment.amountCents)}</span>
    </li>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd>{children}</dd>
    </>
  )
}
