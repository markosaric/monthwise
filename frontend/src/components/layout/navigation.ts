import {
  CalendarDays,
  ChartColumn,
  CreditCard,
  HandCoins,
  Landmark,
  LayoutDashboard,
  PiggyBank,
  Settings,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { sectionColors } from '@/components/design/colors'
import { appTexts } from '@/components/common/texts'

export interface NavItem {
  to: string
  label: string
  /** A shorter label for the bottom navigation on phones. */
  shortLabel?: string
  icon: LucideIcon
  /** The section colour of the icon (design refresh FR-7). */
  color: string
}

export const navItems: NavItem[] = [
  {
    to: '/',
    label: appTexts.nav.dashboard,
    shortLabel: appTexts.nav.home,
    icon: LayoutDashboard,
    color: sectionColors.dashboard,
  },
  { to: '/month', label: appTexts.nav.month, icon: CalendarDays, color: sectionColors.month },
  {
    to: '/cash-flow',
    label: appTexts.nav.cashFlow,
    shortLabel: appTexts.nav.cashShort,
    icon: ChartColumn,
    color: sectionColors.cashflow,
  },
  { to: '/cards', label: appTexts.nav.cards, icon: CreditCard, color: sectionColors.cards },
  { to: '/loans', label: appTexts.nav.loans, icon: Landmark, color: sectionColors.loans },
  {
    to: '/shared-costs',
    label: appTexts.nav.sharedCosts,
    shortLabel: appTexts.nav.sharedShort,
    icon: Users,
    color: sectionColors.shared,
  },
  {
    to: '/lending',
    label: appTexts.nav.lending,
    shortLabel: appTexts.nav.lendingShort,
    icon: HandCoins,
    color: sectionColors.lending,
  },
  { to: '/savings', label: appTexts.nav.savings, icon: PiggyBank, color: sectionColors.savings },
  { to: '/settings', label: appTexts.nav.settings, icon: Settings, color: sectionColors.settings },
]
