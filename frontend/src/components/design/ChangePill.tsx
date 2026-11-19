import { cn } from '@/lib/utils'
import { percentChange } from './percent'
import { appTexts } from '@/components/common/texts'

/**
 * The change against the previous period in whole percent (FR-2), green when the change is good and
 * red when it is bad. Not shown when there is no previous value to compare with.
 */
export function ChangePill({
  current,
  previous,
  higherIsBetter,
}: {
  current: number
  previous: number
  higherIsBetter: boolean
}) {
  const change = percentChange(current, previous)
  if (change === null) return null
  const up = change > 0
  const good = change === 0 || up === higherIsBetter
  return (
    <span
      className={cn(
        'inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums',
        good ? 'bg-teal-100 text-teal-800' : 'bg-red-100 text-red-700',
      )}
      title={appTexts.common.comparedWithLastMonth}
    >
      <span aria-hidden>{change === 0 ? '=' : up ? '▲' : '▼'}</span>
      <span className="sr-only">
        {up
          ? appTexts.common.changeUp
          : change === 0
            ? appTexts.common.changeUnchanged
            : appTexts.common.changeDown}{' '}
      </span>
      {Math.abs(change)}%
    </span>
  )
}
