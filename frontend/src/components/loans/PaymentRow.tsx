import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { paymentRef, usePaymentAction, type LoanPayment } from '@/api/loans'
import { formatDate } from '@/components/cards/format'
import { CheckButton, ListRow } from '@/components/design/ListRow'
import { StatusChip } from '@/components/design/StatusChip'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PaymentShareDialog } from '@/components/sharedCosts/PaymentShareDialog'
import { OwnPart, ShareBadges } from '@/components/sharedCosts/SharedMarkers'
import { sharedCostTexts } from '@/components/sharedCosts/texts'
import { formatCents } from '@/lib/money'
import { messageOf } from './messageOf'
import { ChangePaymentDialog, PaidDialog } from './PaymentDialogs'
import { loanTexts } from './texts'

const t = loanTexts

/**
 * One loan payment with its Paid check box, dates and markers (FR-4, FR-5). Ticking asks for the
 * paid date and account; unticking makes it expected again. Expected payments are dimmed. A shared
 * payment shows its participant and own part, and any payment can be shared on its own (shared bills
 * FR-3, FR-7).
 */
export function PaymentRow({
  payment,
  showLoan = true,
  readOnly = false,
}: {
  payment: LoanPayment
  /** On the Month screen the row names and links its loan; on the loan itself it does not. */
  showLoan?: boolean
  readOnly?: boolean
}) {
  const action = usePaymentAction()
  const [dialog, setDialog] = useState<'paid' | 'change' | 'share' | null>(null)
  const residual = payment.kind === 'Residual'
  const paidOnOtherDay = payment.paidDate !== null && payment.paidDate !== payment.dueDate
  const close = (open: boolean) => !open && setDialog(null)

  async function untick() {
    try {
      await action.mutateAsync({ action: 'unpaid', ref: paymentRef(payment) })
      toast.success(t.payment.saved)
    } catch (error) {
      toast.error(messageOf(error))
    }
  }

  const title = showLoan
    ? payment.loanName
    : residual
      ? t.labels.residual
      : `${payment.number} ${t.labels.of} ${payment.total}`

  return (
    <ListRow
      label={title}
      data-expected={!payment.isPaid || undefined}
      dimmed={!payment.isPaid}
      check={
        <CheckButton
          checked={payment.isPaid}
          disabled={readOnly || action.isPending}
          onChange={(event) => (event.target.checked ? setDialog('paid') : untick())}
          label={`${t.payment.paid}: ${payment.loanName} ${payment.month}${residual ? ` ${t.labels.residual}` : ''}`}
        />
      }
      title={
        showLoan ? (
          <Link to={`/loans/${payment.loanId}`} className="hover:underline">
            {title}
          </Link>
        ) : (
          title
        )
      }
      date={formatDate(payment.paidDate ?? payment.dueDate)}
      chips={
        <>
          {payment.isOverdue ? (
            <StatusChip status="overdue" label={t.labels.overdue} />
          ) : (
            <StatusChip
              status={payment.isPaid ? 'done' : 'expected'}
              label={payment.isPaid ? t.payment.paid : t.payment.expected}
            />
          )}
          {showLoan && !residual && (
            <span className="text-xs text-muted-foreground">
              {payment.number} {t.labels.of} {payment.total}
            </span>
          )}
          {showLoan && residual && <Badge variant="secondary">{t.labels.residual}</Badge>}
          {payment.isChanged && <Badge variant="outline">{t.labels.changed}</Badge>}
          {payment.needsAccount && <Badge variant="destructive">{t.labels.needsAccount}</Badge>}
          <ShareBadges share={payment.share} isShareChanged={payment.isShareChanged} />
        </>
      }
      amount={
        <>
          {formatCents(payment.amountCents)}
          <OwnPart cents={payment.ownPartCents} />
        </>
      }
      details={
        <span className="flex flex-col gap-0.5">
          <span>
            {showLoan && `${payment.category.name} · ${payment.subcategory.name} · `}
            {t.methods[payment.paymentMethod]}
            {payment.account && ` · ${payment.account.name}`}
          </span>
          <span className="text-xs">
            {payment.paidDate ? (
              <>
                {t.payment.paidOn} {formatDate(payment.paidDate)}
                {paidOnOtherDay &&
                  ` (${t.payment.due.toLowerCase()} ${formatDate(payment.dueDate)})`}
              </>
            ) : (
              <>
                {t.payment.due} {formatDate(payment.dueDate)}
              </>
            )}
            {` · ${t.payment.billDate} ${formatDate(payment.billDate)}`}
          </span>
          {!readOnly && (
            <span className="flex flex-wrap gap-1 pt-1">
              {!payment.isPaid && (
                <Button size="xs" variant="ghost" onClick={() => setDialog('change')}>
                  {t.actions.changePayment}
                </Button>
              )}
              {payment.isPaid && (
                <Button size="xs" variant="ghost" onClick={() => setDialog('paid')}>
                  {t.actions.correctPaid}
                </Button>
              )}
              <Button size="xs" variant="ghost" onClick={() => setDialog('share')}>
                {sharedCostTexts.payment.title}
              </Button>
            </span>
          )}
          {dialog === 'paid' && <PaidDialog payment={payment} open onOpenChange={close} />}
          {dialog === 'share' && <PaymentShareDialog payment={payment} onOpenChange={close} />}
          {dialog === 'change' && (
            <ChangePaymentDialog payment={payment} open onOpenChange={close} />
          )}
        </span>
      }
    />
  )
}
