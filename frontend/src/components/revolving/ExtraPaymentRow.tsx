import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { useDeleteExtraPayment, type ExtraPayment } from '@/api/revolving'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { formatDate } from '@/components/cards/format'
import { ListRow } from '@/components/design/ListRow'
import { StatusChip } from '@/components/design/StatusChip'
import { Button } from '@/components/ui/button'
import { formatCents } from '@/lib/money'
import { revolvingTexts } from './texts'

const t = revolvingTexts.payment

/** An extra payment to a revolving card, always paid on its date (revolving FR-8, FR-11). */
export function ExtraPaymentRow({
  payment,
  showCard = true,
  readOnly = false,
}: {
  payment: ExtraPayment
  showCard?: boolean
  readOnly?: boolean
}) {
  const remove = useDeleteExtraPayment()
  const [confirming, setConfirming] = useState(false)
  const title = showCard ? payment.cardName : (payment.description ?? t.title)

  return (
    <ListRow
      label={`${t.title} ${payment.cardName} ${payment.date}`}
      title={
        showCard ? (
          <Link to={`/cards/revolving/${payment.cardId}`} className="hover:underline">
            {title}
          </Link>
        ) : (
          title
        )
      }
      date={formatDate(payment.date)}
      chips={<StatusChip status="done" label={revolvingTexts.month.extraPayment} />}
      amount={formatCents(payment.amountCents)}
      details={
        <span className="flex flex-col gap-0.5">
          <span>
            {payment.account.name}
            {showCard && payment.description && ` · ${payment.description}`}
          </span>
          {!readOnly && (
            <span className="flex flex-wrap gap-1 pt-1">
              <Button asChild size="xs" variant="ghost">
                <Link to={`/cards/revolving/${payment.cardId}/payments/${payment.id}/edit`}>
                  {revolvingTexts.payment.editTitle}
                </Link>
              </Button>
              <Button size="xs" variant="ghost" onClick={() => setConfirming(true)}>
                {t.delete}
              </Button>
            </span>
          )}
          <CardActionDialog
            open={confirming}
            onOpenChange={setConfirming}
            title={t.deleteTitle}
            description={t.deleteDescription}
            confirmLabel={t.delete}
            destructive
            onConfirm={async () => {
              await remove.mutateAsync(payment.id)
              toast.success(t.deleted)
            }}
          />
        </span>
      }
    />
  )
}
