import { act, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { safeReturnTo } from '@/lib/returnTo'
import { aCard } from '@/test/cardFixtures'
import { anInstallmentCard } from '@/test/cardPurchaseFixtures'
import { expenseCategories } from '@/test/expenseFixtures'
import { aDashboard } from '@/test/dashboardFixtures'
import { renderRoute } from '@/test/renderRoute'
import { healthy, json, stubApi, type ApiHandlers } from '@/test/stubApi'
import { dashboardTexts as t } from './texts'

function stubDashboard(dashboard = aDashboard(), extra: ApiHandlers = {}) {
  return stubApi({
    ...healthy,
    'GET /api/dashboard': () => json(dashboard),
    'GET /api/card-purchases/cards': () => json([anInstallmentCard()]),
    ...extra,
  })
}

const keyFigures = () => screen.getByRole('region', { name: t.keyFigures })

describe('Dashboard', () => {
  it('shows the four key figures of the month with the change against last month', async () => {
    stubDashboard()
    renderRoute('/')

    expect(await screen.findByRole('heading', { name: 'October 2026' })).toBeInTheDocument()
    const figures = within(keyFigures())
    expect(figures.getByRole('link', { name: /Inputs/ })).toHaveTextContent('3.250,00 €')
    expect(figures.getByRole('link', { name: /Inputs/ })).toHaveTextContent('2.900,00 € received')
    // 3,250.00 against 3,125.00 is 4% more inputs: good, so green.
    expect(figures.getByRole('link', { name: /Inputs/ })).toHaveTextContent('up 4%')
    expect(figures.getByRole('link', { name: /Outputs/ })).toHaveTextContent('1.915,00 € paid')
    expect(figures.getByRole('link', { name: /Month result/ })).toHaveTextContent('770,00 €')
    expect(figures.getByRole('link', { name: /Month result/ })).toHaveAttribute(
      'href',
      '/cash-flow/2026-10',
    )
    expect(figures.getByRole('link', { name: /Still to pay/ })).toHaveTextContent('4 items')
  })

  it('shows no account balances, no cards list and no system status card', async () => {
    stubDashboard()
    renderRoute('/')

    await screen.findByRole('heading', { name: 'October 2026' })
    expect(screen.queryByText(/Current balance/)).not.toBeInTheDocument()
    expect(screen.queryByText(/Expected balance/)).not.toBeInTheDocument()
    expect(screen.queryByText(/starting balance/i)).not.toBeInTheDocument()
    expect(screen.queryByText('System status')).not.toBeInTheDocument()
  })

  it('highlights the next payment, overdue payments, or a fully paid month', async () => {
    stubDashboard()
    const { unmount } = renderRoute('/')
    expect(await screen.findByRole('status')).toHaveTextContent(
      'Next: Electricity 64,20 € on 22.10.2026',
    )
    expect(screen.getByRole('status')).toHaveTextContent(
      '3 more bills and payments due this month.',
    )
    unmount()

    stubDashboard(
      aDashboard({
        stillToPay: { totalCents: 56500, count: 4, overdueCents: 3840, overdueCount: 1 },
      }),
    )
    const second = renderRoute('/')
    expect(await screen.findByRole('status')).toHaveTextContent(
      '1 bill or payment is overdue: 38,40 €',
    )
    expect(screen.getByRole('status')).toHaveClass('bg-problem')
    second.unmount()

    stubDashboard(
      aDashboard({
        stillToPay: { totalCents: 0, count: 0, overdueCents: 0, overdueCount: 0 },
        nextDue: null,
      }),
    )
    renderRoute('/')
    expect(await screen.findByRole('status')).toHaveTextContent(
      'All bills and payments of October are paid',
    )
  })

  it('lists what is coming up in the next 7 days, or says nothing is', async () => {
    stubDashboard()
    const { unmount } = renderRoute('/')
    const list = (await screen.findByText(t.comingUp)).closest('[data-slot=card]') as HTMLElement
    expect(within(list).getByText('Car loan').closest('li')).toHaveTextContent('−310,00 €')
    expect(within(list).getByText('Contribution from Ana').closest('li')).toHaveTextContent(
      '+150,00 €',
    )
    unmount()

    stubDashboard(aDashboard({ comingUp: [] }))
    renderRoute('/')
    expect(await screen.findByText(t.nothingComingUp)).toBeInTheDocument()
  })

  it('shows outputs by kind with exact amounts and small cards only for sections with data', async () => {
    stubDashboard(aDashboard({ loans: null, sharedCosts: null }))
    renderRoute('/')

    const ring = (await screen.findByText(t.outputsByKind.title)).closest(
      '[data-slot=card]',
    ) as HTMLElement
    expect(within(ring).getByText('Loan payments').closest('li')).toHaveTextContent('595,20 €')
    expect(screen.getByRole('link', { name: /9 recurring bills/ })).toHaveAttribute(
      'href',
      '/recurring-bills',
    )
    expect(screen.getByRole('link', { name: /Card payments this month/ })).toHaveAttribute(
      'href',
      '/cards',
    )
    expect(screen.queryByRole('link', { name: /to repay/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Shared costs outstanding/ })).not.toBeInTheDocument()
  })

  it('shows quick actions as icons that open their forms and come back to the Dashboard', async () => {
    stubDashboard()
    renderRoute('/')

    const actions = await screen.findByRole('navigation', { name: 'Quick actions' })
    const links = within(actions).getAllByRole('link')
    expect(links.map((l) => l.getAttribute('aria-label'))).toEqual([
      'Add income',
      'Add bill',
      'Add expense',
      'Cash withdrawal',
      'Add purchase',
    ])
    links.forEach((l) => expect(l.textContent).toBe(''))
    expect(within(actions).getByRole('link', { name: 'Cash withdrawal' })).toHaveAttribute(
      'href',
      '/month/2026-10/expenses/new?kind=cash&returnTo=/',
    )
    expect(within(actions).getByRole('link', { name: 'Add purchase' })).toHaveAttribute(
      'href',
      '/cards/purchases/new?returnTo=/',
    )
  })

  it('shows the label of a quick action on keyboard focus', async () => {
    stubDashboard()
    renderRoute('/')

    await screen.findByRole('navigation', { name: 'Quick actions' })
    act(() => screen.getByRole('link', { name: 'Add income' }).focus())
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Add income')
  })

  it('hides Add purchase when no card can take a purchase', async () => {
    stubDashboard(aDashboard(), {
      'GET /api/card-purchases/cards': () => json([anInstallmentCard({ canBeUsed: false })]),
    })
    renderRoute('/')

    const actions = await screen.findByRole('navigation', { name: 'Quick actions' })
    expect(within(actions).getAllByRole('link')).toHaveLength(4)
    expect(within(actions).queryByRole('link', { name: 'Add purchase' })).not.toBeInTheDocument()
  })

  it('asks to add inputs and bills when the month is empty', async () => {
    stubDashboard(
      aDashboard({
        inputs: { totalCents: 0, doneCents: 0, previousMonthTotalCents: 0 },
        outputs: { totalCents: 0, doneCents: 0, previousMonthTotalCents: 0 },
        monthResultCents: 0,
        previousMonthResultCents: 0,
        stillToPay: { totalCents: 0, count: 0, overdueCents: 0, overdueCount: 0 },
        nextDue: null,
        comingUp: [],
      }),
    )
    renderRoute('/')

    expect(await screen.findByRole('status')).toHaveTextContent(t.highlight.empty)
    expect(screen.getByRole('status')).toHaveAttribute('href', '/month')
  })

  it('returns from a form opened by a quick action to the Dashboard', async () => {
    stubDashboard(aDashboard(), {
      'GET /api/cards': () => json([aCard({ isPrimary: true })]),
      'GET /api/expense-categories': () => json(expenseCategories),
      'GET /api/participants': () => json([]),
    })
    renderRoute('/month/2026-10/bills/new?returnTo=/')

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }))

    expect(await screen.findByRole('heading', { name: 'October 2026' })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Quick actions' })).toBeInTheDocument()
  })
})

describe('safeReturnTo', () => {
  it('accepts only paths inside the app', () => {
    expect(safeReturnTo('/')).toBe('/')
    expect(safeReturnTo('/month/2026-10')).toBe('/month/2026-10')
    expect(safeReturnTo('https://example.com')).toBeNull()
    expect(safeReturnTo('//example.com')).toBeNull()
    expect(safeReturnTo('/\\example.com')).toBeNull()
    expect(safeReturnTo(null)).toBeNull()
  })
})
