import { ChevronDown } from 'lucide-react'
import { useId, useState, type ReactNode } from 'react'
import { Card, CardContent } from '@/components/ui/card'
import { cn } from '@/lib/utils'

/** A card whose content opens and closes with its title (FR-17: grouped lists collapsed by default). */
export function CollapsibleCard({
  title,
  aside,
  defaultOpen = false,
  children,
}: {
  title: string
  aside?: ReactNode
  defaultOpen?: boolean
  children: ReactNode
}) {
  const [open, setOpen] = useState(defaultOpen)
  const contentId = useId()
  return (
    <Card className="gap-0 py-0">
      <h2>
        <button
          type="button"
          onClick={() => setOpen(!open)}
          aria-expanded={open}
          aria-controls={contentId}
          className="flex w-full items-center gap-3 px-4 py-3 text-left text-base font-semibold hover:bg-muted/50"
        >
          <span className="flex-1">{title}</span>
          {aside && <span className="font-sans text-sm font-medium tabular-nums">{aside}</span>}
          <ChevronDown
            aria-hidden
            className={cn(
              'size-4 text-muted-foreground transition-transform',
              !open && '-rotate-90',
            )}
          />
        </button>
      </h2>
      <CardContent id={contentId} hidden={!open} className="pb-4">
        {children}
      </CardContent>
    </Card>
  )
}
