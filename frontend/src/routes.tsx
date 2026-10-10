import { Navigate, type RouteObject } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { CardsPage } from '@/pages/CardsPage'
import { CardPaymentPage } from '@/pages/cardPurchases/CardPaymentPage'
import { InstallmentCardPage } from '@/pages/cardPurchases/InstallmentCardPage'
import { PurchaseDetailsPage } from '@/pages/cardPurchases/PurchaseDetailsPage'
import { PurchaseEditPage, PurchaseNewPage } from '@/pages/cardPurchases/PurchaseFormPages'
import { DashboardPage } from '@/pages/DashboardPage'
import { BillEditPage, BillNewPage } from '@/pages/expenses/BillFormPages'
import { ExpenseEditPage, ExpenseNewPage } from '@/pages/expenses/ExpenseFormPages'
import {
  RecurringBillEditPage,
  RecurringBillNewPage,
  RecurringBillsPage,
} from '@/pages/expenses/RecurringBillsPages'
import { LoansPage } from '@/pages/LoansPage'
import { MonthPage } from '@/pages/MonthPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { SavingsPage } from '@/pages/SavingsPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { ParticipantEditPage, ParticipantNewPage } from '@/pages/sharedCosts/ParticipantFormPages'
import { SharedCostsPage } from '@/pages/sharedCosts/SharedCostsPage'
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
import { LoanDetailsPage } from '@/pages/loans/LoanDetailsPage'
import { LoanEditPage, LoanNewPage } from '@/pages/loans/LoanFormPages'
import { InputCategoriesSettingsPage } from '@/pages/settings/InputCategoriesSettingsPage'
import { LoanCategoriesSettingsPage } from '@/pages/settings/LoanCategoriesSettingsPage'
import { PayersSettingsPage } from '@/pages/settings/PayersSettingsPage'
import { ExpenseCategoriesSettingsPage } from '@/pages/settings/ExpenseCategoriesSettingsPage'

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
      { path: 'month/:yearMonth/bills/new', element: <BillNewPage /> },
      { path: 'month/:yearMonth/expenses/new', element: <ExpenseNewPage /> },
      { path: 'bills/:id/edit', element: <BillEditPage /> },
      { path: 'expenses/:id/edit', element: <ExpenseEditPage /> },
      { path: 'recurring-bills', element: <RecurringBillsPage /> },
      { path: 'recurring-bills/new', element: <RecurringBillNewPage /> },
      { path: 'recurring-bills/:id/edit', element: <RecurringBillEditPage /> },
      { path: 'cash-flow', element: <CashFlowPage /> },
      { path: 'cash-flow/:yearMonth', element: <MonthlyReportPage /> },
      { path: 'cards', element: <CardsPage /> },
      { path: 'cards/purchases/new', element: <PurchaseNewPage /> },
      { path: 'cards/purchases/:id', element: <PurchaseDetailsPage /> },
      { path: 'cards/purchases/:id/edit', element: <PurchaseEditPage /> },
      { path: 'cards/:cardId', element: <InstallmentCardPage /> },
      { path: 'cards/:cardId/payments/:month', element: <CardPaymentPage /> },
      { path: 'loans', element: <LoansPage /> },
      { path: 'loans/new', element: <LoanNewPage /> },
      { path: 'loans/:id', element: <LoanDetailsPage /> },
      { path: 'loans/:id/edit', element: <LoanEditPage /> },
      { path: 'shared-costs', element: <SharedCostsPage /> },
      { path: 'shared-costs/:yearMonth', element: <SharedCostsPage /> },
      { path: 'shared-costs/participants/new', element: <ParticipantNewPage /> },
      { path: 'shared-costs/participants/:id/edit', element: <ParticipantEditPage /> },
      { path: 'savings', element: <SavingsPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'settings/cards', element: <CardsSettingsPage /> },
      { path: 'settings/cards/new', element: <CardNewPage /> },
      { path: 'settings/cards/:id', element: <CardDetailsPage /> },
      { path: 'settings/cards/:id/edit', element: <CardEditPage /> },
      { path: 'settings/payers', element: <PayersSettingsPage /> },
      { path: 'settings/input-categories', element: <InputCategoriesSettingsPage /> },
      { path: 'settings/loan-categories', element: <LoanCategoriesSettingsPage /> },
      { path: 'settings/expense-categories', element: <ExpenseCategoriesSettingsPage /> },
      {
        path: 'settings/purchase-categories',
        element: <Navigate to="/settings/expense-categories" replace />,
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]
