import { APP_LOCALE } from './money'

/**
 * Parses a percentage typed by the user into basis points, e.g. "8,33" → 833, "12.75 %" → 1275,
 * "5" → 500. Accepts "," or "." as the decimal separator and at most two decimals. Returns null when
 * the text is not a percentage (negative numbers are not percentages here).
 */
export function parsePercentInput(input: string): number | null {
  const text = input.replace(/[\s%]/g, '').replace(',', '.')
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(text)
  if (!match) return null
  const basisPoints = Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0'))
  return Number.isSafeInteger(basisPoints) ? basisPoints : null
}

/** Basis points as an editable percentage, e.g. 833 → "8,33", 500 → "5". */
export function basisPointsToPercentInput(basisPoints: number): string {
  const whole = Math.trunc(basisPoints / 100)
  const rest = basisPoints % 100
  return rest === 0 ? String(whole) : `${whole},${String(rest).padStart(2, '0')}`
}

const percent = new Intl.NumberFormat(APP_LOCALE, {
  style: 'percent',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
})

/** Basis points for display, e.g. 833 → "8,33 %". */
export function formatBasisPoints(basisPoints: number): string {
  return percent.format(basisPoints / 10_000)
}
