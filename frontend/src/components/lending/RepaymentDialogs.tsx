import { useState } from 'react'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import { useChangeLending, type Lending, type Repayment } from '@/api/lending'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { formatDate } from '@/components/cards/format'
import { DateInput } from '@/components/DateInput'
import { Field } from '@/components/expenses/formParts'
import { PaymentDialog } from '@/components/loans/PaymentDialogs'
import { messageOf } from '@/components/loans/messageOf'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/api/cards'
import { dateEntryMessage, isInvalidDateEntry, todayIso } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { AccountSelect } from './AccountSelect'
import {
  descriptionMaxLength,
  repaymentErrors,
  repaymentFormValuesOf,
  toRepaymentRequest,
  type RepaymentFormValues,
} from './lendingFormModel'
import { lendingTexts } from './texts'

const t = lendingTexts

type FieldErrors = Partial<Record<keyof RepaymentFormValues, string>>

const apiFields: Record<string, keyof RepaymentFormValues> = {
  amountCents: 'amount',
  toAccountId: 'toAccountId',
  receivedDate: 'receivedDate',
  note: 'note',
}

/**
 * Records a repayment, prefilled with everything outstanding, the lending's account and today (FR-4,
 * R2, R3), or edits a saved one. Rendered only while open, so it starts from fresh values each time.
 */
export function RepaymentDialog({
  lending,
  repayment,
  onOpenChange,
}: {
  lending: Lending
  repayment?: Repayment
  onOpenChange: (open: boolean) => void
}) {
  const { data: cards = [] } = useCards('Default')
  const change = useChangeLending()
  const [values, setValues] = useState(() => repaymentFormValuesOf(lending, todayIso(), repayment))
  const [errors, setErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const set = (field: keyof RepaymentFormValues) => (value: string) =>
    setValues((v) => ({ ...v, [field]: value }))

  async function save(): Promise<boolean> {
    const found = repaymentErrors(values, lending, repayment)
    if (isInvalidDateEntry(values.receivedDate)) found.receivedDate = dateEntryMessage
    setErrors(found)
    setFormError(null)
    if (Object.keys(found).length > 0) return false
    const request = toRepaymentRequest(values)
    try {
      await change.mutateAsync(
        repayment
          ? { action: 'updateRepayment', id: lending.id, repaymentId: repayment.id, request }
          : { action: 'addRepayment', id: lending.id, request },
      )
      toast.success(t.repaymentSaved)
      return true
    } catch (error) {
      const mapped: FieldErrors = {}
      if (error instanceof ApiError) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          const key = apiFields[field]
          if (key) mapped[key] = messages[0]
        }
      }
      setErrors(mapped)
      if (Object.keys(mapped).length === 0) setFormError(messageOf(error))
      return false
    }
  }

  return (
    <PaymentDialog
      open
      onOpenChange={onOpenChange}
      title={repayment ? t.actions.editRepayment : t.dialogs.repaymentTitle(lending.borrower.name)}
      description={t.dialogs.repaymentDescription(
        formatCents(lending.outstandingCents + (repayment?.amountCents ?? 0)),
      )}
      onSave={save}
    >
      <Field id="repayment-amount" label={t.fields.amount} error={errors.amount}>
        <Input
          id="repayment-amount"
          inputMode="decimal"
          aria-invalid={!!errors.amount}
          value={values.amount}
          onChange={(e) => set('amount')(e.target.value)}
        />
      </Field>
      <Field id="repayment-account" label={t.fields.toAccount} error={errors.toAccountId}>
        <AccountSelect
          id="repayment-account"
          cards={cards}
          value={values.toAccountId}
          current={repayment?.toAccount ?? lending.fromAccount}
          invalid={!!errors.toAccountId}
          onChange={set('toAccountId')}
        />
      </Field>
      <Field id="repayment-date" label={t.fields.receivedDate} error={errors.receivedDate}>
        <DateInput
          id="repayment-date"
          aria-invalid={!!errors.receivedDate}
          value={values.receivedDate}
          onChange={set('receivedDate')}
        />
      </Field>
      <Field id="repayment-note" label={t.fields.note} error={errors.note}>
        <Input
          id="repayment-note"
          maxLength={descriptionMaxLength}
          value={values.note}
          onChange={(e) => set('note')(e.target.value)}
        />
      </Field>
      {formError && (
        <p role="alert" className="text-sm text-destructive">
          {formError}
        </p>
      )}
    </PaymentDialog>
  )
}

/** Deletes a lending and its repayments after naming them (FR-3). */
export function DeleteLendingDialog({
  lending,
  open,
  onOpenChange,
  onDeleted,
}: {
  lending: Lending
  open: boolean
  onOpenChange: (open: boolean) => void
  onDeleted?: () => void
}) {
  const change = useChangeLending()
  return (
    <CardActionDialog
      open={open}
      onOpenChange={onOpenChange}
      destructive
      title={t.dialogs.deleteTitle(lending.borrower.name)}
      description={t.dialogs.deleteDescription(
        formatCents(lending.amountCents),
        formatDate(lending.lendDate),
        lending.repayments.length,
      )}
      confirmLabel={t.actions.delete}
      onConfirm={async () => {
        try {
          await change.mutateAsync({ action: 'delete', id: lending.id })
        } catch (error) {
          throw new Error(messageOf(error))
        }
        toast.success(t.deleted)
        onDeleted?.()
      }}
    />
  )
}

/** Deletes one repayment; the amount becomes outstanding again (FR-4). */
export function DeleteRepaymentDialog({
  lending,
  repayment,
  onOpenChange,
}: {
  lending: Lending
  repayment: Repayment
  onOpenChange: (open: boolean) => void
}) {
  const change = useChangeLending()
  return (
    <CardActionDialog
      open
      onOpenChange={onOpenChange}
      destructive
      title={t.dialogs.deleteRepaymentTitle}
      description={t.dialogs.deleteRepaymentDescription(
        formatCents(repayment.amountCents),
        formatDate(repayment.receivedDate),
      )}
      confirmLabel={t.actions.deleteRepayment}
      onConfirm={async () => {
        try {
          await change.mutateAsync({
            action: 'deleteRepayment',
            id: lending.id,
            repaymentId: repayment.id,
          })
        } catch (error) {
          throw new Error(messageOf(error))
        }
        toast.success(t.deleted)
      }}
    />
  )
}
