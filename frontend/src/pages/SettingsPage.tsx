import {
  CreditCard,
  Landmark,
  Repeat,
  ShoppingBag,
  Tags,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { Link } from 'react-router'
import { useInputCategories, usePayers } from '@/api/cashFlow'
import { useHealth } from '@/api/health'
import { useCards } from '@/api/cards'
import { useExpenseCategories, useRecurringBills } from '@/api/expenses'
import { useLoanCategories } from '@/api/loans'
import { cardTexts } from '@/components/cards/texts'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { IconTile } from '@/components/design/IconTile'
import { expenseTexts } from '@/components/expenses/texts'
import { loanTexts } from '@/components/loans/texts'
import { PageHeader } from './PageHeader'

interface Tile {
  to: string
  icon: LucideIcon
  color: string
  title: string
  description: string
  count?: string
}

function counted(count: number | undefined, one: string, many: string): string | undefined {
  return count === undefined ? undefined : `${count} ${count === 1 ? one : many}`
}

/**
 * Settings as tiles grouped into Accounts, Money in and Money out, each with a coloured icon and a
 * count of what it holds (design refresh FR-20).
 */
export function SettingsPage() {
  const { data: cards } = useCards('Default')
  const { data: payers } = usePayers()
  const { data: inputCategories } = useInputCategories()
  const { data: expenseCategories } = useExpenseCategories()
  const { data: loanCategories } = useLoanCategories()
  const { data: recurringBills } = useRecurringBills()
  const { data: health } = useHealth()

  const groups: { title: string; tiles: Tile[] }[] = [
    {
      title: 'Accounts',
      tiles: [
        {
          to: '/settings/cards',
          icon: CreditCard,
          color: sectionColors.cards,
          title: cardTexts.section,
          description: cardTexts.sectionDescription,
          count: counted(cards?.length, 'card', 'cards'),
        },
      ],
    },
    {
      title: 'Money in',
      tiles: [
        {
          to: '/settings/payers',
          icon: Users,
          color: meaningColors.moneyIn,
          title: cashFlowTexts.payers.section,
          description: cashFlowTexts.payers.description,
          count: counted(payers?.length, 'payer', 'payers'),
        },
        {
          to: '/settings/input-categories',
          icon: Tags,
          color: meaningColors.moneyIn,
          title: cashFlowTexts.categories.section,
          description: cashFlowTexts.categories.description,
          count: counted(inputCategories?.length, 'category', 'categories'),
        },
      ],
    },
    {
      title: 'Money out',
      tiles: [
        {
          to: '/settings/expense-categories',
          icon: ShoppingBag,
          color: meaningColors.expense,
          title: expenseTexts.categories.section,
          description: expenseTexts.categories.description,
          count: counted(expenseCategories?.length, 'category', 'categories'),
        },
        {
          to: '/settings/loan-categories',
          icon: Landmark,
          color: sectionColors.loans,
          title: loanTexts.categories.section,
          description: loanTexts.categories.description,
          count: counted(loanCategories?.length, 'category', 'categories'),
        },
        {
          to: '/recurring-bills',
          icon: Repeat,
          color: sectionColors.month,
          title: expenseTexts.recurring.title,
          description: 'Bills that arrive every month.',
          count: counted(
            recurringBills?.filter((b) => b.isActive).length,
            'active bill',
            'active bills',
          ),
        },
      ],
    },
  ]

  return (
    <div className="flex max-w-5xl flex-col gap-5">
      <PageHeader title="Settings" flush />
      {groups.map((group) => (
        <section key={group.title} aria-label={group.title} className="flex flex-col gap-2">
          <h2 className="font-sans text-xs font-semibold tracking-wider text-muted-foreground uppercase">
            {group.title}
          </h2>
          <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {group.tiles.map((tile) => (
              <li key={tile.to}>
                <Link
                  to={tile.to}
                  className="flex h-full items-start gap-3 rounded-xl bg-card p-4 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8 hover:ring-foreground/20 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
                >
                  <IconTile icon={tile.icon} color={tile.color} solid />
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="font-semibold">{tile.title}</span>
                    <span className="text-sm text-muted-foreground">{tile.description}</span>
                  </span>
                  {tile.count && (
                    <span className="text-xs whitespace-nowrap text-muted-foreground">
                      {tile.count}
                    </span>
                  )}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ))}
      {health?.version && (
        <p className="text-xs text-muted-foreground">Monthwise version {health.version}</p>
      )}
    </div>
  )
}
