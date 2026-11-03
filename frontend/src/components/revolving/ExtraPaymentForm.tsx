import { useState } from 'react'
import { useCards } from '@/api/cards'
import { ApiError } from '@/api/http'
import { useRevolvingDebt, type ExtraPaymentRequest, type RevolvingCard } from '@/api/revolving'
import { payableAccounts } from '@/components/expenses/paidFrom'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { todayIso } from '@/lib/dates'
import { centsToEuroInput, formatCents, parseEuroInput } from '@/lib/money'
import { revolvingTexts } from './texts'

const t = revolvingTexts.payment

export type ExtraPaymentFormValues = {
  cardId: string
  accountId: string
  amount: string
  date: string
  description: string
}

type Field = keyof ExtraPaymentFormValues

/** API field names that differ from form field names. */
const apiFieldToFormField: Record<string, Field> = { amountCents: 'amount' }

/**
 * Records or edits an extra payment to a revolving card (revolving FR-8, X2 to X5): the card (only
 * when it is not given), the account (prefilled with the card's paying account), the amount with
 * Pay the whole debt, the date (today, not later) and a description. It fits one phone screen.
 */
export function ExtraPaymentForm({
  cards,
  defaultValues,
  editing = false,
  submitLabel,
  onSubmit,
  onCancel,
}: {
  /** The cards to choose from; a single card is fixed and not shown as a choice. */
  cards: RevolvingCard[]
  defaultValues: ExtraPaymentFormValues
  editing?: boolean
  submitLabel: string
  onSubmit: (cardId: string, request: ExtraPaymentRequest) => Promise<void>
  onCancel: () => void
}) {
  const [values, setValues] = useState(defaultValues)
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const { data: allCards = [] } = useCards('Default')
  const accounts = payableAccounts(allCards)
  const today = todayIso()
  const { data: debt } = useRevolvingDebt(values.cardId || undefined, values.date)
  // When editing, the debt on the date is already lowered by this payment.
  const wholeDebt =
    debt === undefined
      ? null
      : debt.debtCents + (editing ? (parseEuroInput(defaultValues.amount) ?? 0) : 0)

  function set(field: Field, value: string) {
    setValues((current) => {
      const next = { ...current, [field]: value }
      if (field === 'cardId' && current.accountId === '') {
        next.accountId = cards.find((c) => c.id === value)?.terms?.payingAccount.id ?? ''
      }
      return next
    })
  }

  function validate(): Partial<Record<Field, string>> {
    const found: Partial<Record<Field, string>> = {}
    if (values.cardId === '') found.cardId = t.errors.cardRequired
    if (values.accountId === '') found.accountId = t.errors.accountRequired
    const amount = parseEuroInput(values.amount)
    if (amount === null || amount === 0) found.amount = t.errors.amountInvalid
    else if (wholeDebt !== null && amount > wholeDebt) found.amount = t.errors.overDebt
    if (values.date === '') found.date = t.errors.dateRequired
    else if (values.date > today) found.date = t.errors.dateInFuture
    if (values.description.trim().length > 100) found.description = t.errors.descriptionTooLong
    return found
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setFormError(null)
    const found = validate()
    setErrors(found)
    if (Object.keys(found).length > 0) return
    setBusy(true)
    try {
      await onSubmit(values.cardId, {
        accountId: values.accountId,
        date: values.date,
        amountCents: parseEuroInput(values.amount)!,
        description: values.description.trim() === '' ? null : values.description.trim(),
      })
    } catch (error) {
      if (!(error instanceof ApiError)) {
        setFormError('Saving failed. Please try again.')
        return
      }
      const mapped: Partial<Record<Field, string>> = {}
      for (const [field, messages] of Object.entries(error.fieldErrors)) {
        const formField = apiFieldToFormField[field] ?? field
        if (formField in values) mapped[formField as Field] = messages[0]
      }
      setErrors(mapped)
      if (Object.keys(mapped).length === 0) setFormError(error.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex max-w-md flex-col gap-3">
      {cards.length > 1 && (
        <FormField id="cardId" label={t.card} error={errors.cardId}>
          <NativeSelect
            id="cardId"
            value={values.cardId}
            aria-invalid={!!errors.cardId}
            onChange={(e) => set('cardId', e.target.value)}
          >
            <option value="">Choose…</option>
            {cards.map((card) => (
              <option key={card.id} value={card.id}>
                {card.name} · {formatCents(card.debtCents)}
              </option>
            ))}
          </NativeSelect>
        </FormField>
      )}
      <FormField id="accountId" label={t.account} error={errors.accountId}>
        <NativeSelect
          id="accountId"
          value={values.accountId}
          aria-invalid={!!errors.accountId}
          onChange={(e) => set('accountId', e.target.value)}
        >
          <option value="">Choose…</option>
          {accounts.map((account) => (
            <option key={account.id} value={account.id}>
              {account.name}
            </option>
          ))}
        </NativeSelect>
      </FormField>
      <FormField
        id="amount"
        label={t.amount}
        hint={wholeDebt !== null ? t.wholeDebt(formatCents(wholeDebt)) : undefined}
        error={errors.amount}
      >
        <div className="flex gap-2">
          <Input
            id="amount"
            inputMode="decimal"
            aria-invalid={!!errors.amount}
            value={values.amount}
            onChange={(e) => set('amount', e.target.value)}
          />
          <Button
            type="button"
            variant="outline"
            disabled={wholeDebt === null || wholeDebt <= 0}
            onClick={() => wholeDebt !== null && set('amount', centsToEuroInput(wholeDebt))}
          >
            {t.payWholeDebt}
          </Button>
        </div>
      </FormField>
      <FormField id="date" label={t.date} error={errors.date}>
        <Input
          id="date"
          type="date"
          max={today}
          aria-invalid={!!errors.date}
          value={values.date}
          onChange={(e) => set('date', e.target.value)}
        />
      </FormField>
      <FormField id="description" label={t.descriptionField} error={errors.description}>
        <Input
          id="description"
          maxLength={100}
          aria-invalid={!!errors.description}
          value={values.description}
          onChange={(e) => set('description', e.target.value)}
        />
      </FormField>
      {formError && (
        <p role="alert" className="text-sm text-destructive">
          {formError}
        </p>
      )}
      <div className="flex gap-2">
        <Button type="submit" disabled={busy}>
          {submitLabel}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          {t.cancel}
        </Button>
      </div>
    </form>
  )
}

export function FormField({
  id,
  label,
  hint,
  error,
  children,
}: {
  id: string
  label: string
  hint?: string
  error?: string
  children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {hint && !error && <p className="text-xs text-muted-foreground">{hint}</p>}
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
