import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import {
  useChangeRecurringBill,
  useExpenseCategories,
  useRecurringBill,
  useRecurringBills,
  type RecurringBill,
  type RecurringBillChange,
  type RecurringBillRequest,
} from '@/api/expenses'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import {
  recurringBillFormSchema,
  recurringBillFormValuesOf,
  toRecurringBillRequest,
  type RecurringBillFormValues,
} from '@/components/expenses/billFormModel'
import { applyApiErrors } from '@/components/expenses/formErrors'
import { CategorySelect, Field } from '@/components/expenses/formParts'
import { PaidFromSelect } from '@/components/expenses/PaidFromSelect'
import { expenseTexts } from '@/components/expenses/texts'
import { messageOf } from '@/components/loans/messageOf'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { PageHeader } from '@/pages/PageHeader'

const t = expenseTexts

/** The recurring bills (FR-2): add, edit, stop, restart, and delete those never added to a month. */
export function RecurringBillsPage() {
  const [showStopped, setShowStopped] = useState(false)
  const { data: bills, isPending, isError } = useRecurringBills(showStopped)

  return (
    <>
      <PageHeader title={t.recurring.title} description={t.recurring.description} />
      <div className="flex max-w-2xl flex-col gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <Button asChild>
            <Link to="/recurring-bills/new">{t.recurring.add}</Link>
          </Button>
          <div className="ml-auto flex items-center gap-2">
            <Switch id="show-stopped" checked={showStopped} onCheckedChange={setShowStopped} />
            <Label htmlFor="show-stopped">{t.recurring.showStopped}</Label>
          </div>
        </div>
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && (
          <p className="text-sm text-destructive">Recurring bills could not be loaded.</p>
        )}
        {bills?.length === 0 && (
          <p className="text-sm text-muted-foreground">{t.recurring.empty}</p>
        )}
        <ul className="flex flex-col gap-2">
          {bills?.map((bill) => (
            <RecurringBillRow key={bill.id} bill={bill} />
          ))}
        </ul>
      </div>
    </>
  )
}

function RecurringBillRow({ bill }: { bill: RecurringBill }) {
  const change = useChangeRecurringBill()
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function run(action: RecurringBillChange) {
    setError(null)
    try {
      await change.mutateAsync(action)
      toast.success(t.recurring.saved)
    } catch (e) {
      setError(messageOf(e))
    }
  }

  return (
    <li className="flex flex-col gap-1 rounded-lg border p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Link
          to={`/recurring-bills/${bill.id}/edit`}
          className={bill.isActive ? 'font-medium hover:underline' : 'text-muted-foreground'}
        >
          {bill.name}
        </Link>
        <Badge variant="secondary">{bill.category.name}</Badge>
        {!bill.isActive && <Badge variant="outline">{t.recurring.stopped}</Badge>}
        <span className="ml-auto flex flex-wrap gap-1">
          <Button
            size="sm"
            variant="ghost"
            disabled={change.isPending}
            onClick={() => run({ action: bill.isActive ? 'stop' : 'restart', id: bill.id })}
          >
            {bill.isActive ? t.recurring.stop : t.recurring.restart}
          </Button>
          {bill.canBeDeleted && (
            <Button size="sm" variant="ghost" onClick={() => setDeleting(true)}>
              {t.actions.delete}
            </Button>
          )}
        </span>
      </div>
      <span className="text-sm text-muted-foreground">
        {[bill.payee, bill.paidFrom?.name].filter(Boolean).join(' · ')}
      </span>
      {error && <p className="text-sm text-destructive">{error}</p>}
      <CardActionDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={t.recurring.deleteTitle(bill.name)}
        description={t.recurring.deleteDescription}
        confirmLabel={t.actions.delete}
        destructive
        onConfirm={async () => {
          try {
            await change.mutateAsync({ action: 'delete', id: bill.id })
          } catch (e) {
            throw new Error(messageOf(e))
          }
          toast.success(t.recurring.deleted)
        }}
      />
    </li>
  )
}

export function RecurringBillNewPage() {
  const navigate = useNavigate()
  const change = useChangeRecurringBill()
  return (
    <>
      <PageHeader title={t.recurring.add} description={t.recurring.noDates} />
      <RecurringBillForm
        defaultValues={recurringBillFormValuesOf()}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await change.mutateAsync({ action: 'create', request })
          toast.success(t.recurring.saved)
          navigate('/recurring-bills', { replace: true })
        }}
      />
    </>
  )
}

export function RecurringBillEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: bill, isPending, isError } = useRecurringBill(id)
  const change = useChangeRecurringBill()

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError)
    return <p className="text-sm text-destructive">This recurring bill could not be loaded.</p>

  return (
    <>
      <PageHeader title={t.recurring.edit} description={bill.name} />
      <RecurringBillForm
        defaultValues={recurringBillFormValuesOf(bill)}
        current={bill}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await change.mutateAsync({ action: 'update', id: bill.id, request })
          toast.success(t.recurring.saved)
          navigate('/recurring-bills', { replace: true })
        }}
      />
    </>
  )
}

/** Name, category, payee, what it is usually paid with and a note; no dates or amount (FR-2). */
function RecurringBillForm({
  defaultValues,
  current,
  onSubmit,
  onCancel,
}: {
  defaultValues: RecurringBillFormValues
  current?: RecurringBill
  onSubmit: (request: RecurringBillRequest) => Promise<void>
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
  } = useForm<RecurringBillFormValues>({
    resolver: zodResolver(recurringBillFormSchema),
    defaultValues,
  })

  async function submit(values: RecurringBillFormValues) {
    setFormError(null)
    try {
      await onSubmit(toRecurringBillRequest(values))
    } catch (error) {
      setFormError(applyApiErrors(error, defaultValues, setError))
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
      <Field
        id="paidFromId"
        label={t.fields.usualPaidFrom}
        hint={t.hints.creditCard}
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
              current={current?.paidFrom}
              onChange={field.onChange}
            />
          )}
        />
      </Field>
      <Field id="description" label={t.fields.description} error={errors.description?.message}>
        <Textarea id="description" maxLength={200} rows={2} {...register('description')} />
      </Field>
      {formError && (
        <p role="alert" className="text-sm text-destructive">
          {formError}
        </p>
      )}
      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {t.actions.save}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          {t.actions.cancel}
        </Button>
      </div>
    </form>
  )
}
