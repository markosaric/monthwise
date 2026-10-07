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
 * Parses an amount typed in euros into cents, e.g. "1.234,56" → 123456, "1234.5" → 123450,
 * "250" → 25000. Accepts an optional "€" and spaces. Returns null when the text is not an amount
 * (negative amounts and more than two decimals are not amounts here).
 */
export function parseEuroInput(input: string): number | null {
  let text = input.replace(/[\s€]/g, '')
  if (text === '') return null

  if (text.includes(',')) {
    // de-DE style: "." groups thousands, "," is the decimal separator.
    text = text.replace(/\./g, '').replace(',', '.')
  } else if (!/^\d+\.\d{1,2}$/.test(text)) {
    // A single "." with one or two digits after it is a decimal point; otherwise it groups thousands.
    text = text.replace(/\./g, '')
  }

  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(text)
  if (!match) return null
  const cents = Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0'))
  return Number.isSafeInteger(cents) ? cents : null
}

/** Cents as an editable euro value, e.g. 123456 → "1234,56". */
export function centsToEuroInput(cents: number): string {
  const euros = Math.trunc(cents / 100)
  const rest = Math.abs(cents % 100)
  return rest === 0 ? String(euros) : `${euros},${String(rest).padStart(2, '0')}`
}

/** Parses a balance that may be negative, e.g. "-250,00" → -25000. */
export function parseSignedEuroInput(text: string): number | null {
  const trimmed = text.trim()
  const negative = trimmed.startsWith('-')
  const cents = parseEuroInput(negative ? trimmed.slice(1) : trimmed)
  return cents === null ? null : negative ? -cents : cents
}
