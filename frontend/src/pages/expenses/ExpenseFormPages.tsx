import { useState } from 'react'
import { Navigate, useNavigate, useParams, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import { useChangeExpense, useExpense, useLastExpensePaidFrom } from '@/api/expenses'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { ExpenseForm } from '@/components/expenses/ExpenseForm'
import { emptyExpenseFormValues, expenseFormValuesOf } from '@/components/expenses/expenseFormModel'
import {
  onlyPrimaryAccount,
  payableAccounts,
  payableCreditCards,
} from '@/components/expenses/paidFrom'
import { expenseTexts } from '@/components/expenses/texts'
import { messageOf } from '@/components/loans/messageOf'
import { Button } from '@/components/ui/button'
import { isYearMonth, monthOf, todayIso } from '@/lib/dates'
import { PageHeader } from '@/pages/PageHeader'

const t = expenseTexts

/**
 * Record an expense (FR-7), or a cash withdrawal with `?kind=cash` (FR-8). Dated today, paid with what
 * the last expense was paid with, or the only primary account (E4).
 */
export function ExpenseNewPage() {
  const { yearMonth } = useParams()
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const cash = searchParams.get('kind') === 'cash'
  const { data: cards, isPending } = useCards('Default')
  const { data: last, isPending: lastPending } = useLastExpensePaidFrom()
  const change = useChangeExpense()
  if (!isYearMonth(yearMonth)) return <Navigate to="/month" replace />
  if (isPending || lastPending) return <p className="text-sm text-muted-foreground">Loading…</p>

  const all = cards ?? []
  const lastId = last?.paidFrom?.id
  const lastUsable =
    lastId !== undefined &&
    (cash
      ? payableAccounts(all, true).some((c) => c.id === lastId)
      : [...payableAccounts(all), ...payableCreditCards(all)].some((c) => c.id === lastId))
  const paidFromId = lastUsable && lastId ? lastId : onlyPrimaryAccount(all)
  // An expense of an earlier or later month is dated in that month.
  const today = todayIso()
  const date = monthOf(today) === yearMonth ? today : `${yearMonth}-01`

  return (
    <>
      <PageHeader title={cash ? t.expenses.addCashWithdrawal : t.expenses.add} />
      <ExpenseForm
        cashWithdrawal={cash}
        defaultValues={emptyExpenseFormValues(date, paidFromId)}
        submitLabel={t.actions.save}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await change.mutateAsync(
            cash
              ? {
                  action: 'createCash',
                  request: {
                    date: request.date,
                    amountCents: request.amountCents,
                    accountId: request.paidFromId,
                    description: request.description,
                  },
                }
              : { action: 'create', request },
          )
          toast.success(t.saved)
          navigate(`/month/${monthOf(request.date)}`, { replace: true })
        }}
      />
    </>
  )
}

/** Edit or delete an expense or a cash withdrawal (FR-7, FR-8). */
export function ExpenseEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: expense, isPending, isError } = useExpense(id)
  const change = useChangeExpense()
  const [deleting, setDeleting] = useState(false)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This expense could not be loaded.</p>

  const cash = expense.isCashWithdrawal
  const back = `/month/${monthOf(expense.date)}`
  const warning = expense.changesChargedCardPayment ? t.dialogs.chargedWarning : undefined
  const name = expense.description ?? expense.category.name

  return (
    <>
      <PageHeader
        title={cash ? t.expenses.editCashWithdrawal : t.expenses.edit}
        description={name}
      />
      <div className="flex max-w-xl flex-col gap-6">
        <ExpenseForm
          cashWithdrawal={cash}
          currentPaidFrom={expense.paidFrom}
          defaultValues={expenseFormValuesOf(expense)}
          submitLabel={t.actions.save}
          warning={warning}
          onCancel={() => navigate(-1)}
          onSubmit={async (request) => {
            await change.mutateAsync(
              cash
                ? {
                    action: 'updateCash',
                    id: expense.id,
                    request: {
                      date: request.date,
                      amountCents: request.amountCents,
                      accountId: request.paidFromId,
                      description: request.description,
                    },
                  }
                : { action: 'update', id: expense.id, request },
            )
            toast.success(t.saved)
            navigate(`/month/${monthOf(request.date)}`, { replace: true })
          }}
        />
        <div>
          <Button variant="destructive" onClick={() => setDeleting(true)}>
            {t.actions.delete}
          </Button>
        </div>
      </div>
      <CardActionDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={t.dialogs.deleteExpenseTitle(name)}
        description={t.dialogs.deleteExpenseDescription}
        confirmLabel={t.actions.delete}
        destructive
        onConfirm={async () => {
          try {
            await change.mutateAsync({ action: 'delete', id: expense.id })
          } catch (error) {
            throw new Error(messageOf(error))
          }
          toast.success(t.deleted)
          navigate(back, { replace: true })
        }}
      >
        {warning && <p className="text-sm font-medium">{warning}</p>}
      </CardActionDialog>
    </>
  )
}
