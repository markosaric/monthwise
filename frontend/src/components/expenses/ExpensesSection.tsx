import { Link } from 'react-router'
import { useMonthExpenses, type Expense, type ExpenseTotals } from '@/api/expenses'
import { formatDate } from '@/components/cards/format'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
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
    <Card className="max-w-3xl">
      <CardHeader className="flex flex-row flex-wrap items-center gap-2">
        <CardTitle className="mr-auto">{t.expenses.title}</CardTitle>
        <Button asChild size="sm" variant="outline">
          <Link to={`/month/${month}/expenses/new?kind=cash`}>{t.expenses.addCashWithdrawal}</Link>
        </Button>
        <Button asChild size="sm">
          <Link to={`/month/${month}/expenses/new`}>{t.expenses.add}</Link>
        </Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
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
      </CardContent>
    </Card>
  )
}

function ExpenseRow({ expense }: { expense: Expense }) {
  const title = expense.description ?? expense.category.name
  return (
    <li className="flex items-start gap-3 py-3">
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="flex flex-wrap items-center gap-2">
          <Link to={`/expenses/${expense.id}/edit`} className="font-medium hover:underline">
            {title}
          </Link>
          {expense.isCashWithdrawal ? (
            <Badge variant="outline">{t.expenses.cashWithdrawal}</Badge>
          ) : (
            expense.description && <Badge variant="secondary">{expense.category.name}</Badge>
          )}
          {expense.paidWithCard && <Badge variant="secondary">{t.labels.withCard}</Badge>}
        </span>
        <span className="text-sm text-muted-foreground">
          {formatDate(expense.date)} · {expense.paidFrom.name}
        </span>
      </div>
      <span className="font-medium tabular-nums">{formatCents(expense.amountCents)}</span>
    </li>
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
