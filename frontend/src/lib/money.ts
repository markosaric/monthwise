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
