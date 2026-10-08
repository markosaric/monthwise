import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { Link } from 'react-router'
import { ApiError, useCards } from '@/api/cards'
import { paymentMethods, useLoanCategories } from '@/api/loans'
import { ListSelectWithAdd } from '@/components/cards/ListSelectWithAdd'
import { Field } from '@/components/cashflow/InputForm'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect } from '@/components/ui/native-select'
import { isYearMonth } from '@/lib/dates'
import {
  apiFieldToFormField,
  endMonthOf,
  loanFormSchema,
  parseCount,
  paymentCountOf,
  standingOrderAccounts,
  type LoanFormValues,
} from './loanFormModel'
import { loanTexts } from './texts'

const t = loanTexts

interface Named {
  id: string
  name: string
}

/** Add/edit form for loans (FR-2, FR-3, FR-10). */
export function LoanForm({
  defaultValues,
  current,
  editing = false,
  submitLabel,
  onSubmit,
  onCancel,
}: {
  defaultValues: LoanFormValues
  /** The category, subcategory and account the loan has now; kept selectable when hidden or no longer usable. */
  current?: { category: Named; subcategory: Named; account: Named | null }
  editing?: boolean
  submitLabel: string
  onSubmit: (values: LoanFormValues) => Promise<void>
  onCancel: () => void
}) {
  const [formError, setFormError] = useState<string | null>(null)
  const { data: cards = [] } = useCards('Default')
  const { data: categories = [] } = useLoanCategories()
  const {
    register,
    control,
    handleSubmit,
    setValue,
    getValues,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoanFormValues>({ resolver: zodResolver(loanFormSchema), defaultValues })
  const [categoryId, paymentMethod, allowsResidualValue] = useWatch({
    control,
    name: ['categoryId', 'paymentMethod', 'allowsResidualValue'],
  })

  const category = categories.find((c) => c.id === categoryId)
  const categoryOptions = withCurrent<Named>(categories, current?.category)
  const subcategoryOptions = withCurrent<Named>(
    category?.subcategories ?? [],
    current?.subcategory.id && categoryId === current.category.id ? current.subcategory : undefined,
  )
  const usable = standingOrderAccounts(cards)
  const accounts = withCurrent<Named>(usable, current?.account ?? undefined)
  const noPrimary = paymentMethod === 'StandingOrder' && accounts.length === 0

  /** Keeps the number of payments and the end month in step: the one typed last wins (A10, A11). */
  function syncCountAndEnd(changed: 'count' | 'end' | 'start') {
    const { startMonth, paymentCount, endMonth, lastEdited } = getValues()
    const source = changed === 'start' ? lastEdited : changed
    if (changed !== 'start') setValue('lastEdited', changed)
    if (!isYearMonth(startMonth)) return
    if (source === 'count') {
      const count = parseCount(paymentCount)
      if (count !== null) setValue('endMonth', endMonthOf(startMonth, count))
    } else if (isYearMonth(endMonth)) {
      const count = paymentCountOf(startMonth, endMonth)
      setValue('paymentCount', count === null ? '' : String(count))
    }
  }

  async function submit(values: LoanFormValues) {
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
          setError(formField as keyof LoanFormValues, { message: messages[0] })
          mapped = true
        }
      }
      if (!mapped) setFormError(error.message)
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      <Field id="name" label={t.fields.name} hint={t.hints.name} error={errors.name?.message}>
        <Input id="name" maxLength={50} aria-invalid={!!errors.name} {...register('name')} />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="categoryId" label={t.fields.category} error={errors.categoryId?.message}>
          <NativeSelect
            id="categoryId"
            aria-invalid={!!errors.categoryId}
            {...register('categoryId', {
              onChange: (event: React.ChangeEvent<HTMLSelectElement>) => {
                const chosen = categories.find((c) => c.id === event.target.value)
                setValue('subcategoryId', '')
                setValue(
                  'allowsResidualValue',
                  (chosen?.allowsResidualValue ?? false) ||
                    (editing && defaultValues.residualValue !== ''),
                )
              },
            })}
          >
            <option value="">Choose…</option>
            {categoryOptions.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </NativeSelect>
        </Field>
        <Field
          id="subcategoryId"
          label={t.fields.subcategory}
          error={errors.subcategoryId?.message}
        >
          <NativeSelect
            id="subcategoryId"
            aria-invalid={!!errors.subcategoryId}
            disabled={categoryId === ''}
            {...register('subcategoryId')}
          >
            <option value="">Choose…</option>
            {subcategoryOptions.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </NativeSelect>
        </Field>
      </div>

      <Field id="lenderId" label={t.fields.lender} error={errors.lenderId?.message}>
        <Controller
          control={control}
          name="lenderId"
          render={({ field }) => (
            <ListSelectWithAdd
              id="lenderId"
              kind="banks"
              value={field.value}
              onChange={field.onChange}
              invalid={!!errors.lenderId}
            />
          )}
        />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="paymentMethod" label={t.fields.paymentMethod}>
          <NativeSelect id="paymentMethod" {...register('paymentMethod')}>
            {paymentMethods.map((method) => (
              <option key={method} value={method}>
                {t.methods[method]}
              </option>
            ))}
          </NativeSelect>
        </Field>
        {paymentMethod === 'StandingOrder' && !noPrimary && (
          <Field
            id="accountId"
            label={t.fields.account}
            hint={usable.length > 1 ? t.hints.account : undefined}
            error={errors.accountId?.message}
          >
            <NativeSelect
              id="accountId"
              aria-invalid={!!errors.accountId}
              {...register('accountId')}
            >
              <option value="">Choose…</option>
              {accounts.map((account) => (
                <option key={account.id} value={account.id}>
                  {account.name}
                </option>
              ))}
            </NativeSelect>
          </Field>
        )}
      </div>
      {noPrimary && (
        <div role="alert" className="flex flex-col items-start gap-2 rounded-lg border p-3 text-sm">
          <p>{t.hints.noPrimaryAccount}</p>
          <Button asChild size="sm" variant="outline">
            <Link to="/settings/cards">{t.actions.setUpAccounts}</Link>
          </Button>
          {errors.accountId && <p className="text-destructive">{errors.accountId.message}</p>}
        </div>
      )}
      {paymentMethod === 'Bill' && <p className="text-sm text-muted-foreground">{t.hints.bill}</p>}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field
          id="startMonth"
          label={t.fields.startMonth}
          hint={t.hints.startMonth}
          error={errors.startMonth?.message}
        >
          <Input
            id="startMonth"
            type="month"
            aria-invalid={!!errors.startMonth}
            {...register('startMonth', { onChange: () => syncCountAndEnd('start') })}
          />
        </Field>
        <Field
          id="paymentDay"
          label={t.fields.paymentDay}
          hint={t.hints.paymentDay}
          error={errors.paymentDay?.message}
        >
          <Input
            id="paymentDay"
            inputMode="numeric"
            aria-invalid={!!errors.paymentDay}
            {...register('paymentDay')}
          />
        </Field>
      </div>

      <Field
        id="monthlyPayment"
        label={t.fields.monthlyPayment}
        error={errors.monthlyPayment?.message}
      >
        <Input
          id="monthlyPayment"
          inputMode="decimal"
          aria-invalid={!!errors.monthlyPayment}
          {...register('monthlyPayment')}
        />
      </Field>

      <div className="flex flex-col gap-1.5">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field
            id="paymentCount"
            label={t.fields.paymentCount}
            error={errors.paymentCount?.message}
          >
            <Input
              id="paymentCount"
              inputMode="numeric"
              aria-invalid={!!errors.paymentCount}
              {...register('paymentCount', { onChange: () => syncCountAndEnd('count') })}
            />
          </Field>
          <Field id="endMonth" label={t.fields.endMonth} error={errors.endMonth?.message}>
            <Input
              id="endMonth"
              type="month"
              aria-invalid={!!errors.endMonth}
              {...register('endMonth', { onChange: () => syncCountAndEnd('end') })}
            />
          </Field>
        </div>
        <p className="text-xs text-muted-foreground">{t.hints.countOrEnd}</p>
      </div>

      {allowsResidualValue && (
        <Field
          id="residualValue"
          label={t.fields.residualValue}
          hint={t.hints.residualValue}
          error={errors.residualValue?.message}
        >
          <Input
            id="residualValue"
            inputMode="decimal"
            aria-invalid={!!errors.residualValue}
            {...register('residualValue')}
          />
        </Field>
      )}

      <Field id="description" label={t.fields.description} error={errors.description?.message}>
        <Input
          id="description"
          maxLength={200}
          aria-invalid={!!errors.description}
          {...register('description')}
        />
      </Field>

      {editing && <p className="text-sm text-muted-foreground">{t.hints.edit}</p>}
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

/** Adds the current value to the options when it is no longer offered (hidden category, unusable account). */
function withCurrent<T extends Named>(options: T[], current: Named | undefined): Named[] {
  return current && !options.some((o) => o.id === current.id) ? [...options, current] : options
}
