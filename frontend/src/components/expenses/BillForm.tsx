import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo, useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { Link } from 'react-router'
import { useCards } from '@/api/cards'
import { useExpenseCategories, type BillRequest, type PaidFrom } from '@/api/expenses'
import type { Contribution, Share } from '@/api/sharedCosts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { DateInput } from '@/components/DateInput'
import { ContributionWarning } from '@/components/sharedCosts/ContributionWarning'
import { ShareFields } from '@/components/sharedCosts/ShareFields'
import { shareErrorFields } from '@/components/sharedCosts/shareModel'
import { parseEuroInput } from '@/lib/money'
import {
  billFormSchema,
  recurringErrorFields,
  recurringOptionsOf,
  toBillRequest,
  type BillFormValues,
} from './billFormModel'
import { applyApiErrors } from './formErrors'
import { CategorySelect, Field } from './formParts'
import { isCreditCard, isRevolvingCard } from './paidFrom'
import { PaidFromSelect } from './PaidFromSelect'
import { expenseTexts } from './texts'

const t = expenseTexts

/**
 * Add a one-off bill, enter (issue) a bill added from a recurring bill, or edit a bill (FR-4, FR-5):
 * name, category, payee, amount, issue and due date, what it is paid with, the paid state, and how it
 * is shared (shared bills FR-3). A received contribution that would differ is pointed out (FR-6).
 * A one-off bill can be made recurring with the Recurring switch (Appendix 2, A2.3); a bill from a
 * recurring bill shows it ticked and links to its recurring bill.
 */
