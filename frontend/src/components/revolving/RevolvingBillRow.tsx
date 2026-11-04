import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import { useChangeRevolvingBill, type RevolvingBill } from '@/api/revolving'
import { formatDate } from '@/components/cards/format'
import { CheckButton, ListRow } from '@/components/design/ListRow'
import { StatusChip } from '@/components/design/StatusChip'
import { payableAccounts } from '@/components/expenses/paidFrom'
import { PaymentDialog } from '@/components/loans/PaymentDialogs'
import { messageOf } from '@/components/loans/messageOf'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DateInput } from '@/components/DateInput'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { dateEntryMessage, formatYearMonth, isInvalidDateEntry, todayIso } from '@/lib/dates'
import { centsToEuroInput, formatCents, parseEuroInput } from '@/lib/money'
import { revolvingTexts } from './texts'

const t = revolvingTexts.bill

/**
 * One monthly bill of a revolving card with its Paid check box and markers (revolving FR-4, FR-8,
 * FR-11). Ticking asks for the account and the amount, prefilled with the minimum; an estimated bill
 * cannot be paid yet. On the Month screen the row names and links its card.
 */
export function RevolvingBillRow({
  bill,
  showCard = true,
  readOnly = false,
}: {
  bill: RevolvingBill
  showCard?: boolean
  readOnly?: boolean
}) {
  const action = useChangeRevolvingBill(bill.cardId, bill.month)
  const [dialog, setDialog] = useState<'paid' | 'correct' | null>(null)
  const close = (open: boolean) => !open && setDialog(null)
  const amount = bill.isPaid ? (bill.paidAmountCents ?? bill.minimumCents) : bill.minimumCents

  async function run(change: Parameters<typeof action.mutateAsync>[0]) {
    try {
      await action.mutateAsync(change)
      toast.success(revolvingTexts.payment.saved)
    } catch (error) {
      toast.error(messageOf(error))
    }
  }

  const title = showCard ? bill.cardName : formatYearMonth(bill.month)
  return (
    <ListRow
      label={`${bill.cardName} ${bill.month}`}
      data-expected={!bill.isPaid || undefined}
      dimmed={!bill.isPaid}
      check={
        <CheckButton
          checked={bill.isPaid}
          disabled={readOnly || bill.isEstimated || action.isPending}
          onChange={(event) =>
            event.target.checked ? setDialog('paid') : run({ action: 'unpaid' })
          }
          label={`${t.paid}: ${bill.cardName} ${bill.month}`}
        />
      }
      title={
        showCard ? (
          <Link to={`/cards/revolving/${bill.cardId}`} className="hover:underline">
            {title}
          </Link>
        ) : (
          title
        )
      }
      date={formatDate(bill.paidDate ?? bill.dueDate)}
      chips={
        <>
          {bill.isOverdue ? (
            <StatusChip status="overdue" label={t.overdue} />
          ) : (
            <StatusChip
              status={bill.isPaid ? 'done' : 'expected'}
              label={bill.isPaid ? t.paid : bill.isEstimated ? t.estimated : t.minimum}
            />
          )}
          {bill.paidAutomatically && <Badge variant="secondary">{t.paidAutomatically}</Badge>}
          {bill.isCorrected && <Badge variant="outline">{t.corrected}</Badge>}
          {bill.needsAccount && <Badge variant="destructive">{t.needsAccount}</Badge>}
        </>
      }
      amount={formatCents(amount)}
      details={
        <span className="flex flex-col gap-0.5">
          <span>
            {t.debt} {formatCents(bill.debtCents)} · {t.interest} {formatCents(bill.interestCents)}{' '}
            · {t.minimum} {formatCents(bill.minimumCents)}
          </span>
          <span className="text-xs">
            {formatDate(bill.billDate)} → {formatDate(bill.dueDate)}
            {bill.account && ` · ${bill.account.name}`}
            {bill.paidDate && ` · ${t.paid} ${formatDate(bill.paidDate)}`}
          </span>
          {!readOnly && !bill.isEstimated && (
            <span className="flex flex-wrap gap-1 pt-1">
              {!bill.paidAutomatically && (
                <Button size="xs" variant="ghost" onClick={() => setDialog('paid')}>
                  {t.pay}
                </Button>
              )}
              <Button size="xs" variant="ghost" onClick={() => setDialog('correct')}>
                {t.correct}
              </Button>
              {bill.isCorrected && (
                <Button
                  size="xs"
                  variant="ghost"
                  onClick={() => run({ action: 'clearCorrection' })}
                >
                  {t.clearCorrection}
                </Button>
              )}
            </span>
          )}
          {dialog === 'paid' && <BillPaidDialog bill={bill} onOpenChange={close} />}
          {dialog === 'correct' && <BillCorrectionDialog bill={bill} onOpenChange={close} />}
        </span>
      }
    />
  )
}

