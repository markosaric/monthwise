import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { useCard, useCardAction, useCards, type Card } from '@/api/cards'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { CardStatusBadge } from '@/components/cards/CardStatusBadge'
import { formatDate, formatMonth } from '@/components/cards/format'
import { RevealableNumber } from '@/components/cards/RevealableNumber'
import { cardTexts } from '@/components/cards/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { formatCents } from '@/lib/money'
import { PageHeader } from '@/pages/PageHeader'

type Dialog = 'block' | 'unblock' | 'expire' | 'archive' | null
const t = cardTexts

/** Card details with reveal, status history and the block/unblock/expire/archive actions (FR-3, FR-5, FR-7, FR-8). */
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
  const replacementOptions = candidates.filter(
    (c) => c.id !== card.id && ['Active', 'ExpiringSoon', 'NotYetValid'].includes(c.status),
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
          <Row label={t.fields.allowsInstallments}>{card.allowsInstallments ? 'Yes' : 'No'}</Row>
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
          await action.mutateAsync({ action: 'block', reason })
          toast.success(t.blocked)
        }}
      />
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
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="replacement">{t.dialogs.replacement}</Label>
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
        </div>
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
        description={t.dialogs.archiveDescription}
        confirmLabel={t.actions.archive}
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