export function BillForm({
  defaultValues,
  requireIssued,
  recurringBillId = null,
  currentPaidFrom,
  dueDateHinted = false,
  submitLabel,
  warning,
  currentShare,
  contribution,
  onSubmit,
  onCancel,
}: {
  defaultValues: BillFormValues
  /** A one-off bill is always issued; one from a recurring bill can stay Not issued. */
  requireIssued: boolean
  /** The recurring bill this bill was added from; null for a one-off bill. */
  recurringBillId?: string | null
  currentPaidFrom?: PaidFrom | null
  /** The due date was prefilled from the last bill (FR-2). */
  dueDateHinted?: boolean
  submitLabel: string
  warning?: string
  /** The bill's share, so an archived participant stays listed. */
  currentShare?: Share | null
  /** The bill's contribution, to point out a received one that would differ. */
  contribution?: Contribution | null
  onSubmit: (request: BillRequest) => Promise<void>
  onCancel: () => void
}) {
  const schema = useMemo(() => billFormSchema(requireIssued), [requireIssued])
  const { data: categories = [] } = useExpenseCategories(true)
  const { data: cards = [] } = useCards('Default')
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    control,
    handleSubmit,
    setError,
    getValues,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<BillFormValues>({ resolver: zodResolver(schema), defaultValues })
  const [paid, paidFromId, amount, share, recurring] = useWatch({
    control,
    name: ['paid', 'paidFromId', 'amount', 'share', 'recurring'],
  })
  const fromRecurring = recurringBillId !== null

  function toggleRecurring(checked: boolean) {
    setValue('recurring', checked)
    if (checked) {
      const options = recurringOptionsOf(getValues())
      setValue('recurringAmount', options.recurringAmount)
      setValue('recurringIssueDay', options.recurringIssueDay)
      setValue('recurringDueDay', options.recurringDueDay)
    }
  }
  const amountCents = parseEuroInput(amount)
  const withCard = cards.some((c) => c.id === paidFromId && isCreditCard(c))
  const withRevolving = cards.some((c) => c.id === paidFromId && isRevolvingCard(c))

  async function submit(values: BillFormValues) {
    setFormError(null)
    try {
      await onSubmit(toBillRequest(values))
    } catch (error) {
      setFormError(
        applyApiErrors(error, defaultValues, setError, {
          amountCents: 'amount',
          ...recurringErrorFields,
          ...shareErrorFields,
        }),
      )
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      <Field id="name" label={t.fields.name} error={errors.name?.message}>
        <Input id="name" maxLength={50} aria-invalid={!!errors.name} {...register('name')} />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="categoryId" label={t.fields.category} error={errors.categoryId?.message}>
          <CategorySelect
            id="categoryId"
            categories={categories}
            current={defaultValues.categoryId}
            invalid={!!errors.categoryId}
            {...register('categoryId')}
          />
        </Field>
        <Field id="payee" label={t.fields.payee} error={errors.payee?.message}>
          <Input id="payee" maxLength={100} {...register('payee')} />
        </Field>
      </div>
      {!requireIssued && <p className="text-xs text-muted-foreground">{t.hints.notIssued}</p>}
      <div className="grid gap-4 sm:grid-cols-3">
        <Field id="amount" label={t.fields.amount} error={errors.amount?.message}>
          <Input
            id="amount"
            inputMode="decimal"
            aria-invalid={!!errors.amount}
            {...register('amount')}
          />
        </Field>
        <Field id="issueDate" label={t.fields.issueDate} error={errors.issueDate?.message}>
          <Controller
            control={control}
            name="issueDate"
            render={({ field }) => (
              <DateInput
                id="issueDate"
                aria-invalid={!!errors.issueDate}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                ref={field.ref}
              />
            )}
          />
        </Field>
        <Field
          id="dueDate"
          label={t.fields.dueDate}
          hint={dueDateHinted ? t.hints.dueDateHint : undefined}
          error={errors.dueDate?.message}
        >
          <Controller
            control={control}
            name="dueDate"
            render={({ field }) => (
              <DateInput
                id="dueDate"
                aria-invalid={!!errors.dueDate}
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
        id="paidFromId"
        label={paid ? t.fields.paidFrom : t.fields.usualPaidFrom}
        hint={withRevolving ? t.hints.revolvingCard : withCard ? t.hints.creditCard : undefined}
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
              invalid={!!errors.paidFromId}
              onChange={field.onChange}
            />
          )}
        />
      </Field>
      <div className="flex items-center gap-3">
        <Controller
          control={control}
          name="paid"
          render={({ field }) => (
            <Switch id="paid" checked={field.value} onCheckedChange={field.onChange} />
          )}
        />
        <Label htmlFor="paid">{t.fields.paid}</Label>
      </div>
      {paid && (
        <Field id="paidDate" label={t.fields.paidDate} error={errors.paidDate?.message}>
          <Controller
            control={control}
            name="paidDate"
            render={({ field }) => (
              <DateInput
                id="paidDate"
                aria-invalid={!!errors.paidDate}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                ref={field.ref}
              />
            )}
          />
        </Field>
      )}
      <Field id="description" label={t.fields.description} error={errors.description?.message}>
        <Textarea id="description" maxLength={200} rows={2} {...register('description')} />
      </Field>
      <Controller
        control={control}
        name="share"
        render={({ field }) => (
          <ShareFields
            id="share"
            value={field.value}
            onChange={field.onChange}
            amountCents={amountCents}
            cards={cards}
            current={currentShare}
            error={
              errors.share?.message ??
              errors.share?.participantId?.message ??
              errors.share?.participantShares?.message
            }
          />
        )}
      />
      <ContributionWarning
        contributions={contribution ? [contribution] : []}
        amountCents={amountCents}
        share={share}
      />
      <div className="flex flex-col gap-3 rounded-lg border p-4">
        <div className="flex items-center gap-3">
          <Switch
            id="recurring"
            checked={fromRecurring || recurring}
            disabled={fromRecurring}
            onCheckedChange={toggleRecurring}
          />
          <Label htmlFor="recurring">{t.fields.recurring}</Label>
        </div>
        {fromRecurring ? (
          <p className="text-sm text-muted-foreground">
            {t.hints.fromRecurring}.{' '}
            <Link to={`/recurring-bills/${recurringBillId}/edit`} className="underline">
              {t.hints.openRecurring}
            </Link>
          </p>
        ) : (
          <p className="text-sm text-muted-foreground">{t.hints.recurring}</p>
        )}
        {!fromRecurring && recurring && (
          <fieldset className="flex flex-col gap-3">
            <legend className="mb-2 text-sm font-medium">{t.fields.recurringSection}</legend>
            <div className="grid gap-4 sm:grid-cols-3">
              <Field
                id="recurringAmount"
                label={t.fields.optionalAmount}
                error={errors.recurringAmount?.message}
              >
                <Input
                  id="recurringAmount"
                  inputMode="decimal"
                  aria-invalid={!!errors.recurringAmount}
                  {...register('recurringAmount')}
                />
              </Field>
              <Field
                id="recurringIssueDay"
                label={t.fields.issueDay}
                error={errors.recurringIssueDay?.message}
              >
                <Input
                  id="recurringIssueDay"
                  inputMode="numeric"
                  maxLength={2}
                  aria-invalid={!!errors.recurringIssueDay}
                  {...register('recurringIssueDay')}
                />
              </Field>
              <Field
                id="recurringDueDay"
                label={t.fields.dueDay}
                error={errors.recurringDueDay?.message}
              >
                <Input
                  id="recurringDueDay"
                  inputMode="numeric"
                  maxLength={2}
                  aria-invalid={!!errors.recurringDueDay}
                  {...register('recurringDueDay')}
                />
              </Field>
            </div>
            <p className="text-xs text-muted-foreground">{t.recurring.optionsHint}</p>
          </fieldset>
        )}
      </div>

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
