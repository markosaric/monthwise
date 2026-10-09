import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useCards } from '@/api/cards'
import { useExpenseCategories, type ExpenseRequest, type PaidFrom } from '@/api/expenses'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { expenseFormSchema, toExpenseRequest, type ExpenseFormValues } from './expenseFormModel'
import { applyApiErrors } from './formErrors'
import { CategorySelect, Field } from './formParts'
import { isCreditCard } from './paidFrom'
import { PaidFromSelect } from './PaidFromSelect'
import { expenseTexts } from './texts'

const t = expenseTexts

/**
 * Record or edit an expense (FR-7) or a cash withdrawal (FR-8). Kept short so it fits on one phone
 * screen: date, amount, category, paid from and an optional description. A cash withdrawal has no
 * category and offers primary accounts only.
 */
export function ExpenseForm({
  cashWithdrawal,
  defaultValues,
  currentPaidFrom,
  submitLabel,
  warning,
  onSubmit,
  onCancel,
}: {
  cashWithdrawal: boolean
  defaultValues: ExpenseFormValues
  currentPaidFrom?: PaidFrom | null
  submitLabel: string
  warning?: string
  onSubmit: (request: ExpenseRequest) => Promise<void>
  onCancel: () => void
}) {
  const { data: categories = [] } = useExpenseCategories(true)
  const { data: cards = [] } = useCards('Default')
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ExpenseFormValues>({
    resolver: zodResolver(expenseFormSchema(cashWithdrawal)),
    defaultValues,
  })
  const paidFromId = useWatch({ control, name: 'paidFromId' })
  const withCard = cards.some((c) => c.id === paidFromId && isCreditCard(c))

  async function submit(values: ExpenseFormValues) {
    setFormError(null)
    try {
      await onSubmit(toExpenseRequest(values, cashWithdrawal))
    } catch (error) {
      setFormError(
        applyApiErrors(error, defaultValues, setError, {
          amountCents: 'amount',
          accountId: 'paidFromId',
        }),
      )
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      <div className="grid grid-cols-2 gap-4">
        <Field id="amount" label={t.fields.amount} error={errors.amount?.message}>
          <Input
            id="amount"
            inputMode="decimal"
            aria-invalid={!!errors.amount}
            {...register('amount')}
          />
        </Field>
        <Field id="date" label={t.fields.date} error={errors.date?.message}>
          <Input id="date" type="date" aria-invalid={!!errors.date} {...register('date')} />
        </Field>
      </div>
      {!cashWithdrawal && (
        <Field id="categoryId" label={t.fields.category} error={errors.categoryId?.message}>
          <CategorySelect
            id="categoryId"
            categories={categories}
            current={defaultValues.categoryId}
            invalid={!!errors.categoryId}
            {...register('categoryId')}
          />
        </Field>
      )}
      <Field
        id="paidFromId"
        label={cashWithdrawal ? t.fields.account : t.fields.paidFrom}
        hint={cashWithdrawal ? t.hints.cashAccount : withCard ? t.hints.creditCard : undefined}
        error={errors.paidFromId?.message}
      >
        <Controller
          control={control}
          name="paidFromId"
          render={({ field }) => (
            <PaidFromSelect
              id="paidFromId"
              cards={cards}
              value={field.value}
              current={currentPaidFrom}
              primaryOnly={cashWithdrawal}
              invalid={!!errors.paidFromId}
              onChange={field.onChange}
            />
          )}
        />
      </Field>
      <Field id="description" label={t.fields.description} error={errors.description?.message}>
        <Input id="description" maxLength={100} {...register('description')} />
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
