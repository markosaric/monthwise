import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo, useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { type CardPurchaseRequest, type InstallmentCard } from '@/api/cardPurchases'
import { ApiError } from '@/api/cards'
import { useExpenseCategories } from '@/api/expenses'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { Switch } from '@/components/ui/switch'
import { formatCents, parseEuroInput } from '@/lib/money'
import {
  installmentAmounts,
  purchaseFormSchema,
  toPurchaseRequest,
  type PurchaseFormValues,
} from './purchaseFormModel'
import { cardPurchaseTexts } from './texts'

const t = cardPurchaseTexts

/** API field names that differ from form field names. */
const apiFieldToFormField: Record<string, keyof PurchaseFormValues> = { amountCents: 'amount' }

/**
 * Record or edit a card purchase (FR-3): card, description, date, amount, in installments (off by
 * default) with their number and the postpone option, and an optional category. The installments
 * are previewed while the user types.
 */
export function PurchaseForm({
  cards,
  defaultValues,
  submitLabel,
  warning,
  onSubmit,
  onCancel,
}: {
  /** Cards purchases can be recorded on: usable, with billing details. */
  cards: InstallmentCard[]
  defaultValues: PurchaseFormValues
  submitLabel: string
  /** Shown above the buttons, e.g. that charged card payments will change. */
  warning?: string
  onSubmit: (request: CardPurchaseRequest) => Promise<void>
  onCancel: () => void
}) {
  const [cardId, setCardId] = useState(defaultValues.cardId)
  const maxInstallments = cards.find((c) => c.id === cardId)?.maxInstallments ?? 36
  const schema = useMemo(() => purchaseFormSchema(maxInstallments), [maxInstallments])
  const { data: categories = [] } = useExpenseCategories(true)
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<PurchaseFormValues>({ resolver: zodResolver(schema), defaultValues })
  const [inInstallments, amount, installmentCount] = useWatch({
    control,
    name: ['inInstallments', 'amount', 'installmentCount'],
  })
  // Cash withdrawal is only for cash withdrawals (bills and expenses FR-1).
  const categoryOptions = categories.filter(
    (c) => !c.isFixed && (!c.isHidden || c.id === defaultValues.categoryId),
  )
  const preview = inInstallments
    ? installmentAmounts(parseEuroInput(amount) ?? 0, Number(installmentCount) || 0)
    : []

  async function submit(values: PurchaseFormValues) {
    setFormError(null)
    try {
      await onSubmit(toPurchaseRequest(values))
    } catch (error) {
      if (!(error instanceof ApiError)) {
        setFormError('Saving failed. Please try again.')
        return
      }
      let mapped = false
      for (const [field, messages] of Object.entries(error.fieldErrors)) {
        const formField = apiFieldToFormField[field] ?? field
        if (formField in defaultValues) {
          setError(formField as keyof PurchaseFormValues, { message: messages[0] })
          mapped = true
        }
      }
      if (!mapped) setFormError(error.message)
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      <Field id="cardId" label={t.fields.card} error={errors.cardId?.message}>
        <Controller
          control={control}
          name="cardId"
          render={({ field }) => (
            <NativeSelect
              id="cardId"
              value={field.value}
              aria-invalid={!!errors.cardId}
              onChange={(e) => {
                field.onChange(e.target.value)
                setCardId(e.target.value)
              }}
            >
              <option value="">Choose…</option>
              {cards.map((card) => (
                <option key={card.id} value={card.id}>
                  {card.name}
                </option>
              ))}
            </NativeSelect>
          )}
        />
      </Field>
      <Field id="description" label={t.fields.description} error={errors.description?.message}>
        <Input
          id="description"
          maxLength={100}
          aria-invalid={!!errors.description}
          {...register('description')}
        />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="purchaseDate" label={t.fields.purchaseDate} error={errors.purchaseDate?.message}>
          <Input
            id="purchaseDate"
            type="date"
            aria-invalid={!!errors.purchaseDate}
            {...register('purchaseDate')}
          />
        </Field>
        <Field id="amount" label={t.fields.amount} error={errors.amount?.message}>
          <Input
            id="amount"
            inputMode="decimal"
            aria-invalid={!!errors.amount}
            {...register('amount')}
          />
        </Field>
      </div>

      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-3">
          <Controller
            control={control}
            name="inInstallments"
            render={({ field }) => (
              <Switch id="inInstallments" checked={field.value} onCheckedChange={field.onChange} />
            )}
          />
          <Label htmlFor="inInstallments">{t.fields.inInstallments}</Label>
        </div>
        <p className="text-xs text-muted-foreground">{t.hints.inInstallments}</p>
      </div>

      {inInstallments && (
        <>
          <Field
            id="installmentCount"
            label={t.fields.installmentCount}
            hint={t.hints.installmentCount(maxInstallments)}
            error={errors.installmentCount?.message}
          >
            <Input
              id="installmentCount"
              inputMode="numeric"
              maxLength={2}
              aria-invalid={!!errors.installmentCount}
              {...register('installmentCount')}
            />
          </Field>
          <div className="flex items-center gap-3">
            <Controller
              control={control}
              name="postponeFirst"
              render={({ field }) => (
                <Switch id="postponeFirst" checked={field.value} onCheckedChange={field.onChange} />
              )}
            />
            <Label htmlFor="postponeFirst">{t.fields.postponeFirst}</Label>
          </div>
          {preview.length > 0 && preview.length <= maxInstallments && (
            <p className="text-sm text-muted-foreground" aria-live="polite">
              {t.hints.preview}: {preview.length} × {formatCents(preview[0]!)}
              {preview[preview.length - 1] !== preview[0] &&
                ` (${t.hints.lastOne} ${formatCents(preview[preview.length - 1]!)})`}
            </p>
          )}
        </>
      )}

      <Field id="categoryId" label={t.fields.category} error={errors.categoryId?.message}>
        <NativeSelect id="categoryId" {...register('categoryId')}>
          <option value="">{t.fields.noCategory}</option>
          {categoryOptions.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </NativeSelect>
      </Field>

      {warning && <p className="text-sm font-medium">{warning}</p>}
      {formError && (
        <p role="alert" className="text-sm text-destructive">
          {formError}
        </p>
      )}
      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {submitLabel}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          {t.actions.cancel}
        </Button>
      </div>
    </form>
  )
}

function Field({
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
