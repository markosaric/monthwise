import { Navigate, type RouteObject } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { ScreenError } from '@/components/layout/ScreenError'
import { lazyScreen, type LazyScreen } from '@/lib/lazyScreen'
import { NotFoundPage } from '@/pages/NotFoundPage'

// Every screen is downloaded the first time it is opened (page loading and errors FR-1).
// A new screen is added here the same way.
const screens: LazyScreen[] = []
const screen = (load: Parameters<typeof lazyScreen>[0]) => {
  const lazy = lazyScreen(load)
  screens.push(lazy)
  return lazy
}

const DashboardPage = screen(() => import('@/pages/DashboardPage').then((m) => m.DashboardPage))
const MonthPage = screen(() => import('@/pages/MonthPage').then((m) => m.MonthPage))
const InputNewPage = screen(() =>
  import('@/pages/month/InputFormPages').then((m) => m.InputNewPage),
)
const InputEditPage = screen(() =>
  import('@/pages/month/InputFormPages').then((m) => m.InputEditPage),
)
const OccurrenceEditPage = screen(() =>
  import('@/pages/month/InputFormPages').then((m) => m.OccurrenceEditPage),
)
const RecurringInputEditPage = screen(() =>
  import('@/pages/month/InputFormPages').then((m) => m.RecurringInputEditPage),
)
const BillNewPage = screen(() =>
  import('@/pages/expenses/BillFormPages').then((m) => m.BillNewPage),
)
const ExpenseNewPage = screen(() =>
  import('@/pages/expenses/ExpenseFormPages').then((m) => m.ExpenseNewPage),
)
const BillEditPage = screen(() =>
  import('@/pages/expenses/BillFormPages').then((m) => m.BillEditPage),
)
const ExpenseEditPage = screen(() =>
  import('@/pages/expenses/ExpenseFormPages').then((m) => m.ExpenseEditPage),
)
const RecurringBillsPage = screen(() =>
  import('@/pages/expenses/RecurringBillsPages').then((m) => m.RecurringBillsPage),
)
const RecurringBillNewPage = screen(() =>
  import('@/pages/expenses/RecurringBillsPages').then((m) => m.RecurringBillNewPage),
)
const RecurringBillEditPage = screen(() =>
  import('@/pages/expenses/RecurringBillsPages').then((m) => m.RecurringBillEditPage),
)
const CashFlowPage = screen(() =>
  import('@/pages/cashflow/CashFlowPage').then((m) => m.CashFlowPage),
)
const MonthlyReportPage = screen(() =>
  import('@/pages/cashflow/MonthlyReportPage').then((m) => m.MonthlyReportPage),
)
const CardsPage = screen(() => import('@/pages/CardsPage').then((m) => m.CardsPage))
const PurchaseNewPage = screen(() =>
  import('@/pages/cardPurchases/PurchaseFormPages').then((m) => m.PurchaseNewPage),
)
const PurchaseDetailsPage = screen(() =>
  import('@/pages/cardPurchases/PurchaseDetailsPage').then((m) => m.PurchaseDetailsPage),
)
const PurchaseEditPage = screen(() =>
  import('@/pages/cardPurchases/PurchaseFormPages').then((m) => m.PurchaseEditPage),
)
const RevolvingCardPage = screen(() =>
  import('@/pages/revolving/RevolvingCardPage').then((m) => m.RevolvingCardPage),
)
const ExtraPaymentNewPage = screen(() =>
  import('@/pages/revolving/ExtraPaymentPages').then((m) => m.ExtraPaymentNewPage),
)
const ExtraPaymentEditPage = screen(() =>
  import('@/pages/revolving/ExtraPaymentPages').then((m) => m.ExtraPaymentEditPage),
)
const InstallmentCardPage = screen(() =>
  import('@/pages/cardPurchases/InstallmentCardPage').then((m) => m.InstallmentCardPage),
)
const CardPaymentPage = screen(() =>
  import('@/pages/cardPurchases/CardPaymentPage').then((m) => m.CardPaymentPage),
)
const LoansPage = screen(() => import('@/pages/LoansPage').then((m) => m.LoansPage))
const LoanNewPage = screen(() => import('@/pages/loans/LoanFormPages').then((m) => m.LoanNewPage))
const LoanDetailsPage = screen(() =>
  import('@/pages/loans/LoanDetailsPage').then((m) => m.LoanDetailsPage),
)
const LoanEditPage = screen(() => import('@/pages/loans/LoanFormPages').then((m) => m.LoanEditPage))
const SharedCostsPage = screen(() =>
  import('@/pages/sharedCosts/SharedCostsPage').then((m) => m.SharedCostsPage),
)
const ParticipantNewPage = screen(() =>
  import('@/pages/sharedCosts/ParticipantFormPages').then((m) => m.ParticipantNewPage),
)
const ParticipantEditPage = screen(() =>
  import('@/pages/sharedCosts/ParticipantFormPages').then((m) => m.ParticipantEditPage),
)
const LendingPage = screen(() => import('@/pages/LendingPage').then((m) => m.LendingPage))
const LendingNewPage = screen(() =>
  import('@/pages/lending/LendingFormPages').then((m) => m.LendingNewPage),
)
const LendingEditPage = screen(() =>
  import('@/pages/lending/LendingFormPages').then((m) => m.LendingEditPage),
)
const SavingsPage = screen(() => import('@/pages/SavingsPage').then((m) => m.SavingsPage))
const SettingsPage = screen(() => import('@/pages/SettingsPage').then((m) => m.SettingsPage))
const CardsSettingsPage = screen(() =>
  import('@/pages/settings/CardsSettingsPage').then((m) => m.CardsSettingsPage),
)
const CardNewPage = screen(() =>
  import('@/pages/settings/CardFormPages').then((m) => m.CardNewPage),
)
const CardDetailsPage = screen(() =>
  import('@/pages/settings/CardDetailsPage').then((m) => m.CardDetailsPage),
)
const CardEditPage = screen(() =>
  import('@/pages/settings/CardFormPages').then((m) => m.CardEditPage),
)
const PayersSettingsPage = screen(() =>
  import('@/pages/settings/PayersSettingsPage').then((m) => m.PayersSettingsPage),
)
const InputCategoriesSettingsPage = screen(() =>
  import('@/pages/settings/InputCategoriesSettingsPage').then((m) => m.InputCategoriesSettingsPage),
)
const LoanCategoriesSettingsPage = screen(() =>
  import('@/pages/settings/LoanCategoriesSettingsPage').then((m) => m.LoanCategoriesSettingsPage),
)
const ExpenseCategoriesSettingsPage = screen(() =>
  import('@/pages/settings/ExpenseCategoriesSettingsPage').then(
    (m) => m.ExpenseCategoriesSettingsPage,
  ),
)
const DataSettingsPage = screen(() =>
  import('@/pages/settings/DataSettingsPage').then((m) => m.DataSettingsPage),
)

export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    errorElement: <ScreenError fullPage />,
    children: [
      {
        // A failing screen shows its message below the app shell (FR-3).
        errorElement: <ScreenError />,
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
          {
            path: 'month/:yearMonth/recurring-inputs/:id/edit',
            element: <RecurringInputEditPage />,
          },
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
          { path: 'cards/revolving/:cardId', element: <RevolvingCardPage /> },
          { path: 'cards/revolving/:cardId/pay', element: <ExtraPaymentNewPage /> },
          {
            path: 'cards/revolving/:cardId/payments/:paymentId/edit',
            element: <ExtraPaymentEditPage />,
          },
          { path: 'pay-revolving', element: <ExtraPaymentNewPage /> },
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
          { path: 'lending', element: <LendingPage /> },
          { path: 'lending/new', element: <LendingNewPage /> },
          { path: 'lending/:id/edit', element: <LendingEditPage /> },
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
          { path: 'settings/data', element: <DataSettingsPage /> },
          {
            path: 'settings/purchase-categories',
            element: <Navigate to="/settings/expense-categories" replace />,
          },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
]

/** Downloads every screen, so tests can render any screen at once. */
export function preloadScreens() {
  return Promise.all(screens.map((s) => s.preload()))
}
