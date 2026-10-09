import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import { useAddRecurringBills, useChangeBill, useMonthBills, type Bill } from '@/api/expenses'
import { formatDate } from '@/components/cards/format'
import { messageOf } from '@/components/loans/messageOf'
import { PaymentDialog } from '@/components/loans/PaymentDialogs'
import { PaymentTotals } from '@/components/loans/LoanPaymentsSection'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { addMonths, currentMonth, formatYearMonth, isYearMonth, todayIso } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { PaidFromSelect } from './PaidFromSelect'
import { expenseTexts } from './texts'

const t = expenseTexts

/**
 * The Bills part of the Month screen (FR-9): the month's bills with their state and Paid check boxes,
 * totals, and the actions Add recurring bills (FR-3) and Add bill.
 */
export function BillsSection({ month }: { month: string }) {
  const { data, isPending, isError } = useMonthBills(month)
  const [addOpen, setAddOpen] = useState(false)

  return (
    <Card className="max-w-3xl">
      <CardHeader className="flex flex-row flex-wrap items-center gap-2">
        <CardTitle className="mr-auto">{t.bills.title}</CardTitle>
        <Button size="sm" variant="outline" onClick={() => setAddOpen(true)}>
          {t.bills.addRecurring}
        </Button>
        <Button asChild size="sm">
          <Link to={`/month/${month}/bills/new`}>{t.bills.addBill}</Link>
        </Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Bills could not be loaded.</p>}
        {data && data.bills.length === 0 && (
          <p className="text-sm text-muted-foreground">{t.bills.empty}</p>
        )}
        {data && data.bills.length > 0 && (
          <>
            <ul className="flex flex-col divide-y">
              {data.bills.map((bill) => (
                <BillRow key={bill.id} bill={bill} />
              ))}
            </ul>
            <PaymentTotals totals={data.totals} />
            {(data.withCardCents !== 0 || data.notIssuedCount > 0) && (
              <p className="text-xs text-muted-foreground">
                {data.notIssuedCount > 0 && t.bills.notIssuedCount(data.notIssuedCount)}
                {data.notIssuedCount > 0 && data.withCardCents !== 0 && ' · '}
                {data.withCardCents !== 0 &&
                  `${t.bills.withCard}: ${formatCents(data.withCardCents)}`}
              </p>
            )}
          </>
        )}
        <Link to="/recurring-bills" className="text-sm text-muted-foreground underline">
          {t.bills.recurring}
        </Link>
      </CardContent>
      {addOpen && <AddRecurringDialog onOpenChange={(open) => !open && setAddOpen(false)} />}
    </Card>
  )
}

/**
 * One bill (FR-4): Not issued bills are dimmed and cannot be ticked, expected ones are dimmed, overdue
 * ones highlighted, and bills paid with a credit card are marked as part of the card payment.
 */
export function BillRow({ bill }: { bill: Bill }) {
  const change = useChangeBill()
  const [paidOpen, setPaidOpen] = useState(false)
  const notIssued = bill.state === 'NotIssued'
  const paid = bill.state === 'Paid'

  async function untick() {
    try {
      await change.mutateAsync({ action: 'unpaid', id: bill.id })
      toast.success(t.saved)
    } catch (error) {
      toast.error(messageOf(error))
    }
  }

  return (
    <li
      className={cn('flex items-start gap-3 py-3', !paid && 'text-muted-foreground')}
      data-state={bill.state}
    >
      <input
        type="checkbox"
        className="mt-1 size-4 accent-primary"
        checked={paid}
        disabled={notIssued || change.isPending}
        onChange={(event) => (event.target.checked ? setPaidOpen(true) : untick())}
        aria-label={`${t.fields.paid}: ${bill.name}`}
      />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="flex flex-wrap items-center gap-2">
          <Link
            to={`/bills/${bill.id}/edit`}
            className={cn('font-medium hover:underline', paid && 'text-foreground')}
          >
            {bill.name}
          </Link>
          {notIssued && <Badge variant="outline">{t.states.NotIssued}</Badge>}
          {bill.isOverdue && <Badge variant="destructive">{t.labels.overdue}</Badge>}
          {bill.paidWithCard && <Badge variant="secondary">{t.labels.withCard}</Badge>}
        </span>
        <span className="text-sm">
          {bill.category.name}
          {bill.paidFrom && ` · ${bill.paidFrom.name}`}
        </span>
        <span className="text-xs">
          {bill.paidDate ? (
            <>
              {t.labels.paidOn} {formatDate(bill.paidDate)}
              {bill.dueDate && ` (${t.labels.due} ${formatDate(bill.dueDate)})`}
            </>
          ) : bill.dueDate ? (
            <>
              {t.labels.due} {formatDate(bill.dueDate)}
            </>
          ) : (
            bill.dueDateHint && (
              <>
                {t.labels.due} {formatDate(bill.dueDateHint)} ({t.labels.hint})
              </>
            )
          )}
        </span>
        {notIssued && (
          <span className="pt-1">
            <Button asChild size="xs" variant="ghost">
              <Link to={`/bills/${bill.id}/edit`}>{t.bills.issueBill}</Link>
            </Button>
          </span>
        )}
      </div>
      <span className={cn('font-medium tabular-nums', paid && 'text-foreground')}>
        {notIssued ? '–' : formatCents(bill.amountCents)}
      </span>
      {paidOpen && (
        <BillPaidDialog bill={bill} onOpenChange={(open) => !open && setPaidOpen(false)} />
      )}
    </li>
  )
}

