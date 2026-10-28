import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { aCard } from '@/test/cardFixtures'
import { aCardPayment } from '@/test/cardPurchaseFixtures'
import { aMonth } from '@/test/cashFlowFixtures'
import {
  aBill,
  aBillMonth,
  anExpense,
  anExpenseMonth,
  aNotIssuedBill,
  expenseCategories,
  tekuciPaidFrom,
  visaPaidFrom,
} from '@/test/expenseFixtures'
import { aLoanMonth } from '@/test/loanFixtures'
import { renderRoute } from '@/test/renderRoute'
import { json, stubApi } from '@/test/stubApi'
import { addMonths, currentMonth } from '@/lib/dates'
import { expenseTexts } from './texts'

const t = expenseTexts
const month = '2026-10'
const tekuci = aCard({ id: 'tekuci', name: 'Tekući račun', type: 'Debit', isPrimary: true })
const savings = aCard({ id: 'savings', name: 'Savings', type: 'Debit', isPrimary: false })
const debitWithInstallments = aCard({
  id: 'debit-inst',
  name: 'Debit with installments',
  type: 'Debit',
  isPrimary: false,
  allowsInstallments: true,
})
const visa = aCard({ id: 'visa', name: 'Family Visa', type: 'Credit' })
const lists = {
  'GET /api/cards': () => json([tekuci, savings, debitWithInstallments, visa]),
  'GET /api/expense-categories': () => json(expenseCategories),
}
const monthApi = {
  ...lists,
  'GET /api/inputs': () => json(aMonth([])),
  'GET /api/loan-payments': () => json(aLoanMonth([])),
  'GET /api/card-payments': () =>
    json({ month, payments: [], totals: { expectedCents: 0, paidCents: 0, totalCents: 0 } }),
}

function lastBody(fetchMock: ReturnType<typeof stubApi>, method: string) {
  const call = fetchMock.mock.calls.findLast(([request]) => (request as Request).method === method)
  return call ? (call[0] as Request).clone().json() : null
}

