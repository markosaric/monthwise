import { APP_LOCALE } from './money'

/** A local calendar date as ISO "yyyy-MM-dd" (no time zone shift). */
export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function todayIso(): string {
  return toIsoDate(new Date())
}

/** "2026-10-06" → "2026-10". */
export function monthOf(isoDate: string): string {
  return isoDate.slice(0, 7)
}

export function currentMonth(): string {
  return monthOf(todayIso())
}

export function isYearMonth(value: string | undefined): value is string {
  return value !== undefined && /^\d{4}-(0[1-9]|1[0-2])$/.test(value)
}

/** "2026-10" → [2026, 10]. */
function parts(yearMonth: string): [number, number] {
  return [Number(yearMonth.slice(0, 4)), Number(yearMonth.slice(5, 7))]
}

/** "2026-10" plus 3 → "2027-01". */
export function addMonths(yearMonth: string, months: number): string {
  const [year, month] = parts(yearMonth)
  const index = year * 12 + (month - 1) + months
  return `${Math.floor(index / 12)}-${String((index % 12) + 1).padStart(2, '0')}`
}

export function firstDayOf(yearMonth: string): string {
  return `${yearMonth}-01`
}

export function lastDayOf(yearMonth: string): string {
  const [year, month] = parts(yearMonth)
  return `${yearMonth}-${String(new Date(year, month, 0).getDate()).padStart(2, '0')}`
}

/** "2026-10" → "October 2026". */
export function formatYearMonth(yearMonth: string, style: 'long' | 'short' = 'long'): string {
  const [year, month] = parts(yearMonth)
  return new Intl.DateTimeFormat('en-GB', { month: style, year: 'numeric' }).format(
    new Date(year, month - 1, 1),
  )
}

/** "2026-10-06" → "06.10." for chart axes. */
export function formatDayShort(isoDate: string): string {
  const [, month, day] = isoDate.split('-')
  return `${day}.${month}.`
}

export { APP_LOCALE }

/**
 * Formats what was typed or pasted into a card month field as "MM/YY": keeps up to 4 digits and
 * inserts the slash once a third digit is there ("0228" → "02/28", "022" → "02/2", "02" → "02").
 */
export function formatCardMonthInput(text: string): string {
  const digits = text.replace(/\D/g, '').slice(0, 4)
  return digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits
}

const cardMonth = /^(0[1-9]|1[0-2])\/(\d{2})$/

/** "02/28" → "2028-02", or null when the text is not a valid "MM/YY". */
export function cardMonthToYearMonth(text: string): string | null {
  const match = cardMonth.exec(text)
  return match ? `20${match[2]}-${match[1]}` : null
}

/** "2028-02" → "02/28". */
export function yearMonthToCardMonth(yearMonth: string): string {
  return `${yearMonth.slice(5, 7)}/${yearMonth.slice(2, 4)}`
}

/** The message for a date field that is filled in but is not a complete, existing date. */
export const dateEntryMessage = 'Enter the date as DD.MM.YYYY, e.g. 16.11.2026.'

/**
 * Formats what was typed or pasted into a date field as "DD.MM.YYYY": keeps up to 8 digits and
 * inserts a dot once the next part has started ("16112026" → "16.11.2026", "161" → "16.1",
 * "16" → "16"), so deleting back over a dot works.
 */
export function formatDateInput(text: string): string {
  const digits = text.replace(/\D/g, '').slice(0, 8)
  if (digits.length <= 2) return digits
  if (digits.length <= 4) return `${digits.slice(0, 2)}.${digits.slice(2)}`
  return `${digits.slice(0, 2)}.${digits.slice(2, 4)}.${digits.slice(4)}`
}

const dateEntry = /^(\d{2})\.(\d{2})\.(\d{4})$/

/** "16.11.2026" → "2026-11-16", or null when the text is not a complete, existing date. */
export function dateInputToIso(text: string): string | null {
  const match = dateEntry.exec(text)
  if (!match) return null
  const [, day, month, year] = match
  const d = Number(day)
  const m = Number(month)
  const y = Number(year)
  if (y < 1 || m < 1 || m > 12 || d < 1) return null
  const leap = (y % 4 === 0 && y % 100 !== 0) || y % 400 === 0
  const daysInMonth = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][m - 1] ?? 0
  if (d > daysInMonth) return null
  return `${year}-${month}-${day}`
}

/** "2026-11-16" → "16.11.2026"; anything that is not an ISO date is returned unchanged. */
export function isoToDateInput(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  return match ? `${match[3]}.${match[2]}.${match[1]}` : value
}

/** A date form value that was filled in but is not a complete, existing date (Appendix 2). */
export function isInvalidDateEntry(value: string): boolean {
  return value !== '' && !/^\d{4}-\d{2}-\d{2}$/.test(value)
}
