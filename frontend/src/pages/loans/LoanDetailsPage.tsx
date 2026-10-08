import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import {
  useChangePaymentChange,
  useCloseLoan,
  useDeleteLoan,
  useLoan,
  type LoanDetails,
  type PaymentChange,
} from '@/api/loans'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { formatDate, formatMonth } from '@/components/cards/format'
import { PaymentChangeDialog } from '@/components/loans/PaymentChangeDialog'
import { messageOf } from '@/components/loans/messageOf'
import { PaymentRow } from '@/components/loans/PaymentRow'
import { loanTexts } from '@/components/loans/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCents } from '@/lib/money'
import { PageHeader } from '@/pages/PageHeader'

const t = loanTexts

/** A loan with its payments made and left, remaining amount, payment changes, and Close and Delete (FR-7, FR-9, FR-10). */
export function LoanDetailsPage() {
  const { id } = useParams()
  const { data: details, isPending, isError } = useLoan(id)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This loan could not be loaded.</p>
  return <LoanDetailsView details={details} />
}

type Dialog =
  | { kind: 'close' | 'delete' | 'addChange' }
  | { kind: 'editChange' | 'deleteChange'; change: PaymentChange }
  | null

function LoanDetailsView({ details }: { details: LoanDetails }) {
  const navigate = useNavigate()
  const { loan, changes, payments } = details
  const closeLoan = useCloseLoan(loan.id)
  const deleteLoan = useDeleteLoan(loan.id)
  const changeAction = useChangePaymentChange(loan.id)
  const [dialog, setDialog] = useState<Dialog>(null)
  const closed = loan.status === 'Closed'
  const hasPaid = payments.some((p) => p.isPaid)
  const onOpenChange = (open: boolean) => !open && setDialog(null)

  return (
    <>
      <PageHeader title={loan.name} description={loan.description ?? undefined} />
      <div className="flex max-w-3xl flex-col gap-6">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="secondary">{t.statuses[loan.status]}</Badge>
          {loan.needsAccount && <Badge variant="destructive">{t.labels.needsAccount}</Badge>}
          {loan.hasOverduePayment && <Badge variant="destructive">{t.labels.overdue}</Badge>}
        </div>

        <dl className="grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-[auto_1fr]">
          <Row label={t.fields.category}>
            {loan.category.name} · {loan.subcategory.name}
          </Row>
          <Row label={t.fields.lender}>{loan.lender.name}</Row>
          <Row label={t.fields.paymentMethod}>
            {t.methods[loan.paymentMethod]}
            {loan.account && ` · ${loan.account.name}`}
          </Row>
          <Row label={t.labels.monthlyPayment}>
            {formatCents(loan.currentMonthlyPaymentCents)}, {t.labels.day} {loan.currentPaymentDay}
            {(loan.currentMonthlyPaymentCents !== loan.monthlyPaymentCents ||
              loan.currentPaymentDay !== loan.paymentDay) &&
              ` (${t.labels.original.toLowerCase()} ${formatCents(loan.monthlyPaymentCents)}, ${t.labels.day} ${loan.paymentDay})`}
          </Row>
          <Row label={`${t.fields.startMonth} – ${t.fields.endMonth}`}>
            {formatMonth(loan.startMonth)} – {formatMonth(loan.endMonth)}
          </Row>
          {loan.nextPaymentDate && (
            <Row label={t.labels.nextPayment}>{formatDate(loan.nextPaymentDate)}</Row>
          )}
          <Row label={t.details.paymentsMade}>
            {loan.paymentsPaid} · {formatCents(loan.paidAmountCents)}
          </Row>
          <Row label={t.details.paymentsLeft}>
            {loan.paymentsLeft} {t.labels.of} {loan.paymentCount} ·{' '}
            {formatCents(loan.remainingAmountCents)}
          </Row>
          {loan.residualValueCents !== null && (
            <Row label={t.labels.residualValue}>{formatCents(loan.residualValueCents)}</Row>
          )}
          {loan.closedOn && <Row label={t.details.closedOn}>{formatDate(loan.closedOn)}</Row>}
        </dl>

        {!closed && (
          <div className="flex flex-wrap gap-2">
            <Button asChild>
              <Link to={`/loans/${loan.id}/edit`}>{t.actions.edit}</Link>
            </Button>
            <Button variant="outline" onClick={() => setDialog({ kind: 'addChange' })}>
              {t.actions.addChange}
            </Button>
            <Button variant="outline" onClick={() => setDialog({ kind: 'close' })}>
              {t.actions.close}
            </Button>
            <Button
              variant="ghost"
              onClick={() => setDialog({ kind: 'delete' })}
              disabled={hasPaid}
              title={hasPaid ? t.deleteDialog.hasPaid : undefined}
            >
              {t.actions.delete}
            </Button>
          </div>
        )}
        {!closed && hasPaid && (
          <p className="text-xs text-muted-foreground">{t.deleteDialog.hasPaid}</p>
        )}

        <Card>
          <CardHeader>
            <CardTitle>{t.details.changes}</CardTitle>
          </CardHeader>
          <CardContent className="text-sm">
            {changes.length === 0 ? (
              <p className="text-muted-foreground">{t.details.noChanges}</p>
            ) : (
              <ul className="flex flex-col divide-y">
                {changes.map((change) => (
                  <li key={change.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2">
                    <span className="font-medium">{formatMonth(change.fromMonth)}</span>
                    {change.monthlyPaymentCents !== null && (
                      <span className="tabular-nums">
                        {formatCents(change.monthlyPaymentCents)}
                      </span>
                    )}
                    {change.paymentDay !== null && (
                      <span>
                        {t.labels.day} {change.paymentDay}
                      </span>
                    )}
                    {change.note && <span className="text-muted-foreground">{change.note}</span>}
                    {change.canChange && !closed ? (
                      <span className="ml-auto flex gap-1">
                        <Button
                          size="xs"
                          variant="ghost"
                          onClick={() => setDialog({ kind: 'editChange', change })}
                        >
                          {t.actions.editChange}
                        </Button>
                        <Button
                          size="xs"
                          variant="ghost"
                          onClick={() => setDialog({ kind: 'deleteChange', change })}
                        >
                          {t.actions.deleteChange}
                        </Button>
                      </span>
                    ) : (
                      <span className="ml-auto text-xs text-muted-foreground">
                        {t.change.locked}
                      </span>
                    )}
                  </li>
                ))}
                <li className="flex flex-wrap gap-x-3 py-2 text-muted-foreground">
                  <span>{t.labels.original}</span>
                  <span className="tabular-nums">{formatCents(loan.monthlyPaymentCents)}</span>
                  <span>
                    {t.labels.day} {loan.paymentDay}
                  </span>
                </li>
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t.details.payments}</CardTitle>
            <CardAction className="text-sm text-muted-foreground">
              {loan.paymentsPaid} / {payments.length}
            </CardAction>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col divide-y">
              {payments.map((payment) => (
                <PaymentRow
                  key={`${payment.month}-${payment.kind}`}
                  payment={payment}
                  showLoan={false}
                  readOnly={closed}
                />
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>

      {(dialog?.kind === 'addChange' || dialog?.kind === 'editChange') && (
        <PaymentChangeDialog
          details={details}
          change={dialog.kind === 'editChange' ? dialog.change : undefined}
          open
          onOpenChange={onOpenChange}
        />
      )}
      <CardActionDialog
        open={dialog?.kind === 'deleteChange'}
        onOpenChange={onOpenChange}
        title={t.change.deleteTitle}
        description={dialog?.kind === 'deleteChange' ? formatMonth(dialog.change.fromMonth) : ''}
        confirmLabel={t.actions.deleteChange}
        destructive
        onConfirm={async () => {
          if (dialog?.kind !== 'deleteChange') return
          try {
            await changeAction.mutateAsync({ action: 'delete', changeId: dialog.change.id })
          } catch (e) {
            throw new Error(messageOf(e))
          }
          toast.success(t.change.deleted)
        }}
      />
      <CardActionDialog
        open={dialog?.kind === 'close'}
        onOpenChange={onOpenChange}
        title={t.closeDialog.title(loan.name)}
        description={t.closeDialog.description}
        confirmLabel={t.actions.close}
        destructive
        onConfirm={async () => {
          await closeLoan.mutateAsync()
          toast.success(t.closed)
        }}
      />
      <CardActionDialog
        open={dialog?.kind === 'delete'}
        onOpenChange={onOpenChange}
        title={t.deleteDialog.title(loan.name)}
        description={t.deleteDialog.description}
        confirmLabel={t.actions.delete}
        destructive
        onConfirm={async () => {
          await deleteLoan.mutateAsync()
          toast.success(t.deleted)
          navigate('/loans', { replace: true })
        }}
      />
    </>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd>{children}</dd>
    </>
  )
}