describe('Bills on the Month screen', () => {
  it('keeps the add buttons in their sections and shows short rows with details on request', async () => {
    stubApi({
      ...monthApi,
      'GET /api/bills': () =>
        json(
          aBillMonth([
            aBill({ isOverdue: true }),
            aBill({ id: 'bill-2', name: 'Water', state: 'Paid', paidDate: '2026-10-05' }),
          ]),
        ),
      'GET /api/expenses': () => json(anExpenseMonth([])),
    })
    renderRoute(`/month/${month}`)

    // Design refresh FR-15: the add buttons stay in the section they belong to.
    const bills = await screen.findByRole('region', { name: t.bills.title })
    expect(within(bills).getByRole('link', { name: t.bills.addBill })).toHaveAttribute(
      'href',
      `/month/${month}/bills/new`,
    )
    expect(within(bills).getByRole('button', { name: t.bills.addRecurring })).toBeInTheDocument()
    expect(await within(bills).findByText('1 of 2 paid')).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Quick actions' })).not.toBeInTheDocument()
    const expenses = screen.getByRole('region', { name: t.expenses.title })
    expect(within(expenses).getByRole('link', { name: t.expenses.add })).toBeInTheDocument()
    expect(
      within(expenses).getByRole('link', { name: t.expenses.addCashWithdrawal }),
    ).toHaveAttribute('href', `/month/${month}/expenses/new?kind=cash`)

    // A row shows its status; category and account only when expanded.
    const row = within(bills).getByRole('link', { name: 'Electricity, apartment' }).closest('li')!
    expect(within(row).getByText(t.labels.overdue)).toBeInTheDocument()
    const details = within(row).getByText(/Electricity · Tekući račun/)
    expect(details).not.toBeVisible()
    await userEvent.click(
      within(row).getByRole('button', { name: 'Show details: Electricity, apartment' }),
    )
    expect(details).toBeVisible()
  })

  it('shows not issued bills without a Paid box, overdue and card markers, and totals', async () => {
    stubApi({
      ...monthApi,
      'GET /api/expenses': () => json(anExpenseMonth([])),
      'GET /api/bills': () =>
        json(
          aBillMonth([
            aBill({ isOverdue: true }),
            aNotIssuedBill(),
            aBill({
              id: 'bill-3',
              name: 'Gym',
              state: 'Paid',
              paidDate: '2026-10-05',
              paidFrom: visaPaidFrom,
              paidWithCard: true,
              amountCents: 3000,
            }),
          ]),
        ),
    })
    renderRoute(`/month/${month}`)

    const notIssued = (await screen.findByRole('link', { name: 'Internet' })).closest('li')!
    expect(notIssued).toHaveAttribute('data-state', 'NotIssued')
    expect(within(notIssued).getByRole('checkbox')).toBeDisabled()
    expect(within(notIssued).getByText(/20\.10\.2026 \(hint\)/)).toBeInTheDocument()
    const overdue = screen.getByRole('link', { name: 'Electricity, apartment' }).closest('li')!
    expect(within(overdue).getByText(t.labels.overdue)).toBeInTheDocument()
    const gym = screen.getByRole('link', { name: 'Gym' }).closest('li')!
    expect(within(gym).getByText(t.labels.withCard)).toBeInTheDocument()
    expect(screen.getByText(t.bills.notIssuedCount(1), { exact: false })).toBeInTheDocument()
    expect(screen.getByText(/Paid with credit cards .*30,00 €/)).toBeInTheDocument()
  })

  it('asks for the paid date and what the bill was paid with when it is ticked', async () => {
    const fetchMock = stubApi({
      ...monthApi,
      'GET /api/expenses': () => json(anExpenseMonth([])),
      'GET /api/bills': () => json(aBillMonth([aBill()])),
      'POST /api/bills/bill-1/paid': () => json(aBill({ state: 'Paid', paidDate: '2026-10-09' })),
    })
    renderRoute(`/month/${month}`)

    await userEvent.click(await screen.findByRole('checkbox', { name: /Electricity, apartment/ }))
    const dialog = await screen.findByRole('alertdialog')
    const paidFrom = within(dialog).getByLabelText<HTMLSelectElement>(t.fields.paidFrom)
    expect(paidFrom.value).toBe('tekuci')
    // Debit cards are accounts, also when they allow installments; credit cards are their own group.
    const groups = within(paidFrom).getAllByRole('group')
    expect(within(groups[0]!).getByRole('option', { name: 'Debit with installments' })).toBeTruthy()
    expect(within(groups[1]!).getByRole('option', { name: 'Family Visa' })).toBeTruthy()
    await userEvent.selectOptions(paidFrom, 'visa')
    await userEvent.click(within(dialog).getByRole('button', { name: t.actions.save }))

    await waitFor(async () =>
      expect(await lastBody(fetchMock, 'POST')).toMatchObject({ paidFromId: 'visa' }),
    )
  })

  it('adds the recurring bills to the next month and says how many were added', async () => {
    const next = addMonths(currentMonth(), 1)
    let requested = ''
    stubApi({
      ...monthApi,
      'GET /api/expenses': () => json(anExpenseMonth([])),
      'GET /api/bills': () => json(aBillMonth([])),
      'POST /api/bills/add-recurring': (request) => {
        requested = new URL(request.url).searchParams.get('month') ?? ''
        return json({ month: next, added: 2 })
      },
    })
    renderRoute(`/month/${month}`)

    await userEvent.click(await screen.findByRole('button', { name: t.bills.addRecurring }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByLabelText<HTMLInputElement>(t.fields.month).value).toBe(next)
    await userEvent.click(within(dialog).getByRole('button', { name: t.actions.save }))

    await waitFor(() => expect(requested).toBe(next))
    expect(await screen.findByText(/2 bills added to/)).toBeInTheDocument()
  })

  it('prefills the due date of a not issued bill from its hint and issues it', async () => {
    // The issue date defaults to today; keep it before the due date hint.
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-15T12:00:00'))
    onTestFinished(() => {
      vi.useRealTimers()
    })
    const fetchMock = stubApi({
      ...lists,
      'GET /api/bills/bill-2': () => json(aNotIssuedBill()),
      'PUT /api/bills/bill-2': () => json(aBill({ id: 'bill-2' })),
      'GET /api/bills': () => json(aBillMonth([])),
    })
    renderRoute('/bills/bill-2/edit')

    const due = await screen.findByLabelText<HTMLInputElement>(t.fields.dueDate)
    expect(due.value).toBe('2026-10-20')
    expect(screen.getByText(t.hints.dueDateHint)).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(t.fields.amount), '39,90')
    await userEvent.click(screen.getByRole('button', { name: t.actions.save }))

    await waitFor(async () =>
      expect(await lastBody(fetchMock, 'PUT')).toMatchObject({
        name: 'Internet',
        amountCents: 3990,
        dueDate: '2026-10-20',
        paidFromId: 'tekuci',
        paidDate: null,
      }),
    )
  })

  it('needs amount and dates for a one-off bill', async () => {
    const fetchMock = stubApi(lists)
    renderRoute(`/month/${month}/bills/new`)

    await userEvent.type(await screen.findByLabelText(t.fields.name), 'Plumber')
    await userEvent.selectOptions(screen.getByLabelText(t.fields.category), 'electricity')
    await userEvent.click(screen.getByRole('button', { name: t.bills.addBill }))

    expect(await screen.findByText(t.errors.amountInvalid)).toBeInTheDocument()
    expect(screen.getByText(t.errors.dueDateRequired)).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(false)
  })
})

