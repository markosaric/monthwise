import { toast } from 'sonner'
import { api } from '@/api/client'
import { formatCents } from '@/lib/money'
import { revolvingTexts } from './texts'

/**
 * After a charge is saved on a card: when it is a revolving card now over its limit, warns once
 * (revolving FR-6: the charge is kept, the limit is only a warning). Never fails the save.
 */
export async function warnIfOverLimit(cardId: string | null | undefined): Promise<void> {
  if (!cardId) return
  try {
    const { data } = await api.GET('/api/revolving-cards')
    const card = data?.cards.find((c) => c.id === cardId)
    if (card?.isOverLimit) {
      toast.warning(
        revolvingTexts.overLimitWarning(card.name, formatCents(-card.remainingLimitCents)),
      )
    }
  } catch {
    // Only a warning: the charge is saved either way.
  }
}
