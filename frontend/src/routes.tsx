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
import { CashFlowPage } from '@/pages/cashflow/CashFlowPage'
import { MonthlyReportPage } from '@/pages/cashflow/MonthlyReportPage'
import {
  InputEditPage,
  InputNewPage,
  OccurrenceEditPage,
  RecurringInputEditPage,
} from '@/pages/month/InputFormPages'
import { InputCategoriesSettingsPage } from '@/pages/settings/InputCategoriesSettingsPage'
import { PayersSettingsPage } from '@/pages/settings/PayersSettingsPage'

export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'month', element: <MonthPage /> },
      { path: 'month/:yearMonth', element: <MonthPage /> },
      { path: 'month/:yearMonth/inputs/new', element: <InputNewPage /> },
      { path: 'month/:yearMonth/inputs/:id/edit', element: <InputEditPage /> },
      {
        path: 'month/:yearMonth/recurring-inputs/:id/occurrences/:date/edit',
        element: <OccurrenceEditPage />,
      },
      { path: 'month/:yearMonth/recurring-inputs/:id/edit', element: <RecurringInputEditPage /> },
      { path: 'cash-flow', element: <CashFlowPage /> },
      { path: 'cash-flow/:yearMonth', element: <MonthlyReportPage /> },
      { path: 'cards', element: <CardsPage /> },
      { path: 'loans', element: <LoansPage /> },
      { path: 'savings', element: <SavingsPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'settings/cards', element: <CardsSettingsPage /> },
      { path: 'settings/cards/new', element: <CardNewPage /> },
      { path: 'settings/cards/:id', element: <CardDetailsPage /> },
      { path: 'settings/cards/:id/edit', element: <CardEditPage /> },
      { path: 'settings/payers', element: <PayersSettingsPage /> },
      { path: 'settings/input-categories', element: <InputCategoriesSettingsPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]
