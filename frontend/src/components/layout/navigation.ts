import {
  CalendarDays,
  ChartColumn,
  CreditCard,
  Landmark,
  LayoutDashboard,
  PiggyBank,
  Settings,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { sectionColors } from '@/components/design/colors'

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
    label: 'Dashboard',
    shortLabel: 'Home',
    icon: LayoutDashboard,
    color: sectionColors.dashboard,
  },
  { to: '/month', label: 'Month', icon: CalendarDays, color: sectionColors.month },
  {
    to: '/cash-flow',
    label: 'Cash flow',
    shortLabel: 'Cash',
    icon: ChartColumn,
    color: sectionColors.cashflow,
  },
  { to: '/cards', label: 'Cards', icon: CreditCard, color: sectionColors.cards },
  { to: '/loans', label: 'Loans', icon: Landmark, color: sectionColors.loans },
  {
    to: '/shared-costs',
    label: 'Shared costs',
    shortLabel: 'Shared',
    icon: Users,
    color: sectionColors.shared,
  },
  { to: '/savings', label: 'Savings', icon: PiggyBank, color: sectionColors.savings },
  { to: '/settings', label: 'Settings', icon: Settings, color: sectionColors.settings },
]
