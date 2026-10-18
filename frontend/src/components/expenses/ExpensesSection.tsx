import { ShoppingCart } from 'lucide-react'
import { Link } from 'react-router'
import { addActions } from '@/components/design/actions'
import { AddButton } from '@/components/design/AddButton'
import { meaningColors } from '@/components/design/colors'
import { ListRow } from '@/components/design/ListRow'
import { SectionCard } from '@/components/design/SectionCard'
import { useMonthExpenses, type Expense, type ExpenseTotals } from '@/api/expenses'
import { formatDate } from '@/components/cards/format'
import { Badge } from '@/components/ui/badge'
import { formatCents } from '@/lib/money'
import { expenseTexts } from './texts'

const t = expenseTexts

/**
 * The Expenses part of the Month screen (FR-9): the month's expenses and cash withdrawals, newest first,
 * with the month total, what was paid from accounts and with credit cards, and the cash withdrawals.
 */
export function ExpensesSection({ month }: { month: string }) {
  const { data, isPending, isError } = useMonthExpenses(month)

  return (
    <SectionCard
      icon={ShoppingCart}
      color={meaningColors.expense}
      title={t.expenses.title}
      status={data && data.expenses.length > 0 ? t.expenses.count(data.expenses.length) : undefined}
      total={data ? formatCents(data.totals.totalCents) : undefined}
      defaultOpen={!data || data.expenses.length === 0}
      actions={
        <>
          <AddButton
            to={`/month/${month}/expenses/new`}
            label={t.expenses.add}
            icon={addActions.expense.icon}
            color={addActions.expense.color}
          />
          <AddButton
            to={`/month/${month}/expenses/new?kind=cash`}
            label={t.expenses.addCashWithdrawal}
            icon={addActions.cash.icon}
            color={addActions.cash.color}
          />
        </>
      }
    >
      <div className="flex flex-col gap-3">
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Expenses could not be loaded.</p>}
        {data && data.expenses.length === 0 && (
          <p className="text-sm text-muted-foreground">{t.expenses.empty}</p>
        )}
        {data && data.expenses.length > 0 && (
          <>
            <ul className="flex flex-col divide-y">
              {data.expenses.map((expense) => (
                <ExpenseRow key={expense.id} expense={expense} />
              ))}
            </ul>
            <ExpenseTotalsList totals={data.totals} />
          </>
        )}
      </div>
    </SectionCard>
  )
}

function ExpenseRow({ expense }: { expense: Expense }) {
  const title = expense.description ?? expense.category.name
  return (
    <ListRow
      label={title}
      title={
        <Link to={`/expenses/${expense.id}/edit`} className="hover:underline">
          {title}
        </Link>
      }
      date={formatDate(expense.date)}
      chips={
        <>
          {expense.isCashWithdrawal ? (
            <Badge variant="outline">{t.expenses.cashWithdrawal}</Badge>
          ) : (
            expense.description && <Badge variant="secondary">{expense.category.name}</Badge>
          )}
          {expense.paidWithCard && <Badge variant="secondary">{t.labels.withCard}</Badge>}
        </>
      }
      amount={formatCents(expense.amountCents)}
      details={<span>{expense.paidFrom.name}</span>}
    />
  )
}

function ExpenseTotalsList({ totals }: { totals: ExpenseTotals }) {
  const items: [string, number][] = [
    [t.expenses.totals.fromAccounts, totals.fromAccountsCents],
    [t.expenses.totals.withCard, totals.withCardCents],
    [t.expenses.totals.cash, totals.cashWithdrawalCents],
    [t.expenses.totals.total, totals.totalCents],
  ]
  return (
    <dl className="grid grid-cols-2 gap-2 border-t pt-3 text-sm sm:grid-cols-4">
      {items.map(([label, cents]) => (
        <div key={label} className="flex flex-col">
          <dt className="text-muted-foreground">{label}</dt>
          <dd className="font-medium tabular-nums">{formatCents(cents)}</dd>
        </div>
      ))}
    </dl>
  )
}
