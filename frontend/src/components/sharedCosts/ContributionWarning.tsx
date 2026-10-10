import type { Contribution } from '@/api/sharedCosts'
import { partOf, type ShareFormValues } from './shareModel'
import { sharedCostTexts } from './texts'

const t = sharedCostTexts.contribution

/**
 * Points out received contributions that would no longer match the part after a change (shared bills
 * FR-6): they are never changed automatically, so the user settles the difference with the participant.
 * With `amountCents` and `share` the new part is worked out here; otherwise each contribution's
 * current part is used.
 */
export function ContributionWarning({
  contributions,
  amountCents,
  share,
}: {
  contributions: Contribution[]
  amountCents?: number | null
  share?: ShareFormValues
}) {
  const differing = contributions
    .filter((c) => c.isReceived)
    .map((c) => ({
      received: c.amountCents,
      part:
        share === undefined
          ? c.partCents
          : share.enabled && amountCents
            ? partOf(amountCents, share.shares, share.participantShares)
            : 0,
    }))
    .filter((c) => c.received !== c.part)
  if (differing.length === 0) return null
  return (
    <div role="status" className="rounded-lg border border-amber-500/50 p-3 text-sm">
      {differing.length === 1 && <p>{t.differs(differing[0]!.received, differing[0]!.part)}</p>}
      <p className="text-muted-foreground">{t.receivedChanges(differing.length)}</p>
    </div>
  )
}
