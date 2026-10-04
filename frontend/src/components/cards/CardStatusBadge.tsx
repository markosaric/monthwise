import type { CardStatus } from '@/api/cards'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { cardTexts } from './texts'

const variants: Record<CardStatus, 'default' | 'secondary' | 'outline' | 'destructive'> = {
  Active: 'default',
  ExpiringSoon: 'outline',
  NotYetValid: 'secondary',
  Expired: 'destructive',
  Blocked: 'destructive',
  Archived: 'outline',
}

export function CardStatusBadge({ status }: { status: CardStatus }) {
  return (
    <Badge
      variant={variants[status]}
      className={cn(
        status === 'ExpiringSoon' && 'border-amber-500 text-amber-700 dark:text-amber-400',
        status === 'Archived' && 'text-muted-foreground',
      )}
    >
      {cardTexts.status[status]}
    </Badge>
  )
}
