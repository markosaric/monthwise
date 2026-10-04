import type { RouteObject } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { CardsPage } from '@/pages/CardsPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { LoansPage } from '@/pages/LoansPage'
import { MonthPage } from '@/pages/MonthPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { SavingsPage } from '@/pages/SavingsPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { CardDetailsPage } from '@/pages/settings/CardDetailsPage'
import { CardEditPage, CardNewPage } from '@/pages/settings/CardFormPages'
import { CardsSettingsPage } from '@/pages/settings/CardsSettingsPage'

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
      { path: 'settings/cards', element: <CardsSettingsPage /> },
      { path: 'settings/cards/new', element: <CardNewPage /> },
      { path: 'settings/cards/:id', element: <CardDetailsPage /> },
      { path: 'settings/cards/:id/edit', element: <CardEditPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]
