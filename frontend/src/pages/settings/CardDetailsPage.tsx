import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { useCard, useCardAction, useCards, type Card } from '@/api/cards'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { CardStatusBadge } from '@/components/cards/CardStatusBadge'
import { BalanceEntries } from '@/components/cashflow/BalanceEntries'
import { formatDate, formatMonth } from '@/components/cards/format'
import { RevealableNumber } from '@/components/cards/RevealableNumber'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { cardTexts } from '@/components/cards/texts'
import { revolvingTexts } from '@/components/revolving/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { formatCents } from '@/lib/money'
import { formatBasisPoints } from '@/lib/percent'
import { PageHeader } from '@/pages/PageHeader'

type Dialog = 'block' | 'unblock' | 'expire' | 'archive' | null
const t = cardTexts
const b = cardPurchaseTexts.billing
const r = revolvingTexts

/**
 * Card details with reveal, status history and the block/unblock/expire/archive actions (FR-3, FR-5,
 * FR-7, FR-8), and the account balance with its starting balance and corrections (input cash flow FR-9).
 */
export function CardDetailsPage() {
  const { id } = useParams()
  const { data: card, isPending, isError } = useCard(id)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This card could not be loaded.</p>
  return <CardDetails card={card} />
}

function CardDetails({ card }: { card: Card }) {
  const navigate = useNavigate()
  const action = useCardAction(card.id)
  const [dialog, setDialog] = useState<Dialog>(null)
  const [replacementId, setReplacementId] = useState('')
  const [addReplacement, setAddReplacement] = useState(false)
  const { data: candidates = [] } = useCards('Default')

  const blocked = card.status === 'Blocked'
  const archived = card.status === 'Archived'
  const expiredByUser = card.expiredOn !== null
  const readOnly = blocked || archived || expiredByUser
  const isRevolving = card.type === 'Revolving'
  const debt = card.revolvingDebtCents ?? 0
  // A revolving card is replaced only by a revolving card, which takes over its debt (revolving FR-9).
  const replacementOptions = candidates.filter(
    (c) =>
      c.id !== card.id &&
      (c.type === 'Revolving') === isRevolving &&
      ['Active', 'ExpiringSoon', 'NotYetValid'].includes(c.status),
  )
  const replacementSelect = (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="replacement">
        {isRevolving ? r.dialogs.replacement : t.dialogs.replacement}
      </Label>
      <NativeSelect
        id="replacement"
        value={replacementId}
        disabled={addReplacement}
        onChange={(e) => setReplacementId(e.target.value)}
      >
        <option value="">{t.dialogs.noReplacement}</option>
        {replacementOptions.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </NativeSelect>
      {isRevolving && debt > 0 && (
        <p className="text-xs text-muted-foreground">{r.dialogs.replacementHint}</p>
      )}
    </div>
  )

  const close = (open: boolean) => !open && setDialog(null)

  return (
    <>
      <PageHeader title={card.name} />
      <div className="flex max-w-2xl flex-col gap-6">
        <div className="flex flex-wrap items-center gap-2">
          <CardStatusBadge status={card.status} />
          {card.isPrimary && <Badge variant="secondary">{t.primary}</Badge>}
        </div>

        <dl className="grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-[auto_1fr]">
          <Row label={t.fields.issuer}>{card.issuer.name}</Row>
          <Row label={t.fields.bank}>{card.bank.name}</Row>
          <Row label={t.fields.type}>{t.types[card.type]}</Row>
          {card.cardNumberMasked && (
            <Row label={t.fields.cardNumber}>
              <RevealableNumber cardId={card.id} kind="CardNumber" masked={card.cardNumberMasked} />
            </Row>
          )}
          {card.ibanMasked && (
            <Row label={t.fields.iban}>
              <RevealableNumber cardId={card.id} kind="Iban" masked={card.ibanMasked} />
            </Row>
          )}
          <Row label="Valid">
            {card.validFrom
              ? `${formatMonth(card.validFrom)} – ${formatMonth(card.validThrough)}`
              : `until ${formatMonth(card.validThrough)}`}
          </Row>
          {!isRevolving && (
            <Row label={t.fields.allowsInstallments}>{card.allowsInstallments ? 'Yes' : 'No'}</Row>
          )}
          {card.billing && (
            <>
              <Row label={b.terms}>
                <ul className="flex flex-col">
                  {card.billing.terms.map((terms) => (
                    <li key={terms.fromMonth ?? 'start'}>
                      {b.statementDay}: {b.day(terms.statementDay)} · {b.paymentDay}:{' '}
                      {b.day(terms.paymentDay)}{' '}
                      <span className="text-muted-foreground">
                        (
                        {terms.fromMonth
                          ? `${b.from} ${formatMonth(terms.fromMonth)}`
                          : b.fromStart}
                        )
                      </span>
                    </li>
                  ))}
                </ul>
              </Row>
              <Row label={b.payingAccount}>
                <Link className="underline" to={`/settings/cards/${card.billing.payingAccount.id}`}>
                  {card.billing.payingAccount.name}
                </Link>
                {card.billing.paysFromAccountDirectly && ` · ${b.paysFromAccountDirectly}`}
              </Row>
              <Row label={b.maxInstallments}>{card.billing.maxInstallments}</Row>
            </>
          )}
          {card.needsBillingDetails && (
            <Row label={b.title}>
              <span className="text-destructive">{cardPurchaseTexts.needsBillingDetails}</span>
            </Row>
          )}
          {card.revolving && (
            <>
              <Row label={r.card.terms}>
                <ul className="flex flex-col">
                  {card.revolving.terms.map((terms) => (
                    <li key={terms.fromMonth ?? 'start'}>
                      {r.card.termsLine(
                        formatBasisPoints(terms.minPaymentBasisPoints),
                        formatCents(terms.minPaymentAmountCents),
                      )}{' '}
                      · {r.card.interest} {formatBasisPoints(terms.interestBasisPoints)}
                      {terms.minPaymentInterestBasisPoints != null &&
                        ` (${r.card.interestPart} ${formatBasisPoints(terms.minPaymentInterestBasisPoints)})`}{' '}
                      · {r.card.billAndDue} {r.card.days(terms.billDay, terms.dueDay)}{' '}
                      <span className="text-muted-foreground">
                        (
                        {terms.fromMonth
                          ? `${b.from} ${formatMonth(terms.fromMonth)}`
                          : b.fromStart}
                        )
                      </span>
                    </li>
                  ))}
                </ul>
              </Row>
              <Row label={r.card.payingAccount}>
                <Link
                  className="underline"
                  to={`/settings/cards/${card.revolving.payingAccount.id}`}
                >
                  {card.revolving.payingAccount.name}
                </Link>
                {card.revolving.paysFromAccountDirectly && ` · ${r.form.paysFromAccountDirectly}`}
              </Row>
              <Row label={r.card.debt}>
                <Link className="underline" to={`/cards/revolving/${card.id}`}>
                  {formatCents(debt)}
                </Link>
              </Row>
            </>
          )}
          <Row label="Max debt limit">
            {card.debtLimitCents === null ? '—' : formatCents(card.debtLimitCents)}
          </Row>
          {card.blockedOn && (
            <Row label="Blocked on">
              {formatDate(card.blockedOn)}
              {card.blockReason && ` · ${card.blockReason}`}
            </Row>
          )}
          {card.expiredOn && (
            <Row label="Expired on">
              {formatDate(card.expiredOn)}
              {card.expiryReason && ` · ${card.expiryReason}`}
            </Row>
          )}
          {card.archivedOn && <Row label="Archived on">{formatDate(card.archivedOn)}</Row>}
          {card.replacedBy && (
            <Row label="Replaced by">
              <Link className="underline" to={`/settings/cards/${card.replacedBy.id}`}>
                {card.replacedBy.name}
              </Link>
            </Row>
          )}
          {card.replaces && (
            <Row label="Replaces">
              <Link className="underline" to={`/settings/cards/${card.replaces.id}`}>
                {card.replaces.name}
              </Link>
            </Row>
          )}
        </dl>

        {!archived && (
          <div className="flex flex-wrap gap-2">
            {!readOnly && (
              <Button asChild>
                <Link to={`/settings/cards/${card.id}/edit`}>{t.editCard}</Link>
              </Button>
            )}
            {card.allowsInstallments && (
              <Button asChild variant="outline">
                <Link to={`/cards/${card.id}`}>{cardPurchaseTexts.card.purchases}</Link>
              </Button>
            )}
            {blocked ? (
              <Button variant="outline" onClick={() => setDialog('unblock')}>
                {t.actions.unblock}
              </Button>
            ) : (
              <Button variant="outline" onClick={() => setDialog('block')}>
                {t.actions.block}
              </Button>
            )}
            {!expiredByUser && (
              <Button variant="outline" onClick={() => setDialog('expire')}>
                {t.actions.expire}
              </Button>
            )}
            <Button variant="destructive" onClick={() => setDialog('archive')}>
              {t.actions.archive}
            </Button>
          </div>
        )}
        {!isRevolving && <BalanceEntries accountId={card.id} readOnly={archived} />}
        <Link to="/settings/cards" className="text-sm text-muted-foreground underline">
          Back to {t.section.toLowerCase()}
        </Link>
      </div>

      <CardActionDialog
        open={dialog === 'block'}
        onOpenChange={close}
        title={t.dialogs.blockTitle(card.name)}
        description={t.dialogs.blockDescription}
        confirmLabel={t.actions.block}
        withReason
        removesPrimary={card.isPrimary}
        onConfirm={async (reason) => {
          await action.mutateAsync({
            action: 'block',
            reason,
            replacementCardId: isRevolving && replacementId !== '' ? replacementId : null,
          })
          toast.success(t.blocked)
        }}
      >
        {isRevolving && replacementSelect}
      </CardActionDialog>
      <CardActionDialog
        open={dialog === 'unblock'}
        onOpenChange={close}
        title={t.dialogs.unblockTitle(card.name)}
        description={t.dialogs.unblockDescription}
        confirmLabel={t.actions.unblock}
        onConfirm={async () => {
          await action.mutateAsync({ action: 'unblock' })
          toast.success(t.unblocked)
        }}
      />
      <CardActionDialog
        open={dialog === 'expire'}
        onOpenChange={close}
        title={t.dialogs.expireTitle(card.name)}
        description={t.dialogs.expireDescription}
        confirmLabel={t.actions.expire}
        withReason
        removesPrimary={card.isPrimary}
        destructive
        onConfirm={async (reason) => {
          await action.mutateAsync({
            action: 'expire',
            reason,
            replacementCardId: addReplacement || replacementId === '' ? null : replacementId,
          })
          toast.success(t.expired)
          // Expiring removes the primary mark, so pass it on for the replacement's prefill.
          if (addReplacement)
            navigate(`/settings/cards/new?replaces=${card.id}${card.isPrimary ? '&primary=1' : ''}`)
        }}
      >
        {replacementSelect}
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={addReplacement}
            onChange={(e) => setAddReplacement(e.target.checked)}
          />
          {t.dialogs.addReplacement}
        </label>
      </CardActionDialog>
      <CardActionDialog
        open={dialog === 'archive'}
        onOpenChange={close}
        title={t.dialogs.archiveTitle(card.name)}
        description={
          isRevolving && debt > 0
            ? r.dialogs.archiveWithDebt(formatCents(debt))
            : t.dialogs.archiveDescription
        }
        confirmLabel={t.actions.archive}
        canConfirm={!isRevolving || debt === 0}
        removesPrimary={card.isPrimary}
        destructive
        onConfirm={async () => {
          await action.mutateAsync({ action: 'archive' })
          toast.success(t.archived)
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
