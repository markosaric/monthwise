/** Change from `previous` to `current` in whole percent, or null when `previous` is 0. */
export function percentChange(current: number, previous: number): number | null {
  if (previous === 0) return null
  return Math.round(((current - previous) / Math.abs(previous)) * 100)
}
