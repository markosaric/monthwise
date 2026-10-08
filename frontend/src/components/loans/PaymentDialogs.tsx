import { useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import { paymentRef, usePaymentAction, type LoanPayment, type PaymentAction } from '@/api/loans'
import { formatDate } from '@/components/cards/format'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { todayIso } from '@/lib/dates'
import { centsToEuroInput, formatCents, parseEuroInput } from '@/lib/money'
import { onlyPrimaryAccountId, payingAccounts } from './loanFormModel'
import { messageOf } from './messageOf'
import { loanTexts } from './texts'

const t = loanTexts

/** A small dialog with fields, a save button and an error line. */
function PaymentDialog({
  open,
  onOpenChange,
  title,
  description,
  children,
  onSave,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  children: ReactNode
  onSave: () => Promise<boolean>
}) {
  const [busy, setBusy] = useState(false)
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <div className="flex flex-col gap-3">{children}</div>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={busy}>{t.actions.cancel}</AlertDialogCancel>
          <Button
            disabled={busy}
            onClick={async () => {
              setBusy(true)
              try {
                if (await onSave()) onOpenChange(false)
              } finally {
                setBusy(false)
              }
            }}
          >
            {t.actions.save}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

function useRun(onDone?: () => void) {
  const action = usePaymentAction()
  const [error, setError] = useState<string | null>(null)
  async function run(change: PaymentAction): Promise<boolean> {
    setError(null)
    try {
      await action.mutateAsync(change)
      toast.success(t.payment.saved)
      onDone?.()
      return true
    } catch (e) {
      setError(messageOf(e))
      return false
    }
  }
  return { run, error, isPending: action.isPending }
}

/** Changes the amount and due date of one payment (FR-4). */
export function ChangePaymentDialog({
  payment,
  open,
  onOpenChange,
}: {
  payment: LoanPayment
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [amount, setAmount] = useState(centsToEuroInput(payment.amountCents))
  const [dueDate, setDueDate] = useState(payment.dueDate)
  const { run, error } = useRun()
  const [fieldError, setFieldError] = useState<string | null>(null)

  async function save() {
    const cents = parseEuroInput(amount)
    if (cents === null || cents <= 0) {
      setFieldError(t.errors.amountInvalid)
      return false
    }
    if (dueDate.slice(0, 7) !== payment.month) {
      setFieldError(t.errors.dueDateOutsideMonth)
      return false
    }
    setFieldError(null)
    return run({
      action: 'change',
      ref: paymentRef(payment),
      request: { amountCents: cents, dueDate },
    })
  }

  return (
    <PaymentDialog
      open={open}
      onOpenChange={onOpenChange}
      title={`${t.payment.changeTitle}: ${payment.loanName}`}
      description={t.payment.changeDescription}
      onSave={save}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="payment-amount">{t.payment.amount}</Label>
        <Input
          id="payment-amount"
          inputMode="decimal"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
        />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="payment-due-date">{t.payment.dueDate}</Label>
        <Input
          id="payment-due-date"
          type="date"
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
        />
      </div>
      {(fieldError ?? error) && (
        <p role="alert" className="text-sm text-destructive">
          {fieldError ?? error}
        </p>
      )}
    </PaymentDialog>
  )
}

/**
 * Ticks Paid for a bill, asking for the account (prefilled only with exactly one primary account),
 * or corrects the paid date and account of a paid payment (FR-5).
 */
export function PaidDialog({
  payment,
  open,
  onOpenChange,
}: {
  payment: LoanPayment
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { data: cards = [] } = useCards('Default')
  const accounts = payingAccounts(cards)
  const [paidDate, setPaidDate] = useState(payment.paidDate ?? todayIso())
  const [accountId, setAccountId] = useState(payment.account?.id ?? '')
  const chosenAccountId = accountId || onlyPrimaryAccountId(cards)
  const { run, error } = useRun()
  const [fieldError, setFieldError] = useState<string | null>(null)
  const options =
    payment.account && !accounts.some((a) => a.id === payment.account!.id)
      ? [...accounts, payment.account]
      : accounts

  async function save() {
    if (paidDate === '') {
      setFieldError(t.errors.dateRequired)
      return false
    }
    setFieldError(null)
    return run({
      action: 'paid',
      ref: paymentRef(payment),
      request: { paidDate, accountId: chosenAccountId === '' ? null : chosenAccountId },
    })
  }

  return (
    <PaymentDialog
      open={open}
      onOpenChange={onOpenChange}
      title={`${t.payment.paidTitle}: ${payment.loanName}`}
      description={`${t.payment.paidDescription} ${formatCents(payment.amountCents)}, ${t.payment.due.toLowerCase()} ${formatDate(payment.dueDate)}.`}
      onSave={save}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="paid-date">{t.payment.paidDate}</Label>
        <Input
          id="paid-date"
          type="date"
          min={payment.billDate}
          value={paidDate}
          onChange={(e) => setPaidDate(e.target.value)}
        />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="paid-account">{t.payment.account}</Label>
        <NativeSelect
          id="paid-account"
          value={chosenAccountId}
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
      {(fieldError ?? error) && (
        <p role="alert" className="text-sm text-destructive">
          {fieldError ?? error}
        </p>
      )}
    </PaymentDialog>
  )
}
