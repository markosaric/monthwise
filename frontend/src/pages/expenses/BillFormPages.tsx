import { useState } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import { useBill, useChangeBill } from '@/api/expenses'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { BillForm } from '@/components/expenses/BillForm'
import { billFormValuesOf, emptyBillFormValues } from '@/components/expenses/billFormModel'
import { onlyPrimaryAccount } from '@/components/expenses/paidFrom'
import { expenseTexts } from '@/components/expenses/texts'
import { messageOf } from '@/components/loans/messageOf'
import { Button } from '@/components/ui/button'
import { formatYearMonth, isYearMonth, todayIso } from '@/lib/dates'
import { PageHeader } from '@/pages/PageHeader'

const t = expenseTexts

/** Add a one-off bill to a month, expected or already paid (FR-4). */
export function BillNewPage() {
  const { yearMonth } = useParams()
  const navigate = useNavigate()
  const { data: cards, isPending } = useCards('Default')
  const change = useChangeBill()
  if (!isYearMonth(yearMonth)) return <Navigate to="/month" replace />
  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>

  return (
    <>
      <PageHeader title={t.bills.addBill} description={formatYearMonth(yearMonth)} />
      <BillForm
        requireIssued
        defaultValues={emptyBillFormValues(onlyPrimaryAccount(cards ?? []), todayIso())}
        submitLabel={t.bills.addBill}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await change.mutateAsync({ action: 'create', month: yearMonth, request })
          toast.success(t.saved)
          navigate(`/month/${yearMonth}`, { replace: true })
        }}
      />
    </>
  )
}

/**
 * Enter or edit a bill (FR-4): a Not issued bill gets today as issue date and the due date hint of its
 * recurring bill (FR-2). Delete removes it from its month; warns when its card payment is due.
 */
export function BillEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: bill, isPending, isError } = useBill(id)
  const change = useChangeBill()
  const [deleting, setDeleting] = useState(false)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This bill could not be loaded.</p>

  const notIssued = bill.state === 'NotIssued'
  const back = `/month/${bill.month}`
  const warning = bill.changesChargedCardPayment ? t.dialogs.chargedWarning : undefined

  return (
    <>
      <PageHeader
        title={notIssued ? t.bills.issueBill : t.bills.editBill}
        description={`${bill.name} · ${formatYearMonth(bill.month)}`}
      />
      <div className="flex max-w-xl flex-col gap-6">
        <BillForm
          requireIssued={bill.recurringBillId === null}
          currentPaidFrom={bill.paidFrom}
          currentShare={bill.share}
          contribution={bill.contribution}
          dueDateHinted={notIssued && bill.dueDateHint !== null}
          defaultValues={billFormValuesOf(bill, todayIso())}
          submitLabel={t.actions.save}
          warning={warning}
          onCancel={() => navigate(-1)}
          onSubmit={async (request) => {
            await change.mutateAsync({ action: 'update', id: bill.id, request })
            toast.success(t.saved)
            navigate(back, { replace: true })
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
        title={t.dialogs.deleteBillTitle(bill.name)}
        description={t.dialogs.deleteBillDescription}
        confirmLabel={t.actions.delete}
        destructive
        onConfirm={async () => {
          try {
            await change.mutateAsync({ action: 'delete', id: bill.id })
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
