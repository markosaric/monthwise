import {
  CalendarDays,
  ChartColumn,
  CreditCard,
  Landmark,
  LayoutDashboard,
  PiggyBank,
  Settings,
  type LucideIcon,
} from 'lucide-react'

export interface NavItem {
  to: string
  label: string
  icon: LucideIcon
}

export const navItems: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/month', label: 'Month', icon: CalendarDays },
  { to: '/cash-flow', label: 'Cash flow', icon: ChartColumn },
  { to: '/cards', label: 'Cards', icon: CreditCard },
  { to: '/loans', label: 'Loans', icon: Landmark },
  { to: '/savings', label: 'Savings', icon: PiggyBank },
  { to: '/settings', label: 'Settings', icon: Settings },
]
