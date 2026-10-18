import { TriangleAlert } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useSystemProblem } from './systemProblem'

/**
 * System status, shown only on a problem (FR-8): a red box at the bottom of the left menu (a red
 * icon when the menu is collapsed), or a slim red bar above the bottom navigation on phones.
 */
export function SystemStatusAlert({
  variant,
  collapsed = false,
}: {
  variant: 'menu' | 'bar'
  collapsed?: boolean
}) {
  const problem = useSystemProblem()
  if (!problem) return null

  if (variant === 'bar') {
    return (
      <div
        role="alert"
        className="flex items-center justify-center gap-2 bg-problem px-3 py-1 text-xs font-medium text-white"
      >
        <TriangleAlert className="size-3.5" aria-hidden />
        {problem}
      </div>
    )
  }
  return (
    <div
      role="alert"
      title={collapsed ? problem : undefined}
      className={cn(
        'mx-3 mb-3 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 text-sm text-problem',
        collapsed ? 'justify-center p-2' : 'px-3 py-2',
      )}
    >
      <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
      <span className={cn(collapsed && 'sr-only')}>
        <strong className="block font-semibold">System problem</strong>
        {problem}
      </span>
    </div>
  )
}
