import { useNavigate, useParams, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { useCard, useCreateCard, useUpdateCard, type Card } from '@/api/cards'
import { CardForm } from '@/components/cards/CardForm'
import { emptyCardFormValues, type CardFormValues } from '@/components/cards/cardFormModel'
import { cardTexts } from '@/components/cards/texts'
import { centsToEuroInput } from '@/lib/money'
import { PageHeader } from '@/pages/PageHeader'

function detailsOf(card: Card): Partial<CardFormValues> {
  return {
    name: card.name,
    issuerId: card.issuer.id,
    bankId: card.bank.id,
    type: card.type,
    isPrimary: card.isPrimary,
    allowsInstallments: card.type === 'Debit' && card.allowsInstallments,
    debtLimit: card.debtLimitCents === null ? '' : centsToEuroInput(card.debtLimitCents),
    ...(card.billing
      ? {
          statementDay: String(card.billing.statementDay),
          paymentDay: String(card.billing.paymentDay),
          payingAccountId: card.billing.payingAccount.id,
          paysFromAccountDirectly: card.billing.paysFromAccountDirectly,
          maxInstallments: String(
            card.billing.maxInstallments,
          ) as CardFormValues['maxInstallments'],
        }
      : {}),
  }
}

/**
 * Add a card (FR-2). With `?replaces={id}` the form is prefilled from that card and the new card
 * is linked as its replacement (FR-8).
 */
export function CardNewPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const replaces = searchParams.get('replaces') ?? undefined
  const { data: oldCard, isPending } = useCard(replaces)
  const createCard = useCreateCard()

  if (replaces && isPending) return <p className="text-sm text-muted-foreground">Loading…</p>

  const prefill = oldCard
    ? {
        ...detailsOf(oldCard),
        name: `${oldCard.name} (new)`.slice(0, 50),
        isPrimary: oldCard.isPrimary || searchParams.get('primary') === '1',
      }
    : {}

  return (
    <>
      <PageHeader
        title={cardTexts.addCard}
        description={oldCard ? `Replacement for ${oldCard.name}.` : undefined}
      />
      <CardForm
        defaultValues={emptyCardFormValues(prefill)}
        submitLabel={cardTexts.addCard}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          const card = await createCard.mutateAsync({ request, replaces })
          toast.success(cardTexts.saved)
          navigate(`/settings/cards/${card.id}`, { replace: true })
        }}
      />
    </>
  )
}

/** Edit a card (FR-4). */
export function CardEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: card, isPending, isError } = useCard(id)
  const updateCard = useUpdateCard(id!)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This card could not be loaded.</p>

  return (
    <>
      <PageHeader title={cardTexts.editCard} description={card.name} />
      <CardForm
        defaultValues={emptyCardFormValues({
          ...detailsOf(card),
          validFrom: card.validFrom ?? '',
          validThrough: card.validThrough,
          cardNumberMode: card.cardNumberMasked ? 'keep' : 'enter',
          ibanMode: card.ibanMasked ? 'keep' : 'enter',
        })}
        masked={{ cardNumber: card.cardNumberMasked, iban: card.ibanMasked }}
        submitLabel={cardTexts.actions.save}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await updateCard.mutateAsync(request)
          toast.success(cardTexts.saved)
          navigate(`/settings/cards/${card.id}`, { replace: true })
        }}
      />
    </>
  )
}
