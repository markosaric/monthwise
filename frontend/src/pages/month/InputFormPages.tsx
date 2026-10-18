import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import {
  useCreateInput,
  useDeleteEntry,
  useInput,
  useMonthInputs,
  useRecurringInput,
  useSaveEntry,
  useStopRecurringInput,
  useUpdateRecurringInput,
  type EntryRef,
  type InputEntry,
} from '@/api/cashFlow'
import { useCards } from '@/api/cards'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { formatDate } from '@/components/cards/format'
import { InputForm } from '@/components/cashflow/InputForm'
import {
  defaultAccountId,
  emptyInputFormValues,
  inputFormValuesOf,
  recurringFormValuesOf,
  toInputRequest,
  toRecurringRequest,
} from '@/components/cashflow/inputFormModel'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { Button } from '@/components/ui/button'
import { currentMonth, isYearMonth, monthOf, todayIso } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { useReturnTo } from '@/lib/returnTo'
import { PageHeader } from '@/pages/PageHeader'

const t = cashFlowTexts

function useMonthParam(): string {
  const { yearMonth } = useParams()
  return isYearMonth(yearMonth) ? yearMonth : currentMonth()
}

/** Add an input in a month (FR-2); the dates default to today in the current month, else the 1st and 10th. */
export function InputNewPage() {
  const month = useMonthParam()
  const navigate = useNavigate()
  const returnTo = useReturnTo()
  const { data: cards, isPending } = useCards('Default')
  const createInput = useCreateInput()

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  const today = todayIso()
  const [issue, expected] =
    monthOf(today) === month ? [today, today] : [`${month}-01`, `${month}-10`]

  return (
    <>
      <PageHeader title={t.addInput} />
      <InputForm
        mode="new"
        defaultValues={emptyInputFormValues(defaultAccountId(cards ?? []), issue, expected)}
        submitLabel={t.addInput}
        onCancel={() => (returnTo ? navigate(returnTo) : navigate(-1))}
        onSubmit={async (values) => {
          const entry = await createInput.mutateAsync(toInputRequest(values, true))
          toast.success(t.saved)
          navigate(returnTo ?? `/month/${monthOf(entry.receivedDate ?? entry.expectedDate)}`, {
            replace: true,
          })
        }}
      />
    </>
  )
}

/** Edit a stored input (FR-8). */
export function InputEditPage() {
  const { id } = useParams()
  const { data: entry, isPending, isError } = useInput(id)
  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This input could not be loaded.</p>
  return <EntryEditor entry={entry} entryRefValue={{ kind: 'input', id: entry.id! }} />
}

/** Edit one calculated occurrence of a recurring input (FR-7); found in its month's list. */
export function OccurrenceEditPage() {
  const month = useMonthParam()
  const { id, date } = useParams()
  const { data, isPending, isError } = useMonthInputs(month)
  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  const entry = data?.inputs.find((e) => e.recurringInputId === id && e.occurrenceDate === date)
  if (isError || !entry) {
    return <p className="text-sm text-destructive">This occurrence could not be found.</p>
  }
  const ref: EntryRef =
    entry.id !== null
      ? { kind: 'input', id: entry.id }
      : { kind: 'occurrence', recurringInputId: id!, date: date! }
  return <EntryEditor entry={entry} entryRefValue={ref} />
}

function EntryEditor({ entry, entryRefValue }: { entry: InputEntry; entryRefValue: EntryRef }) {
  const month = useMonthParam()
  const navigate = useNavigate()
  const save = useSaveEntry()
  const remove = useDeleteEntry()
  const [confirmDelete, setConfirmDelete] = useState(false)
  const recurring = entry.recurringInputId !== null

  return (
    <>
      <PageHeader
        title={recurring ? t.editOccurrence : t.editInput}
        description={`${entry.payer.name} · ${formatCents(entry.amountCents)}`}
      />
      <div className="flex max-w-xl flex-col gap-6">
        <InputForm
          mode="edit"
          defaultValues={inputFormValuesOf(entry)}
          current={{ account: entry.account, category: entry.category }}
          submitLabel={t.actions.save}
          onCancel={() => navigate(-1)}
          onSubmit={async (values) => {
            await save.mutateAsync({ ref: entryRefValue, request: toInputRequest(values, false) })
            toast.success(t.saved)
            navigate(`/month/${month}`, { replace: true })
          }}
        />
        <div className="flex flex-wrap gap-2 border-t pt-4">
          {recurring && (
            <Button asChild variant="outline">
              <Link to={`/month/${month}/recurring-inputs/${entry.recurringInputId}/edit`}>
                {t.actions.editRecurring}
              </Link>
            </Button>
          )}
          <Button variant="outline" onClick={() => setConfirmDelete(true)}>
            {t.actions.delete}
          </Button>
        </div>
      </div>
      <CardActionDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title={t.deleteTitle}
        description={`${entry.payer.name}, ${formatCents(entry.amountCents)}, ${formatDate(
          entry.receivedDate ?? entry.expectedDate,
        )}.${recurring ? ' Only this occurrence is deleted.' : ''}`}
        confirmLabel={t.actions.delete}
        destructive
        onConfirm={async () => {
          await remove.mutateAsync(entryRefValue)
          toast.success(t.deleted)
          navigate(`/month/${month}`, { replace: true })
        }}
      />
    </>
  )
}

/** Edit or stop the template of a recurring input (FR-7). */
export function RecurringInputEditPage() {
  const month = useMonthParam()
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: recurring, isPending, isError } = useRecurringInput(id)
  const update = useUpdateRecurringInput(id!)
  const stop = useStopRecurringInput(id!)
  const [confirmStop, setConfirmStop] = useState(false)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) {
    return <p className="text-sm text-destructive">This recurring input could not be loaded.</p>
  }

  return (
    <>
      <PageHeader
        title={t.editRecurring}
        description={`${recurring.payer.name} · ${t.frequencies[recurring.frequency ?? 'Monthly']}${
          recurring.isStopped && recurring.endDate
            ? ` · ended ${formatDate(recurring.endDate)}`
            : ''
        }`}
      />
      <div className="flex max-w-xl flex-col gap-6">
        <InputForm
          mode="recurring"
          defaultValues={recurringFormValuesOf(recurring)}
          current={{ account: recurring.account, category: recurring.category }}
          submitLabel={t.actions.save}
          onCancel={() => navigate(-1)}
          onSubmit={async (values) => {
            await update.mutateAsync(toRecurringRequest(values))
            toast.success(t.recurringSaved)
            navigate(`/month/${month}`, { replace: true })
          }}
        />
        {!recurring.isStopped && (
          <div className="border-t pt-4">
            <Button variant="outline" onClick={() => setConfirmStop(true)}>
              {t.actions.stopRecurring}
            </Button>
          </div>
        )}
      </div>
      <CardActionDialog
        open={confirmStop}
        onOpenChange={setConfirmStop}
        title={t.stopTitle}
        description={t.stopDescription}
        confirmLabel={t.actions.stopRecurring}
        destructive
        onConfirm={async () => {
          await stop.mutateAsync()
          toast.success(t.recurringStopped)
          navigate(`/month/${month}`, { replace: true })
        }}
      />
    </>
  )
}
