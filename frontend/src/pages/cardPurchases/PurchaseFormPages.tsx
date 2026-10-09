import { useNavigate, useParams, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import {
  useCardPurchase,
  useCreateCardPurchase,
  useInstallmentCards,
  useUpdateCardPurchase,
} from '@/api/cardPurchases'
import { PurchaseForm } from '@/components/cardPurchases/PurchaseForm'
import {
  emptyPurchaseFormValues,
  purchaseFormValuesOf,
} from '@/components/cardPurchases/purchaseFormModel'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { todayIso } from '@/lib/dates'
import { PageHeader } from '@/pages/PageHeader'

const t = cardPurchaseTexts

/** Record a purchase (FR-3). With `?card={id}` that card is chosen; with only one card it is chosen too. */
export function PurchaseNewPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { data: cards, isPending, isError } = useInstallmentCards()
  const create = useCreateCardPurchase()

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">Cards could not be loaded.</p>

  const usable = cards.filter((c) => c.canBeUsed && !c.needsBillingDetails)
  const requested = searchParams.get('card')
  const cardId = usable.some((c) => c.id === requested)
    ? requested!
    : usable.length === 1
      ? usable[0]!.id
      : ''

  return (
    <>
      <PageHeader title={t.addPurchase} />
      <PurchaseForm
        cards={usable}
        defaultValues={emptyPurchaseFormValues(cardId, todayIso())}
        submitLabel={t.addPurchase}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          const details = await create.mutateAsync(request)
          toast.success(t.saved)
          navigate(`/cards/purchases/${details.purchase.id}`, { replace: true })
        }}
      />
    </>
  )
}

/** Edit a purchase that is not reversed (FR-6); warns when charged card payments will change. */
export function PurchaseEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: details, isPending, isError } = useCardPurchase(id)
  const { data: cards = [] } = useInstallmentCards()
  const update = useUpdateCardPurchase(id!)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This purchase could not be loaded.</p>

  const { purchase } = details
  // The purchase's own card stays available even when it can no longer be used for new purchases.
  const options = cards.filter(
    (c) => c.id === purchase.card.id || (c.canBeUsed && !c.needsBillingDetails),
  )

  return (
    <>
      <PageHeader title={t.editPurchase} description={purchase.description} />
      <PurchaseForm
        cards={options}
        defaultValues={purchaseFormValuesOf(details)}
        submitLabel={t.actions.save}
        warning={purchase.hasChargedInstallments ? t.dialogs.editWarning : undefined}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await update.mutateAsync(request)
          toast.success(t.saved)
          navigate(`/cards/purchases/${purchase.id}`, { replace: true })
        }}
      />
    </>
  )
}
