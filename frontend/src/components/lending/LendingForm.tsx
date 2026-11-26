import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { usePayers } from '@/api/cashFlow'
import { useCards } from '@/api/cards'
import type { LendingRequest } from '@/api/lending'
import { DateInput } from '@/components/DateInput'
import { payableAccounts } from '@/components/expenses/paidFrom'
import { applyApiErrors } from '@/components/expenses/formErrors'
import { Field } from '@/components/expenses/formParts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { AccountSelect } from './AccountSelect'
import {
  borrowerMaxLength,
  descriptionMaxLength,
  lendingFormSchema,
  toLendingRequest,
  type LendingFormValues,
} from './lendingFormModel'
import { lendingTexts } from './texts'

const t = lendingTexts

/**
 * Add or edit a lending (FR-2, FR-3): borrower with the saved payers suggested (FR-6), amount, lend
 * date, from account, expected return date and an optional description.
 */
export function LendingForm({
  defaultValues,
  currentAccount,
  submitLabel,
  onSubmit,
  onCancel,
}: {
  defaultValues: LendingFormValues
  currentAccount?: { id: string; name: string } | null
  submitLabel: string
  onSubmit: (request: LendingRequest) => Promise<void>
  onCancel: () => void
}) {
  const { data: payers = [] } = usePayers()
  const { data: cards = [], isPending: cardsPending } = useCards('Default')
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LendingFormValues>({
    resolver: zodResolver(lendingFormSchema),
    defaultValues,
  })
  const noAccount = !cardsPending && payableAccounts(cards).length === 0 && !currentAccount

  async function submit(values: LendingFormValues) {
    setFormError(null)
    try {
      await onSubmit(toLendingRequest(values))
    } catch (error) {
      setFormError(
        applyApiErrors(error, defaultValues, setError, {
          amountCents: 'amount',
        }),
      )
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      {noAccount && (
        <div role="alert" className="flex flex-col items-start gap-2 rounded-lg border p-3 text-sm">
          <p>{t.hints.noAccount}</p>
          <Button asChild size="sm" variant="outline">
            <Link to="/settings/cards">{t.actions.setUpAccounts}</Link>
          </Button>
        </div>
      )}
      <Field
        id="borrowerName"
        label={t.fields.borrower}
        hint={t.hints.borrower}
        error={errors.borrowerName?.message}
      >
        <Input
          id="borrowerName"
          list="borrower-suggestions"
          autoComplete="off"
          maxLength={borrowerMaxLength}
          aria-invalid={!!errors.borrowerName}
          {...register('borrowerName')}
        />
        <datalist id="borrower-suggestions">
          {payers.map((payer) => (
            <option key={payer.id} value={payer.name} />
          ))}
        </datalist>
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field id="amount" label={t.fields.amount} error={errors.amount?.message}>
          <Input
            id="amount"
            inputMode="decimal"
            aria-invalid={!!errors.amount}
            {...register('amount')}
          />
        </Field>
        <Field id="lendDate" label={t.fields.lendDate} error={errors.lendDate?.message}>
          <Controller
            control={control}
            name="lendDate"
            render={({ field }) => (
              <DateInput
                id="lendDate"
                aria-invalid={!!errors.lendDate}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                ref={field.ref}
              />
            )}
          />
        </Field>
      </div>
      <Field
        id="fromAccountId"
        label={t.fields.fromAccount}
        hint={t.hints.fromAccount}
        error={errors.fromAccountId?.message}
      >
        <Controller
          control={control}
          name="fromAccountId"
          render={({ field }) => (
            <AccountSelect
              id="fromAccountId"
              cards={cards}
              value={field.value}
              current={currentAccount}
              invalid={!!errors.fromAccountId}
              onChange={field.onChange}
            />
          )}
        />
      </Field>
      <Field
        id="expectedReturnDate"
        label={t.fields.expectedReturnDate}
        hint={t.hints.expectedReturnDate}
        error={errors.expectedReturnDate?.message}
      >
        <Controller
          control={control}
          name="expectedReturnDate"
          render={({ field }) => (
            <DateInput
              id="expectedReturnDate"
              aria-invalid={!!errors.expectedReturnDate}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              ref={field.ref}
            />
          )}
        />
      </Field>
      <Field id="description" label={t.fields.description} error={errors.description?.message}>
        <Input id="description" maxLength={descriptionMaxLength} {...register('description')} />
      </Field>

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
