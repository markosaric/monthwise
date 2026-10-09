import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { maxInstallmentsChoices } from '@/api/cardPurchases'
import { ApiError, cardTypes, useCards, type CardRequest } from '@/api/cards'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { onlyPrimaryAccountId, payingAccounts } from '@/components/loans/loanFormModel'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { Switch } from '@/components/ui/switch'
import {
  allowsInstallments,
  cardFormSchema,
  emptyCardFormValues,
  toCardRequest,
  type CardFormValues,
  type NumberMode,
} from './cardFormModel'
import { ListSelectWithAdd } from './ListSelectWithAdd'
import { cardTexts } from './texts'

const t = cardTexts
const b = cardPurchaseTexts.billing

/** API field names that differ from form field names. */
const apiFieldToFormField: Record<string, keyof CardFormValues> = { debtLimitCents: 'debtLimit' }
const formFields = new Set(Object.keys(emptyCardFormValues()))

/** Add/edit form (FR-2, FR-4). On edit, `masked` holds the stored numbers to keep, replace or remove. */
export function CardForm({
  defaultValues,
  masked,
  submitLabel,
  onSubmit,
  onCancel,
}: {
  defaultValues: CardFormValues
  masked?: { cardNumber: string | null; iban: string | null }
  submitLabel: string
  onSubmit: (request: CardRequest) => Promise<void>
  onCancel: () => void
}) {
  const editing = masked !== undefined
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    control,
    handleSubmit,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CardFormValues>({ resolver: zodResolver(cardFormSchema), defaultValues })

  const [type, cardNumberMode, ibanMode, debitAllowsInstallments, payingAccountId] = useWatch({
    control,
    name: ['type', 'cardNumberMode', 'ibanMode', 'allowsInstallments', 'payingAccountId'],
  })
  const withBilling = allowsInstallments(type, debitAllowsInstallments)
  const { data: cards = [], isPending: cardsPending } = useCards('Default')
  const accountOptions = payingAccounts(cards).filter((c) => c.isPrimary)
  const onlyPrimary = onlyPrimaryAccountId(cards)

  // The paying account is prefilled only when exactly one account is primary (installments B3).
  useEffect(() => {
    if (withBilling && payingAccountId === '' && onlyPrimary !== '') {
      setValue('payingAccountId', onlyPrimary)
    }
  }, [withBilling, payingAccountId, onlyPrimary, setValue])

  async function submit(values: CardFormValues) {
    setFormError(null)
    try {
      await onSubmit(toCardRequest(values, editing))
    } catch (error) {
      if (!(error instanceof ApiError)) {
        setFormError('Saving failed. Please try again.')
        return
      }
      let mapped = false
      for (const [field, messages] of Object.entries(error.fieldErrors)) {
        const formField = apiFieldToFormField[field] ?? field
        if (formFields.has(formField)) {
          setError(formField as keyof CardFormValues, { message: messages[0] })
          mapped = true
        }
      }
      if (!mapped) setFormError(error.message)
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      <Field id="name" label={t.fields.name} error={errors.name?.message}>
        <Input id="name" maxLength={50} aria-invalid={!!errors.name} {...register('name')} />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="issuerId" label={t.fields.issuer} error={errors.issuerId?.message}>
          <Controller
            control={control}
            name="issuerId"
            render={({ field }) => (
              <ListSelectWithAdd
                id="issuerId"
                kind="issuers"
                value={field.value}
                onChange={field.onChange}
                invalid={!!errors.issuerId}
              />
            )}
          />
        </Field>
        <Field id="bankId" label={t.fields.bank} error={errors.bankId?.message}>
          <Controller
            control={control}
            name="bankId"
            render={({ field }) => (
              <ListSelectWithAdd
                id="bankId"
                kind="banks"
                value={field.value}
                onChange={field.onChange}
                invalid={!!errors.bankId}
              />
            )}
          />
        </Field>
      </div>

      <p className="text-sm text-muted-foreground">{t.hints.numbers}</p>
      <NumberField
        id="cardNumber"
        label={t.fields.cardNumber}
        mode={cardNumberMode}
        masked={masked?.cardNumber ?? null}
        onModeChange={(mode) => setValue('cardNumberMode', mode)}
        error={errors.cardNumber?.message}
        inputProps={{
          ...register('cardNumber'),
          inputMode: 'numeric',
          autoComplete: 'off',
          placeholder: '4111 1111 1111 1111',
        }}
      />
      <NumberField
        id="iban"
        label={t.fields.iban}
        mode={ibanMode}
        masked={masked?.iban ?? null}
        onModeChange={(mode) => setValue('ibanMode', mode)}
        error={errors.iban?.message}
        inputProps={{
          ...register('iban'),
          autoComplete: 'off',
          placeholder: 'HR12 1001 0051 8630 0016 0',
        }}
      />

      <div className="grid gap-4 sm:grid-cols-2">
        <Field
          id="validFrom"
          label={t.fields.validFrom}
          hint={t.hints.validFrom}
          error={errors.validFrom?.message}
        >
          <Input
            id="validFrom"
            type="month"
            aria-invalid={!!errors.validFrom}
            {...register('validFrom')}
          />
        </Field>
        <Field id="validThrough" label={t.fields.validThrough} error={errors.validThrough?.message}>
          <Input
            id="validThrough"
            type="month"
            aria-invalid={!!errors.validThrough}
            {...register('validThrough')}
          />
        </Field>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="type" label={t.fields.type} error={errors.type?.message}>
          <NativeSelect id="type" {...register('type')}>
            {cardTypes.map((value) => (
              <option key={value} value={value}>
                {t.types[value]}
              </option>
            ))}
          </NativeSelect>
        </Field>
        <Field
          id="debtLimit"
          label={t.fields.debtLimit}
          hint={t.hints.debtLimit}
          error={errors.debtLimit?.message}
        >
          <Input
            id="debtLimit"
            inputMode="decimal"
            aria-invalid={!!errors.debtLimit}
            {...register('debtLimit')}
          />
        </Field>
      </div>

      {type === 'Debit' && (
        <div className="flex items-center gap-3">
          <Controller
            control={control}
            name="isPrimary"
            render={({ field }) => (
              <Switch id="isPrimary" checked={field.value} onCheckedChange={field.onChange} />
            )}
          />
          <Label htmlFor="isPrimary">{t.fields.isPrimary}</Label>
        </div>
      )}
      {errors.isPrimary?.message && (
        <p className="text-sm text-destructive">{errors.isPrimary.message}</p>
      )}
      {type === 'Debit' && (
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-3">
            <Controller
              control={control}
              name="allowsInstallments"
              render={({ field }) => (
                <Switch
                  id="allowsInstallments"
                  checked={field.value}
                  onCheckedChange={field.onChange}
                />
              )}
            />
            <Label htmlFor="allowsInstallments">{t.fields.allowsInstallments}</Label>
          </div>
          <p className="text-xs text-muted-foreground">{t.hints.allowsInstallments}</p>
        </div>
      )}

      {errors.allowsInstallments?.message && (
        <p className="text-sm text-destructive">{errors.allowsInstallments.message}</p>
      )}

      {withBilling && (
        <fieldset className="flex flex-col gap-4 rounded-lg border p-3">
          <legend className="px-1 text-sm font-medium">{b.title}</legend>
          {editing && <p className="text-xs text-muted-foreground">{b.changeHint}</p>}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              id="statementDay"
              label={b.statementDay}
              hint={b.statementHint}
              error={errors.statementDay?.message}
            >
              <Input
                id="statementDay"
                inputMode="numeric"
                maxLength={2}
                aria-invalid={!!errors.statementDay}
                {...register('statementDay')}
              />
            </Field>
            <Field
              id="paymentDay"
              label={b.paymentDay}
              hint={b.paymentHint}
              error={errors.paymentDay?.message}
            >
              <Input
                id="paymentDay"
                inputMode="numeric"
                maxLength={2}
                aria-invalid={!!errors.paymentDay}
                {...register('paymentDay')}
              />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              id="payingAccountId"
              label={b.payingAccount}
              error={errors.payingAccountId?.message}
            >
              <NativeSelect
                id="payingAccountId"
                aria-invalid={!!errors.payingAccountId}
                {...register('payingAccountId')}
              >
                <option value="">Choose…</option>
                {accountOptions.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.name}
                  </option>
                ))}
              </NativeSelect>
              {!cardsPending && accountOptions.length === 0 && (
                <p className="text-xs text-muted-foreground">{b.noPrimaryAccount}</p>
              )}
            </Field>
            <Field
              id="maxInstallments"
              label={b.maxInstallments}
              error={errors.maxInstallments?.message}
            >
              <NativeSelect id="maxInstallments" {...register('maxInstallments')}>
                {maxInstallmentsChoices.map((value) => (
                  <option key={value} value={String(value)}>
                    {value}
                  </option>
                ))}
              </NativeSelect>
            </Field>
          </div>
          <div className="flex flex-col gap-1">
            <div className="flex items-center gap-3">
              <Controller
                control={control}
                name="paysFromAccountDirectly"
                render={({ field }) => (
                  <Switch
                    id="paysFromAccountDirectly"
                    checked={field.value}
                    onCheckedChange={field.onChange}
                  />
                )}
              />
              <Label htmlFor="paysFromAccountDirectly">{b.paysFromAccountDirectly}</Label>
            </div>
            <p className="text-xs text-muted-foreground">{b.directHint}</p>
          </div>
        </fieldset>
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

/** On edit, a stored number is shown masked with Replace and Remove; otherwise a plain input. */
function NumberField({
  id,
  label,
  mode,
  masked,
  onModeChange,
  error,
  inputProps,
}: {
  id: string
  label: string
  mode: NumberMode
  masked: string | null
  onModeChange: (mode: NumberMode) => void
  error?: string
  inputProps: React.ComponentProps<'input'>
}) {
  return (
    <Field id={id} label={label} error={error}>
      {masked !== null && mode !== 'enter' ? (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          {mode === 'keep' ? (
            <span className="font-mono">{masked}</span>
          ) : (
            <span className="text-muted-foreground">{t.hints.removedNumber}</span>
          )}
          <Button type="button" variant="outline" size="xs" onClick={() => onModeChange('enter')}>
            {t.hints.replaceNumber}
          </Button>
          {mode === 'keep' ? (
            <Button type="button" variant="ghost" size="xs" onClick={() => onModeChange('remove')}>
              {t.hints.removeNumber}
            </Button>
          ) : (
            <Button type="button" variant="ghost" size="xs" onClick={() => onModeChange('keep')}>
              {t.hints.keepNumber}
            </Button>
          )}
        </div>
      ) : (
        <div className="flex gap-2">
          <Input id={id} aria-invalid={!!error} {...inputProps} />
          {masked !== null && (
            <Button type="button" variant="ghost" onClick={() => onModeChange('keep')}>
              {t.hints.keepNumber}
            </Button>
          )}
        </div>
      )}
    </Field>
  )
}
