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

export interface NavItem {
  to: string
  label: string
  /** A shorter label for the bottom navigation on phones. */
  shortLabel?: string
  icon: LucideIcon
}

export const navItems: NavItem[] = [
  { to: '/', label: 'Dashboard', shortLabel: 'Home', icon: LayoutDashboard },
  { to: '/month', label: 'Month', icon: CalendarDays },
  { to: '/cash-flow', label: 'Cash flow', shortLabel: 'Cash', icon: ChartColumn },
  { to: '/cards', label: 'Cards', icon: CreditCard },
  { to: '/loans', label: 'Loans', icon: Landmark },
  { to: '/shared-costs', label: 'Shared costs', shortLabel: 'Shared', icon: Users },
  { to: '/savings', label: 'Savings', icon: PiggyBank },
  { to: '/settings', label: 'Settings', icon: Settings },
]
