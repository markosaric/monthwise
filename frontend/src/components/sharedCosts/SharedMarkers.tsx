import type { Share } from '@/api/sharedCosts'
import { Badge } from '@/components/ui/badge'
import { formatCents } from '@/lib/money'
import { sharedCostTexts } from './texts'

const t = sharedCostTexts.share

/** Marks a shared bill or loan payment with its participant and percentage, and a share set by hand (shared bills FR-7). */
export function ShareBadges({
  share,
  isShareChanged,
}: {
  share?: Share | null
  isShareChanged?: boolean
}) {
  return (
    <>
      {share && (
        <Badge variant="outline">{t.summary(share.participant.name, share.percentage)}</Badge>
      )}
      {isShareChanged && <Badge variant="secondary">{t.changedMarker}</Badge>}
    </>
  )
}

/** The household's own part under the amount of a shared bill or loan payment (FR-7). */
export function OwnPart({ cents }: { cents?: number | null }) {
  if (cents === null || cents === undefined) return null
  return <span className="block text-xs font-normal text-muted-foreground">{t.ownPart(cents)}</span>
}

/** The own part total of a list, shown only when something in it is shared (FR-7). */
export function OwnPartTotal({ cents, shown }: { cents: number; shown: boolean }) {
  if (!shown) return null
  return (
    <p className="flex justify-between text-sm">
      <span className="text-muted-foreground">{t.ownPartTotal}</span>
      <span className="font-medium tabular-nums">{formatCents(cents)}</span>
    </p>
  )
}