/** Ticks Paid for a bill, asking for the paid date and what it was paid with (FR-5). */
function BillPaidDialog({
  bill,
  onOpenChange,
}: {
  bill: Bill
  onOpenChange: (open: boolean) => void
}) {
  const { data: cards = [] } = useCards('Default')
  const change = useChangeBill()
  const [paidDate, setPaidDate] = useState(todayIso())
  const [paidFromId, setPaidFromId] = useState(bill.paidFrom?.id ?? '')
  const [error, setError] = useState<string | null>(null)

  async function save() {
    if (paidDate === '') {
      setError(t.errors.dateRequired)
      return false
    }
    if (paidFromId === '') {
      setError(t.errors.paidFromRequired)
      return false
    }
    setError(null)
    try {
      await change.mutateAsync({ action: 'paid', id: bill.id, request: { paidDate, paidFromId } })
      toast.success(t.saved)
      return true
    } catch (e) {
      setError(messageOf(e))
      return false
    }
  }

  return (
    <PaymentDialog
      open
      onOpenChange={onOpenChange}
      title={t.paidDialog.title(bill.name)}
      description={t.paidDialog.description(
        formatCents(bill.amountCents),
        bill.dueDate ? formatDate(bill.dueDate) : '–',
      )}
      onSave={save}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="bill-paid-date">{t.fields.paidDate}</Label>
        <Input
          id="bill-paid-date"
          type="date"
          min={bill.issueDate ?? undefined}
          value={paidDate}
          onChange={(e) => setPaidDate(e.target.value)}
        />
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="bill-paid-from">{t.fields.paidFrom}</Label>
        <PaidFromSelect
          id="bill-paid-from"
          cards={cards}
          value={paidFromId}
          current={bill.paidFrom}
          onChange={setPaidFromId}
        />
        <p className="text-xs text-muted-foreground">{t.hints.creditCard}</p>
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </PaymentDialog>
  )
}

/** Add recurring bills (FR-3): to the chosen month, the next month by default. */
function AddRecurringDialog({ onOpenChange }: { onOpenChange: (open: boolean) => void }) {
  const navigate = useNavigate()
  const add = useAddRecurringBills()
  const [target, setTarget] = useState(addMonths(currentMonth(), 1))
  const [error, setError] = useState<string | null>(null)

  async function save() {
    if (!isYearMonth(target)) {
      setError(t.errors.dateRequired)
      return false
    }
    try {
      const result = await add.mutateAsync(target)
      toast.success(t.bills.added(result.added, formatYearMonth(target)))
      navigate(`/month/${target}`)
      return true
    } catch (e) {
      setError(messageOf(e))
      return false
    }
  }

  return (
    <PaymentDialog
      open
      onOpenChange={onOpenChange}
      title={t.bills.addRecurring}
      description={t.recurring.description}
      onSave={save}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="add-recurring-month">{t.fields.month}</Label>
        <Input
          id="add-recurring-month"
          type="month"
          value={target}
          onChange={(e) => setTarget(e.target.value)}
        />
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </PaymentDialog>
  )
}
