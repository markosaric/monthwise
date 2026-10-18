import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { tint } from './colors'

/** A coloured square with an icon: tinted (default) or filled with the colour (`solid`). */
export function IconTile({
  icon: Icon,
  color,
  solid = false,
  size = 'md',
  className,
}: {
  icon: LucideIcon
  color: string
  solid?: boolean
  size?: 'sm' | 'md' | 'lg'
  className?: string
}) {
  return (
    <span
      aria-hidden
      className={cn(
        'grid shrink-0 place-items-center',
        size === 'sm' && 'size-7 rounded-md',
        size === 'md' && 'size-9 rounded-lg',
        size === 'lg' && 'size-11 rounded-xl',
        className,
      )}
      style={solid ? { background: color, color: 'white' } : { background: tint(color), color }}
    >
      <Icon className={size === 'sm' ? 'size-4' : 'size-5'} />
    </span>
  )
}
