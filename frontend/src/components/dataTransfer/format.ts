import { APP_LOCALE } from '@/lib/money'
import { appTexts } from '@/components/common/texts'

/** A moment as "31.10.2026, 08:15" in the user's time zone. */
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(APP_LOCALE, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** A moment's date as "31.10.2026". */
export function formatDay(iso: string): string {
  return new Date(iso).toLocaleDateString(APP_LOCALE, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

/** How long ago a moment was, in whole days: "today", "yesterday", "3 days ago" (or in Croatian). */
export function daysAgo(iso: string, now = new Date()): string {
  const start = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
  const days = Math.round((start(now) - start(new Date(iso))) / 86_400_000)
  if (days <= 0) return appTexts.time.today
  if (days === 1) return appTexts.time.yesterday
  return appTexts.time.daysAgo(days)
}