/** Pays a bill: date (from the bill date up to today), account and amount, prefilled with the minimum (FR-8). */
export function BillPaidDialog({
  bill,
  onOpenChange,
}: {
  bill: RevolvingBill
  onOpenChange: (open: boolean) => void
}) {
  const action = useChangeRevolvingBill(bill.cardId, bill.month)
  const { data: cards = [] } = useCards('Default')
  const accounts = payableAccounts(cards)
  const today = todayIso()
  const [paidDate, setPaidDate] = useState(
    bill.paidDate ?? (today < bill.billDate ? bill.billDate : today),
  )
  const [accountId, setAccountId] = useState(bill.account?.id ?? '')
  const [amount, setAmount] = useState(centsToEuroInput(bill.paidAmountCents ?? bill.minimumCents))
  const [error, setError] = useState<string | null>(null)
  const options =
    bill.account && !accounts.some((a) => a.id === bill.account!.id)
      ? [...accounts, bill.account]
      : accounts

  async function save() {
    const amountCents = parseEuroInput(amount)
    if (amountCents === null || amountCents === 0) {
      setError(revolvingTexts.payment.errors.amountInvalid)
      return false
    }
    if (isInvalidDateEntry(paidDate)) {
      setError(dateEntryMessage)
      return false
    }
    if (paidDate === '') {
      setError(revolvingTexts.payment.errors.dateRequired)
      return false
    }
    setError(null)
    try {
      await action.mutateAsync({
        action: 'paid',
        request: { paidDate, accountId: accountId === '' ? null : accountId, amountCents },
      })
      toast.success(revolvingTexts.payment.saved)
      return true
    } catch (e) {
      setError(messageOf(e))
      return false
    }
  }

  return (
    <PaymentDialog
      open
      onOpenChange={onOpenChange}
      title={t.payTitle(bill.cardName, formatYearMonth(bill.month))}
      description={`${t.payDescription} ${t.minimum}: ${formatCents(bill.minimumCents)}.`}
      onSave={save}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="bill-amount">{t.amount}</Label>
        <Input
          id="bill-amount"
          inputMode="decimal"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
        />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="bill-paid-date">{t.date}</Label>
        <DateInput id="bill-paid-date" value={paidDate} onChange={setPaidDate} />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="bill-account">{t.account}</Label>
        <NativeSelect
          id="bill-account"
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

/** Corrects a bill to the bank's bill: minimum and interest, empty keeps the calculated value (FR-4, FR-7). */
function BillCorrectionDialog({
  bill,
  onOpenChange,
}: {
  bill: RevolvingBill
  onOpenChange: (open: boolean) => void
}) {
  const action = useChangeRevolvingBill(bill.cardId, bill.month)
  const [minimum, setMinimum] = useState('')
  const [interest, setInterest] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function save() {
    const minimumCents = minimum.trim() === '' ? null : parseEuroInput(minimum)
    const interestCents = interest.trim() === '' ? null : parseEuroInput(interest)
    if (
      (minimum.trim() !== '' && minimumCents === null) ||
      (interest.trim() !== '' && interestCents === null)
    ) {
      setError(revolvingTexts.form.errors.amountInvalid)
      return false
    }
    setError(null)
    try {
      await action.mutateAsync({ action: 'correct', request: { minimumCents, interestCents } })
      toast.success(revolvingTexts.payment.saved)
      return true
    } catch (e) {
      setError(messageOf(e))
      return false
    }
  }

  return (
    <PaymentDialog
      open
      onOpenChange={onOpenChange}
      title={t.correctTitle(bill.cardName, formatYearMonth(bill.month))}
      description={t.correctDescription}
      onSave={save}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="bill-minimum">{`${t.minimum} (€)`}</Label>
        <Input
          id="bill-minimum"
          inputMode="decimal"
          placeholder={centsToEuroInput(bill.minimumCents)}
          value={minimum}
          onChange={(e) => setMinimum(e.target.value)}
        />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="bill-interest">{`${t.interest} (€)`}</Label>
        <Input
          id="bill-interest"
          inputMode="decimal"
          placeholder={centsToEuroInput(bill.interestCents)}
          value={interest}
          onChange={(e) => setInterest(e.target.value)}
        />
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </PaymentDialog>
  )
}
