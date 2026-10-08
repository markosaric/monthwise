import { useState } from 'react'
import { toast } from 'sonner'
import { useChangePaymentChange, type LoanDetails, type PaymentChange } from '@/api/loans'
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
import { addMonths, currentMonth, isYearMonth } from '@/lib/dates'
import { centsToEuroInput, parseEuroInput } from '@/lib/money'
import { messageOf } from './messageOf'
import { loanTexts } from './texts'

const t = loanTexts.change

/** The first month a new payment change can start: after the latest paid payment, and not before the start. */
function firstOpenMonth(details: LoanDetails): string {
  const paid = details.payments.filter((p) => p.isPaid && p.kind === 'Regular').map((p) => p.month)
  const latest = paid.sort().at(-1)
  const candidates = [details.loan.startMonth, latest ? addMonths(latest, 1) : currentMonth()]
  return candidates.sort().at(-1)!
}

/** Adds or edits a payment change: from month, new monthly payment and/or payment day, note (FR-7). */
export function PaymentChangeDialog({
  details,
  change,
  open,
  onOpenChange,
}: {
  details: LoanDetails
  /** The change to edit; a new one when absent. */
  change?: PaymentChange
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const save = useChangePaymentChange(details.loan.id)
  const [fromMonth, setFromMonth] = useState(change?.fromMonth ?? firstOpenMonth(details))
  const [monthly, setMonthly] = useState(
    change?.monthlyPaymentCents == null ? '' : centsToEuroInput(change.monthlyPaymentCents),
  )
  const [day, setDay] = useState(change?.paymentDay == null ? '' : String(change.paymentDay))
  const [note, setNote] = useState(change?.note ?? '')
  const [keepChanged, setKeepChanged] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const coversChangedPayments = details.payments.some(
    (p) => p.isChanged && !p.isPaid && p.kind === 'Regular' && p.month >= fromMonth,
  )

  async function submit() {
    const monthlyCents = monthly.trim() === '' ? null : parseEuroInput(monthly)
    const paymentDay = day.trim() === '' ? null : Number(day)
    if (!isYearMonth(fromMonth)) return setError(loanTexts.errors.monthRequired)
    if (monthlyCents === null && paymentDay === null) return setError(loanTexts.errors.changeEmpty)
    if (monthly.trim() !== '' && (monthlyCents === null || monthlyCents <= 0)) {
      return setError(loanTexts.errors.amountInvalid)
    }
    if (
      paymentDay !== null &&
      !(Number.isInteger(paymentDay) && paymentDay >= 1 && paymentDay <= 31)
    ) {
      return setError(loanTexts.errors.paymentDayInvalid)
    }
    if (note.length > 200) return setError(loanTexts.errors.noteTooLong)
    setError(null)
    const request = {
      fromMonth,
      monthlyPaymentCents: monthlyCents,
      paymentDay,
      note: note.trim() === '' ? null : note.trim(),
      keepChangedPayments: keepChanged,
    }
    try {
      await save.mutateAsync(
        change ? { action: 'update', changeId: change.id, request } : { action: 'create', request },
      )
      toast.success(t.saved)
      onOpenChange(false)
    } catch (e) {
      setError(messageOf(e))
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {t.title}: {details.loan.name}
          </AlertDialogTitle>
          <AlertDialogDescription>{t.description}</AlertDialogDescription>
        </AlertDialogHeader>
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="change-from">{t.fromMonth}</Label>
            <Input
              id="change-from"
              type="month"
              value={fromMonth}
              onChange={(e) => setFromMonth(e.target.value)}
            />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="change-monthly">{t.monthlyPayment}</Label>
              <Input
                id="change-monthly"
                inputMode="decimal"
                value={monthly}
                onChange={(e) => setMonthly(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="change-day">{t.paymentDay}</Label>
              <Input
                id="change-day"
                inputMode="numeric"
                value={day}
                onChange={(e) => setDay(e.target.value)}
              />
            </div>
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="change-note">{t.note}</Label>
            <Input
              id="change-note"
              maxLength={200}
              value={note}
              onChange={(e) => setNote(e.target.value)}
            />
          </div>
          {coversChangedPayments && (
            <div className="flex flex-col gap-1 rounded-lg border p-3 text-sm">
              <p className="text-muted-foreground">{t.keepChangedHint}</p>
              <div className="flex items-center gap-2">
                <input
                  id="change-keep"
                  type="checkbox"
                  className="size-4 accent-primary"
                  checked={keepChanged}
                  onChange={(e) => setKeepChanged(e.target.checked)}
                />
                <Label htmlFor="change-keep">{t.keepChanged}</Label>
              </div>
            </div>
          )}
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={save.isPending}>
            {loanTexts.actions.cancel}
          </AlertDialogCancel>
          <Button disabled={save.isPending} onClick={submit}>
            {loanTexts.actions.save}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
