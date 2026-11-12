/** Locale used to display amounts. Amounts are always EUR and always handled as integer cents. */
export const APP_LOCALE = 'de-DE'

const eur = new Intl.NumberFormat(APP_LOCALE, { style: 'currency', currency: 'EUR' })

/**
 * Formats an amount in cents for display, e.g. 123456 → "1.234,56 €".
 * The only place cents become a fractional number is here, for display.
 */
export function formatCents(cents: number): string {
  if (!Number.isSafeInteger(cents)) {
    throw new RangeError(`Amount must be a whole number of cents, got ${cents}`)
  }
  return eur.format(cents / 100)
}

/**
 * Text shapes that are amounts, after spaces and "€" are removed (amount-input.md FR-1). With a
 * comma: "1234,5" or "1.234,56" (dots group thousands in threes). Without a comma: "250",
 * "1234.5" (a dot before one or two digits is a decimal point) or "1.234.567" (two or more
 * thousands dots). A single dot before exactly three digits ("1.234") matches none of them,
 * because it could mean either, and is refused (FR-2).
 */
const AMOUNT_SHAPES = [
  /^(?<euros>\d+),(?<cents>\d{1,2})$/,
  /^(?<euros>\d{1,3}(?:\.\d{3})+),(?<cents>\d{1,2})$/,
  /^(?<euros>\d+)$/,
  /^(?<euros>\d+)\.(?<cents>\d{1,2})$/,
  /^(?<euros>\d{1,3}(?:\.\d{3}){2,})$/,
]

/**
 * Parses an amount typed in euros into cents, e.g. "1.234,56" → 123456, "1234.5" → 123450,
 * "250" → 25000. Accepts an optional "€" and spaces. Returns null when the text is not an amount:
 * negative amounts, more than two decimals, badly placed dots, and a single dot before exactly
 * three digits ("1.234"), which could be a thousands dot or a decimal point.
 */
export function parseEuroInput(input: string): number | null {
  const text = input.replace(/[\s€]/g, '')
  for (const shape of AMOUNT_SHAPES) {
    const groups = shape.exec(text)?.groups
    if (!groups) continue
    const { euros = '', cents = '' } = groups
    const total = Number(euros.replace(/\./g, '')) * 100 + Number(cents.padEnd(2, '0'))
    return Number.isSafeInteger(total) ? total : null
  }
  return null
}

/** Cents as an editable euro value, e.g. 123456 → "1234,56", -50 → "-0,50". */
export function centsToEuroInput(cents: number): string {
  const sign = cents < 0 ? '-' : ''
  const absolute = Math.abs(cents)
  const euros = Math.trunc(absolute / 100)
  const rest = absolute % 100
  return rest === 0 ? `${sign}${euros}` : `${sign}${euros},${String(rest).padStart(2, '0')}`
}

/** Parses a balance that may be negative, e.g. "-250,00" → -25000. */
export function parseSignedEuroInput(text: string): number | null {
  const trimmed = text.trim()
  const negative = trimmed.startsWith('-')
  const cents = parseEuroInput(negative ? trimmed.slice(1) : trimmed)
  return cents === null ? null : negative ? -cents : cents
}
