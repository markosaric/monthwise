import type { RouteObject } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { CardsPage } from '@/pages/CardsPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { LoansPage } from '@/pages/LoansPage'
import { MonthPage } from '@/pages/MonthPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { SavingsPage } from '@/pages/SavingsPage'
import { SettingsPage } from '@/pages/SettingsPage'

export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'month', element: <MonthPage /> },
      { path: 'cards', element: <CardsPage /> },
      { path: 'loans', element: <LoansPage /> },
      { path: 'savings', element: <SavingsPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]
