import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { useCardPaymentAction, type CardPayment } from '@/api/cardPurchases'
import { useCards } from '@/api/cards'
import { formatDate } from '@/components/cards/format'
import { messageOf } from '@/components/loans/messageOf'
import { PaymentDialog } from '@/components/loans/PaymentDialogs'
import { payingAccounts } from '@/components/loans/loanFormModel'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { formatYearMonth, todayIso } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { cardPurchaseTexts } from './texts'

const t = cardPurchaseTexts

/**
 * One card payment, summed up, with its Paid check box and dates (FR-8). On the Month screen it names
 * the card; on the card it names the month. Selecting it opens the drill-down. Expected card
 * payments are dimmed.
 */
export function CardPaymentRow({
  payment,
  showCard = true,
}: {
  payment: CardPayment
  /** On the Month screen the row names its card; on the card itself it names the month. */
  showCard?: boolean
}) {
  const action = useCardPaymentAction()
  const [paidOpen, setPaidOpen] = useState(false)
  const paidOnOtherDay = payment.paidDate !== null && payment.paidDate !== payment.paymentDate
  const title = showCard ? payment.cardName : formatYearMonth(payment.month)

  async function untick() {
    try {
      await action.mutateAsync({ action: 'unpaid', cardId: payment.cardId, month: payment.month })
      toast.success(t.payment.saved)
    } catch (error) {
      toast.error(messageOf(error))
    }
  }

  return (
    <li
      className={cn('flex items-start gap-3 py-3', !payment.isPaid && 'text-muted-foreground')}
      data-expected={!payment.isPaid || undefined}
    >
      <input
        type="checkbox"
        className="mt-1 size-4 accent-primary"
        checked={payment.isPaid}
        disabled={action.isPending}
        onChange={(event) => (event.target.checked ? setPaidOpen(true) : untick())}
        aria-label={`${t.payment.paid}: ${payment.cardName} ${payment.month}`}
      />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="flex flex-wrap items-center gap-2">
          <Link
            to={`/cards/${payment.cardId}/payments/${payment.month}`}
            className={cn('font-medium hover:underline', payment.isPaid && 'text-foreground')}
          >
            {title}
          </Link>
          {payment.paysFromAccountDirectly && (
            <Badge variant="outline">{t.payment.automatic}</Badge>
          )}
          {payment.isOverdue && <Badge variant="destructive">{t.labels.overdue}</Badge>}
          {payment.needsAccount && <Badge variant="destructive">{t.labels.needsAccount}</Badge>}
        </span>
        <span className="text-sm">
          {t.payment.period} {formatDate(payment.periodFrom)} – {formatDate(payment.statementDate)}
          {payment.account && ` · ${payment.account.name}`}
        </span>
        <span className="text-xs">
          {payment.paidDate ? (
            <>
              {t.payment.paidOn} {formatDate(payment.paidDate)}
              {paidOnOtherDay &&
                ` (${t.payment.due.toLowerCase()} ${formatDate(payment.paymentDate)})`}
            </>
          ) : (
            <>
              {t.payment.due} {formatDate(payment.paymentDate)}
            </>
          )}
        </span>
        {payment.isPaid && (
          <span className="flex flex-wrap gap-1 pt-1">
            <Button size="xs" variant="ghost" onClick={() => setPaidOpen(true)}>
              {t.actions.correctPaid}
            </Button>
          </span>
        )}
      </div>
      <span className={cn('font-medium tabular-nums', payment.isPaid && 'text-foreground')}>
        {formatCents(payment.amountCents)}
      </span>
      {paidOpen && (
        <CardPaidDialog
          payment={payment}
          open
          onOpenChange={(open) => !open && setPaidOpen(false)}
        />
      )}
    </li>
  )
}

/**
 * Ticks Paid for a card payment, asking for the paid date and the account (prefilled with the
 * card's paying account), or corrects them (FR-8).
 */
function CardPaidDialog({
  payment,
  open,
  onOpenChange,
}: {
  payment: CardPayment
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { data: cards = [] } = useCards('Default')
  const action = useCardPaymentAction()
  const accounts = payingAccounts(cards)
  const [paidDate, setPaidDate] = useState(payment.paidDate ?? todayIso())
  const [accountId, setAccountId] = useState(payment.account?.id ?? '')
  const [error, setError] = useState<string | null>(null)
  const options =
    payment.account && !accounts.some((a) => a.id === payment.account!.id)
      ? [...accounts, payment.account]
      : accounts

  async function save() {
    if (paidDate === '') {
      setError(t.errors.dateRequired)
      return false
    }
    setError(null)
    try {
      await action.mutateAsync({
        action: 'paid',
        cardId: payment.cardId,
        month: payment.month,
        request: { paidDate, accountId: accountId === '' ? null : accountId },
      })
      toast.success(t.payment.saved)
      return true
    } catch (e) {
      setError(messageOf(e))
      return false
    }
  }

  return (
    <PaymentDialog
      open={open}
      onOpenChange={onOpenChange}
      title={`${t.payment.paidTitle}: ${payment.cardName}`}
      description={`${t.payment.paidDescription} ${formatCents(payment.amountCents)}, ${t.payment.due.toLowerCase()} ${formatDate(payment.paymentDate)}.`}
      onSave={save}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="card-paid-date">{t.payment.paidDate}</Label>
        <Input
          id="card-paid-date"
          type="date"
          min={payment.statementDate}
          value={paidDate}
          onChange={(e) => setPaidDate(e.target.value)}
        />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="card-paid-account">{t.payment.account}</Label>
        <NativeSelect
          id="card-paid-account"
          value={accountId}
          onChange={(e) => setAccountId(e.target.value)}
        >
          <option value="">Choose…</option>
          {options.map((account) => (
            <option key={account.id} value={account.id}>
              {account.name}
            </option>
          ))}
        </NativeSelect>
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </PaymentDialog>
  )
}
