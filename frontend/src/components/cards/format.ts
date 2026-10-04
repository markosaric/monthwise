/** "2027-02" → "02/2027", as printed on cards. */
export function formatMonth(yearMonth: string): string {
  const [year, month] = yearMonth.split('-')
  return `${month}/${year}`
}

/** ISO date "2026-10-04" → "04.10.2026". */
export function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-')
  return `${day}.${month}.${year}`
}
