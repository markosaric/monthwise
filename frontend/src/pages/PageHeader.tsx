import { cn } from '@/lib/utils'

/**
 * A screen's heading in the display font (design refresh FR-4), with an optional short description.
 * Screens that space their parts themselves pass `flush` to drop the margin below.
 */
export function PageHeader({
  title,
  description,
  flush = false,
}: {
  title: string
  description?: string
  flush?: boolean
}) {
  return (
    <header className={cn(!flush && 'mb-6')}>
      <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
      {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
    </header>
  )
}
