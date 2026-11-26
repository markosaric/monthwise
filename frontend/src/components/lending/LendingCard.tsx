import { HandCoins, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import type { Lending, LendingState, Repayment } from '@/api/lending'
import { formatDate } from '@/components/cards/format'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { IconTile } from '@/components/design/IconTile'
import { ProgressBar } from '@/components/design/ProgressBar'
import { StatusChip, type ChipStatus } from '@/components/design/StatusChip'
import { Button } from '@/components/ui/button'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { DeleteLendingDialog, DeleteRepaymentDialog, RepaymentDialog } from './RepaymentDialogs'
import { lendingTexts } from './texts'

const t = lendingTexts

const chipOf: Record<LendingState, ChipStatus> = {
  Open: 'expected',
  Overdue: 'overdue',
  Returned: 'done',
}

/** A lending's state chip: Open, Overdue (red) or Returned (FR-1, FR-5). */
export function LendingStateChip({ state }: { state: LendingState }) {
  return <StatusChip status={chipOf[state]} label={t.states[state]} />
}

type Dialog =
  | { kind: 'repayment'; repayment?: Repayment }
  | { kind: 'deleteRepayment'; repayment: Repayment }
  | { kind: 'delete' }

/**
 * One lending on the Lending screen (FR-1): borrower, state, outstanding amount, dates, account,
 * how much is returned, the repayments and the actions. An overdue lending is highlighted (FR-5).
 */
export function LendingCard({ lending }: { lending: Lending }) {
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const overdue = lending.state === 'Overdue'
  const returned = lending.state === 'Returned'
  const color = overdue ? meaningColors.problem : sectionColors.lending
  const close = (open: boolean) => !open && setDialog(null)

  return (
    <li
      data-overdue={overdue || undefined}
      className={cn(
        'flex flex-col gap-3 rounded-xl bg-card p-4 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8',
        overdue && 'bg-red-50/40 ring-red-200',
      )}
    >
      <div className="flex items-start gap-3">
        <IconTile icon={HandCoins} color={color} solid />
        <div className="flex min-w-0 flex-1 flex-col">
          <span className="flex flex-wrap items-center gap-2">
            <span className="font-semibold">{lending.borrower.name}</span>
            <LendingStateChip state={lending.state} />
          </span>
          <span className="text-sm text-muted-foreground">
            {t.labels.lentOn(formatDate(lending.lendDate))} · {lending.fromAccount.name}
            {!returned && ` · ${t.labels.dueBack(formatDate(lending.expectedReturnDate))}`}
          </span>
          {lending.description && (
            <span className="text-sm text-muted-foreground">{lending.description}</span>
          )}
        </div>
        <div className="text-right">
          <span className="block font-heading text-lg font-bold tabular-nums">
            {formatCents(returned ? lending.amountCents : lending.outstandingCents)}
          </span>
          <span className="text-xs text-muted-foreground">
            {returned ? t.labels.returned : t.labels.outstanding}
          </span>
        </div>
      </div>

      <ProgressBar
        label={`${lending.borrower.name}: ${t.labels.returnedOf(formatCents(lending.returnedCents), formatCents(lending.amountCents))}`}
        value={lending.returnedCents}
        max={lending.amountCents}
        color={color}
      />
      <span className="text-xs text-muted-foreground">
        {t.labels.returnedOf(formatCents(lending.returnedCents), formatCents(lending.amountCents))}
      </span>

      <div>
        <h3 className="text-sm font-medium">{t.labels.repayments}</h3>
        {lending.repayments.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t.labels.noRepayments}</p>
        ) : (
          <ul className="divide-y">
            {lending.repayments.map((repayment) => (
              <li key={repayment.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-1.5">
                <span className="text-sm">{formatDate(repayment.receivedDate)}</span>
                <span className="min-w-0 flex-1 text-sm text-muted-foreground">
                  {repayment.toAccount.name}
                  {repayment.note && ` · ${repayment.note}`}
                </span>
                <span className="text-sm font-semibold tabular-nums">
                  {formatCents(repayment.amountCents)}
                </span>
                <span className="flex gap-1">
                  <Button
                    size="icon-sm"
                    variant="ghost"
                    aria-label={`${t.actions.editRepayment}: ${formatDate(repayment.receivedDate)}`}
                    onClick={() => setDialog({ kind: 'repayment', repayment })}
                  >
                    <Pencil className="size-4" aria-hidden />
                  </Button>
                  <Button
                    size="icon-sm"
                    variant="ghost"
                    aria-label={`${t.actions.deleteRepayment}: ${formatDate(repayment.receivedDate)}`}
                    onClick={() => setDialog({ kind: 'deleteRepayment', repayment })}
                  >
                    <Trash2 className="size-4" aria-hidden />
                  </Button>
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="flex flex-wrap gap-2">
        {!returned && (
          <Button size="sm" onClick={() => setDialog({ kind: 'repayment' })}>
            <Plus className="size-4" aria-hidden />
            {t.actions.recordRepayment}
          </Button>
        )}
        <Button asChild size="sm" variant="outline">
          <Link
            to={`/lending/${lending.id}/edit`}
            aria-label={`${t.edit}: ${lending.borrower.name}`}
          >
            <Pencil className="size-4" aria-hidden />
            {t.actions.edit}
          </Link>
        </Button>
        <Button size="sm" variant="ghost" onClick={() => setDialog({ kind: 'delete' })}>
          <Trash2 className="size-4" aria-hidden />
          {t.actions.delete}
        </Button>
      </div>

      {dialog?.kind === 'repayment' && (
        <RepaymentDialog lending={lending} repayment={dialog.repayment} onOpenChange={close} />
      )}
      {dialog?.kind === 'deleteRepayment' && (
        <DeleteRepaymentDialog
          lending={lending}
          repayment={dialog.repayment}
          onOpenChange={close}
        />
      )}
      <DeleteLendingDialog
        lending={lending}
        open={dialog?.kind === 'delete'}
        onOpenChange={close}
      />
    </li>
  )
}
