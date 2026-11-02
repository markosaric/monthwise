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
