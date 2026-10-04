import { useState } from 'react'
import { revealCardNumber, type SensitiveNumberKind } from '@/api/cards'
import { Button } from '@/components/ui/button'
import { cardTexts } from './texts'

/**
 * A masked number with its own Reveal button (FR-3). The full value is fetched only on request
 * and kept in this component's state, so it is gone when the user leaves the page.
 */
export function RevealableNumber({
  cardId,
  kind,
  masked,
}: {
  cardId: string
  kind: SensitiveNumberKind
  masked: string
}) {
  const [revealed, setRevealed] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [failed, setFailed] = useState(false)

  async function reveal() {
    setLoading(true)
    setFailed(false)
    try {
      setRevealed(await revealCardNumber(cardId, kind))
    } catch {
      setFailed(true)
    } finally {
      setLoading(false)
    }
  }

  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <span className="font-mono">{revealed ?? masked}</span>
      {revealed === null ? (
        <Button type="button" variant="ghost" size="xs" onClick={reveal} disabled={loading}>
          {cardTexts.actions.reveal}
        </Button>
      ) : (
        <Button type="button" variant="ghost" size="xs" onClick={() => setRevealed(null)}>
          {cardTexts.actions.hide}
        </Button>
      )}
      {failed && <span className="text-xs text-destructive">Could not load the number.</span>}
    </span>
  )
}
