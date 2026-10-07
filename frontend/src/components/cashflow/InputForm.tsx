import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { Link } from 'react-router'
import { frequencies, useInputCategories, usePayers, type InputCategory } from '@/api/cashFlow'
import { ApiError, useCards, type Card } from '@/api/cards'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { Switch } from '@/components/ui/switch'
import { todayIso } from '@/lib/dates'
import {
  apiFieldToFormField,
  inputFormSchema,
  receivingAccounts,
  type InputFormValues,
} from './inputFormModel'
import { cashFlowTexts } from './texts'

const t = cashFlowTexts

/**
 * What the form edits: a new input (may become recurring), a stored input or one occurrence, or the
 * template of a recurring input (no Received check box, always recurring).
 */
export type InputFormMode = 'new' | 'edit' | 'recurring'

/** Add/edit form for inputs (FR-2, FR-3, FR-7, FR-8). */
export function InputForm({
  mode,
  defaultValues,
  current,
  submitLabel,
  onSubmit,
  onCancel,
}: {
  mode: InputFormMode
  defaultValues: InputFormValues
  /** The account and category the input has now; kept selectable even when blocked or hidden since. */
  current?: { account: { id: string; name: string }; category: { id: string; name: string } }
  submitLabel: string
  onSubmit: (values: InputFormValues) => Promise<void>
  onCancel: () => void
}) {
  const [formError, setFormError] = useState<string | null>(null)
  const { data: cards = [], isPending: cardsPending } = useCards('Default')
  const { data: categories = [] } = useInputCategories()
  const { data: payers = [] } = usePayers()
  const {
    register,
    control,
    handleSubmit,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<InputFormValues>({ resolver: zodResolver(inputFormSchema), defaultValues })
  const [received, recurring] = useWatch({ control, name: ['received', 'recurring'] })

  const accounts = withCurrent(receivingAccounts(cards), current?.account)
  const categoryOptions = withCurrent(categories, current?.category)
  const severalPrimary = receivingAccounts(cards).filter((c) => c.isPrimary).length > 1

  if (!cardsPending && accounts.length === 0) {
    return (
      <div className="flex max-w-xl flex-col items-start gap-3">
        <p className="text-sm text-muted-foreground">{t.noAccounts}</p>
        <Button asChild size="sm">
          <Link to="/settings/cards">{t.setUpAccount}</Link>
        </Button>
      </div>
    )
  }

  async function submit(values: InputFormValues) {
    setFormError(null)
    try {
      await onSubmit(values)
    } catch (error) {
      if (!(error instanceof ApiError)) {
        setFormError('Saving failed. Please try again.')
        return
      }
      let mapped = false
      for (const [field, messages] of Object.entries(error.fieldErrors)) {
        const formField = apiFieldToFormField[field] ?? field
        if (formField in defaultValues) {
          setError(formField as keyof InputFormValues, { message: messages[0] })
          mapped = true
        }
      }
      if (!mapped) setFormError(error.message)
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      <Field
        id="payerName"
        label={t.fields.payer}
        hint={t.hints.payer}
        error={errors.payerName?.message}
      >
        <Input
          id="payerName"
          list="payer-suggestions"
          autoComplete="off"
          maxLength={100}
          aria-invalid={!!errors.payerName}
          {...register('payerName')}
        />
        <datalist id="payer-suggestions">
          {payers.map((payer) => (
            <option key={payer.id} value={payer.name} />
          ))}
        </datalist>
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field
          id="accountId"
          label={t.fields.account}
          hint={severalPrimary && mode === 'new' ? t.hints.account : undefined}
          error={errors.accountId?.message}
        >
          <NativeSelect id="accountId" aria-invalid={!!errors.accountId} {...register('accountId')}>
            <option value="">Choose…</option>
            {accounts.map((account) => (
              <option key={account.id} value={account.id}>
                {account.name}
                {'isPrimary' in account && account.isPrimary ? ' (primary)' : ''}
              </option>
            ))}
          </NativeSelect>
        </Field>
        <Field id="categoryId" label={t.fields.category} error={errors.categoryId?.message}>
          <NativeSelect
            id="categoryId"
            aria-invalid={!!errors.categoryId}
            {...register('categoryId')}
          >
            <option value="">Choose…</option>
            {categoryOptions.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </NativeSelect>
        </Field>
      </div>

      <Field id="amount" label={t.fields.amount} error={errors.amount?.message}>
        <Input
          id="amount"
          inputMode="decimal"
          aria-invalid={!!errors.amount}
          {...register('amount')}
        />
      </Field>

      {mode === 'recurring' && (
        <p className="text-sm text-muted-foreground">{t.hints.firstOccurrence}</p>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="issueDate" label={t.fields.issueDate} error={errors.issueDate?.message}>
          <Input
            id="issueDate"
            type="date"
            aria-invalid={!!errors.issueDate}
            {...register('issueDate')}
          />
        </Field>
        <Field id="expectedDate" label={t.fields.expectedDate} error={errors.expectedDate?.message}>
          <Input
            id="expectedDate"
            type="date"
            aria-invalid={!!errors.expectedDate}
            {...register('expectedDate')}
          />
        </Field>
      </div>

      {mode !== 'recurring' && (
        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-3">
            <Controller
              control={control}
              name="received"
              render={({ field }) => (
                <input
                  id="received"
                  type="checkbox"
                  className="size-4 accent-primary"
                  checked={field.value}
                  onChange={(event) => {
                    field.onChange(event.target.checked)
                    setValue('receivedDate', event.target.checked ? todayIso() : '')
                  }}
                />
              )}
            />
            <Label htmlFor="received">{t.fields.received}</Label>
          </div>
          {received && (
            <Field
              id="receivedDate"
              label={t.fields.receivedDate}
              hint={t.hints.received}
              error={errors.receivedDate?.message}
            >
              <Input
                id="receivedDate"
                type="date"
                aria-invalid={!!errors.receivedDate}
                {...register('receivedDate')}
              />
            </Field>
          )}
        </div>
      )}

      <Field id="description" label={t.fields.description} error={errors.description?.message}>
        <Input
          id="description"
          maxLength={200}
          aria-invalid={!!errors.description}
          {...register('description')}
        />
      </Field>

      {mode === 'new' && (
        <div className="flex items-center gap-3">
          <Controller
            control={control}
            name="recurring"
            render={({ field }) => (
              <Switch id="recurring" checked={field.value} onCheckedChange={field.onChange} />
            )}
          />
          <Label htmlFor="recurring">{t.fields.recurring}</Label>
        </div>
      )}
      {(recurring || mode === 'recurring') && (
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id="frequency" label={t.fields.frequency} error={errors.frequency?.message}>
            <NativeSelect id="frequency" {...register('frequency')}>
              {frequencies.map((value) => (
                <option key={value} value={value}>
                  {t.frequencies[value]}
                </option>
              ))}
            </NativeSelect>
          </Field>
          <Field id="endDate" label={t.fields.endDate} error={errors.endDate?.message}>
            <Input
              id="endDate"
              type="date"
              aria-invalid={!!errors.endDate}
              {...register('endDate')}
            />
          </Field>
        </div>
      )}
      {mode === 'recurring' && (
        <div className="flex flex-col gap-1 text-sm text-muted-foreground">
          <p>{t.hints.recurringChange}</p>
          <p>{t.hints.scheduleChange}</p>
        </div>
      )}

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

/** Adds the current value to the options when it is no longer offered (blocked account, hidden category). */
function withCurrent<T extends Card | InputCategory | { id: string; name: string }>(
  options: T[],
  current: { id: string; name: string } | undefined,
): (T | { id: string; name: string })[] {
  return current && !options.some((o) => o.id === current.id) ? [...options, current] : options
}

export function Field({
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
