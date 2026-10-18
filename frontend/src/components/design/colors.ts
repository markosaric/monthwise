/**
 * Colours of the design refresh (FR-1, FR-7). Each main section has its own colour, and some colours
 * always mean the same thing: money in green, money out orange, expenses amber, problems red.
 * Values are CSS variables defined in `index.css`.
 */
export const sectionColors = {
  dashboard: 'var(--section-dashboard)',
  month: 'var(--section-month)',
  cashflow: 'var(--section-cashflow)',
  cards: 'var(--section-cards)',
  loans: 'var(--section-loans)',
  shared: 'var(--section-shared)',
  savings: 'var(--section-savings)',
  settings: 'var(--section-settings)',
} as const

export type Section = keyof typeof sectionColors

export const meaningColors = {
  moneyIn: 'var(--money-in)',
  moneyOut: 'var(--money-out)',
  moneyInText: 'var(--money-in-text)',
  moneyOutText: 'var(--money-out-text)',
  expense: 'var(--expense)',
  highlight: 'var(--highlight)',
  problem: 'var(--problem)',
} as const

/** A light tint of a colour, for icon backgrounds and the expected part of a bar. */
export function tint(color: string, percent = 16): string {
  return `color-mix(in srgb, ${color} ${percent}%, white)`
}

/** Distinct colours for categories in ring charts, used in order and repeated when there are more. */
export const categoryPalette = [
  '#0d9488',
  '#3b82f6',
  '#8b5cf6',
  '#db2777',
  '#ea580c',
  '#ca8a04',
  '#16a34a',
  '#64748b',
] as const

export function categoryColor(index: number): string {
  return categoryPalette[index % categoryPalette.length]!
}
