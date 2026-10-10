import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo, useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useCards } from '@/api/cards'
import { useExpenseCategories, type BillRequest, type PaidFrom } from '@/api/expenses'
import type { Contribution, Share } from '@/api/sharedCosts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { ContributionWarning } from '@/components/sharedCosts/ContributionWarning'
import { ShareFields } from '@/components/sharedCosts/ShareFields'
import { shareErrorFields } from '@/components/sharedCosts/shareModel'
import { parseEuroInput } from '@/lib/money'
import { billFormSchema, toBillRequest, type BillFormValues } from './billFormModel'
import { applyApiErrors } from './formErrors'
import { CategorySelect, Field } from './formParts'
import { isCreditCard } from './paidFrom'
import { PaidFromSelect } from './PaidFromSelect'
import { expenseTexts } from './texts'

const t = expenseTexts

/**
 * Add a one-off bill, enter (issue) a bill added from a recurring bill, or edit a bill (FR-4, FR-5):
 * name, category, payee, amount, issue and due date, what it is paid with, the paid state, and how it
 * is shared (shared bills FR-3). A received contribution that would differ is pointed out (FR-6).
 */
export function BillForm({
  defaultValues,
  requireIssued,
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
    formState: { errors, isSubmitting },
  } = useForm<BillFormValues>({ resolver: zodResolver(schema), defaultValues })
  const [paid, paidFromId, amount, share] = useWatch({
    control,
    name: ['paid', 'paidFromId', 'amount', 'share'],
  })
  const amountCents = parseEuroInput(amount)
  const withCard = cards.some((c) => c.id === paidFromId && isCreditCard(c))

  async function submit(values: BillFormValues) {
    setFormError(null)
    try {
      await onSubmit(toBillRequest(values))
    } catch (error) {
      setFormError(
        applyApiErrors(error, defaultValues, setError, {
          amountCents: 'amount',
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
          <Input
            id="issueDate"
            type="date"
            aria-invalid={!!errors.issueDate}
            {...register('issueDate')}
          />
        </Field>
        <Field
          id="dueDate"
          label={t.fields.dueDate}
          hint={dueDateHinted ? t.hints.dueDateHint : undefined}
          error={errors.dueDate?.message}
        >
          <Input
            id="dueDate"
            type="date"
            aria-invalid={!!errors.dueDate}
            {...register('dueDate')}
          />
        </Field>
      </div>
      <Field
        id="paidFromId"
        label={paid ? t.fields.paidFrom : t.fields.usualPaidFrom}
        hint={withCard ? t.hints.creditCard : undefined}
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
          <Input
            id="paidDate"
            type="date"
            aria-invalid={!!errors.paidDate}
            {...register('paidDate')}
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
