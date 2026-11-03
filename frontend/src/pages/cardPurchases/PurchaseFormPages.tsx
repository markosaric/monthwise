import { useNavigate, useParams, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { warnIfOverLimit } from '@/components/revolving/overLimit'
import {
  useCardPurchase,
  useCreateCardPurchase,
  useInstallmentCards,
  useUpdateCardPurchase,
} from '@/api/cardPurchases'
import { useRevolvingCards, type RevolvingCards } from '@/api/revolving'
import { PurchaseForm, type PurchaseCardOption } from '@/components/cardPurchases/PurchaseForm'
import {
  emptyPurchaseFormValues,
  purchaseFormValuesOf,
} from '@/components/cardPurchases/purchaseFormModel'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { todayIso } from '@/lib/dates'
import { useReturnTo } from '@/lib/returnTo'
import { PageHeader } from '@/pages/PageHeader'

const t = cardPurchaseTexts

/** Revolving cards as purchase card options; `keep` stays listed even when it cannot be used. */
function revolvingOptions(data: RevolvingCards | undefined, keep?: string): PurchaseCardOption[] {
  return (data?.cards ?? [])
    .filter((c) => c.id === keep || (c.canBeUsed && !c.needsAccount))
    .map((c) => ({ id: c.id, name: c.name, maxInstallments: null, isRevolving: true }))
}

/** Record a purchase (FR-3). With `?card={id}` that card is chosen; with only one card it is chosen too. */
export function PurchaseNewPage() {
  const navigate = useNavigate()
  const returnTo = useReturnTo()
  const [searchParams] = useSearchParams()
  const { data: cards, isPending, isError } = useInstallmentCards()
  const revolving = useRevolvingCards()
  const create = useCreateCardPurchase()

  if (isPending || revolving.isPending)
    return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">Cards could not be loaded.</p>

  const usable: PurchaseCardOption[] = [
    ...cards.filter((c) => c.canBeUsed && !c.needsBillingDetails),
    ...revolvingOptions(revolving.data),
  ]
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
        onCancel={() => (returnTo ? navigate(returnTo) : navigate(-1))}
        onSubmit={async (request) => {
          const details = await create.mutateAsync(request)
          toast.success(t.saved)
          await warnIfOverLimit(request.cardId)
          navigate(returnTo ?? `/cards/purchases/${details.purchase.id}`, { replace: true })
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
  const { data: revolving } = useRevolvingCards()
  const update = useUpdateCardPurchase(id!)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This purchase could not be loaded.</p>

  const { purchase } = details
  // The purchase's own card stays available even when it can no longer be used for new purchases.
  const options: PurchaseCardOption[] = [
    ...cards.filter((c) => c.id === purchase.card.id || (c.canBeUsed && !c.needsBillingDetails)),
    ...revolvingOptions(revolving, purchase.card.id),
  ]

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
          await warnIfOverLimit(request.cardId)
          navigate(`/cards/purchases/${purchase.id}`, { replace: true })
        }}
      />
    </>
  )
}
