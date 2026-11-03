import { useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import {
  useCreateExtraPayment,
  useRevolvingCard,
  useRevolvingCards,
  useUpdateExtraPayment,
} from '@/api/revolving'
import { ExtraPaymentForm } from '@/components/revolving/ExtraPaymentForm'
import { revolvingTexts } from '@/components/revolving/texts'
import { todayIso } from '@/lib/dates'
import { useReturnTo } from '@/lib/returnTo'
import { centsToEuroInput } from '@/lib/money'
import { PageHeader } from '@/pages/PageHeader'

const t = revolvingTexts.payment

/**
 * A new extra payment (revolving FR-8): for one card (`/cards/revolving/:cardId/pay`), or from the
 * quick action (`/pay-revolving`) with a choice of the usable revolving cards with debt.
 */
export function ExtraPaymentNewPage() {
  const { cardId } = useParams()
  const navigate = useNavigate()
  const returnTo = useReturnTo()
  const { data, isPending, isError } = useRevolvingCards()
  const create = useCreateExtraPayment()

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">Cards could not be loaded.</p>

  const cards = cardId
    ? data.cards.filter((c) => c.id === cardId)
    : data.cards.filter((c) => c.debtCents > 0 && c.status !== 'Archived')
  const only = cards.length === 1 ? cards[0]! : undefined

  return (
    <>
      <PageHeader
        title={cardId ? t.title : t.quickTitle}
        description={only ? only.name : t.description}
      />
      {cards.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t.noCards}</p>
      ) : (
        <ExtraPaymentForm
          cards={cards}
          defaultValues={{
            cardId: only?.id ?? '',
            accountId: only?.terms?.payingAccount.id ?? '',
            amount: '',
            date: todayIso(),
            description: '',
          }}
          submitLabel={t.save}
          onCancel={() => navigate(-1)}
          onSubmit={async (id, request) => {
            await create.mutateAsync({ cardId: id, request })
            toast.success(t.saved)
            navigate(returnTo ?? `/cards/revolving/${id}`, { replace: true })
          }}
        />
      )}
    </>
  )
}

/** Edit an extra payment (revolving FR-8). */
export function ExtraPaymentEditPage() {
  const { cardId, paymentId } = useParams()
  const navigate = useNavigate()
  const { data, isPending, isError } = useRevolvingCard(cardId)
  const update = useUpdateExtraPayment(paymentId!)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  const payment = data?.extraPayments.find((p) => p.id === paymentId)
  if (isError || !payment)
    return <p className="text-sm text-destructive">This payment could not be loaded.</p>

  return (
    <>
      <PageHeader title={t.editTitle} description={data.card.name} />
      <ExtraPaymentForm
        cards={[data.card]}
        editing
        defaultValues={{
          cardId: payment.cardId,
          accountId: payment.account.id,
          amount: centsToEuroInput(payment.amountCents),
          date: payment.date,
          description: payment.description ?? '',
        }}
        submitLabel={revolvingTexts.entry.save}
        onCancel={() => navigate(-1)}
        onSubmit={async (_, request) => {
          await update.mutateAsync(request)
          toast.success(t.saved)
          navigate(`/cards/revolving/${payment.cardId}`, { replace: true })
        }}
      />
    </>
  )
}