describe('Expenses', () => {
  it('lists expenses with the cash withdrawal and card totals', async () => {
    stubApi({
      ...monthApi,
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/expenses': () =>
        json(
          anExpenseMonth([
            anExpense(),
            anExpense({
              id: 'expense-2',
              description: null,
              category: { id: 'cash', name: 'Cash withdrawal' },
              isCashWithdrawal: true,
              amountCents: 10000,
            }),
            anExpense({
              id: 'expense-3',
              paidFrom: visaPaidFrom,
              paidWithCard: true,
              amountCents: 2500,
            }),
          ]),
        ),
    })
    renderRoute(`/month/${month}`)

    // Expenses are always paid, so the section starts collapsed (design refresh FR-15).
    await userEvent.click(await screen.findByRole('button', { name: `Expand ${t.expenses.title}` }))
    expect(await screen.findAllByRole('link', { name: 'Konzum' })).toHaveLength(2)
    expect(screen.getByText(t.expenses.totals.cash).nextElementSibling).toHaveTextContent(
      '100,00 €',
    )
    expect(screen.getByText(t.expenses.totals.withCard).nextElementSibling).toHaveTextContent(
      '25,00 €',
    )
  })

  it('prefills what the last expense was paid with and records a new one', async () => {
    const fetchMock = stubApi({
      ...lists,
      'GET /api/expenses/last-paid-from': () => json({ paidFrom: visaPaidFrom }),
      'POST /api/expenses': () => json(anExpense()),
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/expenses': () => json(anExpenseMonth([])),
    })
    renderRoute(`/month/${currentMonth()}/expenses/new`)

    const paidFrom = await screen.findByLabelText<HTMLSelectElement>(t.fields.paidFrom)
    await waitFor(() => expect(paidFrom.value).toBe('visa'))
    expect(screen.getByText(t.hints.creditCard)).toBeInTheDocument()
    // Cash withdrawal is not offered for an expense.
    expect(
      within(screen.getByLabelText(t.fields.category)).queryByRole('option', {
        name: 'Cash withdrawal',
      }),
    ).not.toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(t.fields.amount), '12,30')
    await userEvent.selectOptions(screen.getByLabelText(t.fields.category), 'groceries')
    await userEvent.click(screen.getByRole('button', { name: t.actions.save }))

    await waitFor(async () =>
      expect(await lastBody(fetchMock, 'POST')).toMatchObject({
        amountCents: 1230,
        categoryId: 'groceries',
        paidFromId: 'visa',
        description: null,
      }),
    )
  })

  it('offers only primary accounts for a cash withdrawal', async () => {
    const fetchMock = stubApi({
      ...lists,
      'GET /api/expenses/last-paid-from': () => json({ paidFrom: tekuciPaidFrom }),
      'POST /api/expenses/cash-withdrawals': () => json(anExpense({ isCashWithdrawal: true })),
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/expenses': () => json(anExpenseMonth([])),
    })
    renderRoute(`/month/${currentMonth()}/expenses/new?kind=cash`)

    const account = await screen.findByLabelText<HTMLSelectElement>(t.fields.account)
    expect(
      within(account)
        .getAllByRole('option')
        .map((o) => o.textContent),
    ).toEqual([t.fields.choosePaidFrom, 'Tekući račun'])
    expect(screen.queryByLabelText(t.fields.category)).not.toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(t.fields.amount), '100')
    await userEvent.click(screen.getByRole('button', { name: t.actions.save }))

    await waitFor(async () =>
      expect(await lastBody(fetchMock, 'POST')).toMatchObject({
        amountCents: 10000,
        accountId: 'tekuci',
      }),
    )
  })
})

describe('Expense categories and the card drill-down', () => {
  it('keeps Cash withdrawal fixed in Settings', async () => {
    stubApi(lists)
    renderRoute('/settings/expense-categories')

    const cash = (await screen.findByText('Cash withdrawal')).parentElement!
    expect(within(cash).getByText(t.categories.fixed)).toBeInTheDocument()
    expect(
      within(cash).queryByRole('button', { name: t.categories.rename }),
    ).not.toBeInTheDocument()
    const groceries = screen.getByText('Groceries').parentElement!
    expect(within(groceries).getByRole('button', { name: t.categories.hide })).toBeInTheDocument()
  })

  it('opens bills and expenses paid with the card from the drill-down', async () => {
    stubApi({
      ...lists,
      'GET /api/cards/visa/card-payments/2026-10': () =>
        json({
          payment: aCardPayment({ cardId: 'visa' }),
          lines: [
            {
              sourceId: 'bill-1',
              description: 'Internet',
              date: '2026-10-02',
              kind: 'Bill',
              installmentNumber: 1,
              installmentTotal: 1,
              category: { id: 'electricity', name: 'Electricity' },
              amountCents: 3000,
            },
            {
              sourceId: 'expense-1',
              description: 'Konzum',
              date: '2026-10-03',
              kind: 'Expense',
              installmentNumber: 1,
              installmentTotal: 1,
              category: { id: 'groceries', name: 'Groceries' },
              amountCents: 4000,
            },
          ],
        }),
    })
    renderRoute('/cards/visa/payments/2026-10')

    expect(await screen.findByRole('link', { name: 'Internet' })).toHaveAttribute(
      'href',
      '/bills/bill-1/edit',
    )
    expect(screen.getByRole('link', { name: 'Konzum' })).toHaveAttribute(
      'href',
      '/expenses/expense-1/edit',
    )
  })
})
