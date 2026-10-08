import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { cardTexts } from '@/components/cards/texts'
import { aCard, zaba } from '@/test/cardFixtures'
import { aMonth } from '@/test/cashFlowFixtures'
import { aLoan, aLoanMonth, aPayment, bankLoan, lease } from '@/test/loanFixtures'
import { renderRoute } from '@/test/renderRoute'
import { healthy, json, stubApi } from '@/test/stubApi'
import { endMonthOf, paymentCountOf, toLoanRequest } from './loanFormModel'
import { loanTexts } from './texts'

const t = loanTexts
const lists = {
  'GET /api/loan-categories': () => json([bankLoan, lease]),
  'GET /api/banks': () => json([zaba]),
}
const tekuci = aCard({ id: 'tekuci', name: 'Tekući', type: 'Debit', isPrimary: true })

describe('count and end month', () => {
  it('calculates one from the other', () => {
    expect(endMonthOf('2026-10', 12)).toBe('2027-09')
    expect(endMonthOf('2026-10', 1)).toBe('2026-10')
    expect(paymentCountOf('2026-10', '2027-09')).toBe(12)
    expect(paymentCountOf('2026-10', '2026-09')).toBeNull()
  })
})

describe('Add loan form', () => {
  it('prefills the only primary account', async () => {
    stubApi({ ...lists, 'GET /api/cards': () => json([tekuci, aCard({ id: 'visa' })]) })
    renderRoute('/loans/new')

    const account = await screen.findByLabelText<HTMLSelectElement>(t.fields.account)
    await waitFor(() => expect(account.value).toBe('tekuci'))
  })

  it('makes the user choose when several accounts are primary', async () => {
    stubApi({
      ...lists,
      'GET /api/cards': () =>
        json([tekuci, aCard({ id: 'pbz', name: 'PBZ', type: 'Debit', isPrimary: true })]),
    })
    renderRoute('/loans/new')

    const account = await screen.findByLabelText<HTMLSelectElement>(t.fields.account)
    expect(account.value).toBe('')
    expect(screen.getByText(t.hints.account)).toBeInTheDocument()
  })

  it('says a standing order needs a primary account and allows a bill', async () => {
    stubApi({ ...lists, 'GET /api/cards': () => json([aCard({ id: 'visa' })]) })
    renderRoute('/loans/new')

    expect(await screen.findByText(t.hints.noPrimaryAccount)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: t.actions.setUpAccounts })).toHaveAttribute(
      'href',
      '/settings/cards',
    )
    await userEvent.selectOptions(screen.getByLabelText(t.fields.paymentMethod), 'Bill')
    expect(screen.queryByText(t.hints.noPrimaryAccount)).not.toBeInTheDocument()
    expect(screen.getByText(t.hints.bill)).toBeInTheDocument()
  })

  it('keeps the number of payments and the end month in step', async () => {
    stubApi({ ...lists, 'GET /api/cards': () => json([tekuci]) })
    renderRoute('/loans/new')

    const start = await screen.findByLabelText<HTMLInputElement>(t.fields.startMonth)
    const count = screen.getByLabelText<HTMLInputElement>(t.fields.paymentCount)
    const end = screen.getByLabelText<HTMLInputElement>(t.fields.endMonth)
    await userEvent.clear(start)
    await userEvent.type(start, '2026-10')
    await userEvent.type(count, '12')
    expect(end.value).toBe('2027-09')

    await userEvent.clear(end)
    await userEvent.type(end, '2028-09')
    expect(count.value).toBe('24')
  })

  it('offers a residual value only for a category that allows it', async () => {
    stubApi({ ...lists, 'GET /api/cards': () => json([tekuci]) })
    renderRoute('/loans/new')

    const category = await screen.findByLabelText(t.fields.category)
    await waitFor(() => expect(within(category).getByText('Lease')).toBeInTheDocument())
    await userEvent.selectOptions(category, bankLoan.id)
    expect(screen.queryByLabelText(t.fields.residualValue)).not.toBeInTheDocument()
    await userEvent.selectOptions(category, lease.id)
    expect(screen.getByLabelText(t.fields.residualValue)).toBeInTheDocument()
  })

  it('shows API validation errors next to their fields', async () => {
    const fetchMock = stubApi({
      ...lists,
      'GET /api/cards': () => json([tekuci]),
      'POST /api/loans': () =>
        json(
          {
            title: 'One or more validation errors occurred.',
            errors: { name: ['Another loan already has this name.'] },
          },
          400,
        ),
    })
    renderRoute('/loans/new')

    await userEvent.type(await screen.findByLabelText(t.fields.name), 'Flat')
    const category = screen.getByLabelText(t.fields.category)
    await waitFor(() => expect(within(category).getByText('Bank loan')).toBeInTheDocument())
    await userEvent.selectOptions(category, bankLoan.id)
    await userEvent.selectOptions(screen.getByLabelText(t.fields.subcategory), 'sub-cash')
    await userEvent.selectOptions(screen.getByLabelText(t.fields.lender), zaba.id)
    await userEvent.type(screen.getByLabelText(t.fields.paymentDay), '15')
    await userEvent.type(screen.getByLabelText(t.fields.monthlyPayment), '500')
    await userEvent.type(screen.getByLabelText(t.fields.paymentCount), '12')
    await userEvent.click(screen.getByRole('button', { name: t.addLoan }))

    expect(await screen.findByText('Another loan already has this name.')).toBeInTheDocument()
    const post = fetchMock.mock.calls
      .map(([request]) => request as Request)
      .find((r) => r.method === 'POST')
    const body = await post!.clone().json()
    expect(body).toMatchObject({ monthlyPaymentCents: 50000, paymentCount: 12, endMonth: null })
  })

  it('sends the end month when it was typed last', () => {
    const request = toLoanRequest({
      name: ' Car ',
      categoryId: lease.id,
      subcategoryId: 'sub-car-lease',
      lenderId: zaba.id,
      paymentMethod: 'Bill',
      accountId: 'tekuci',
      startMonth: '2026-10',
      paymentDay: '15',
      monthlyPayment: '300',
      paymentCount: '24',
      endMonth: '2028-09',
      lastEdited: 'end',
      residualValue: '2.000,00',
      allowsResidualValue: true,
      description: '',
    })
    expect(request).toMatchObject({
      name: 'Car',
      accountId: null,
      paymentCount: null,
      endMonth: '2028-09',
      residualValueCents: 200000,
      description: null,
    })
  })
})

describe('Expenses on the Month screen', () => {
  const month = '2026-10'
  const monthApi = {
    'GET /api/inputs': () => json(aMonth([])),
    'GET /api/cards': () => json([tekuci, aCard({ id: 'visa' })]),
  }

  it('dims expected payments, shows markers and totals', async () => {
    stubApi({
      ...monthApi,
      'GET /api/loan-payments': () =>
        json(
          aLoanMonth([
            aPayment({ isPaid: true, paidDate: '2026-10-16', isChanged: true }),
            aPayment({
              loanId: 'loan-2',
              loanName: 'Car lease',
              paymentMethod: 'Bill',
              account: null,
              amountCents: 30000,
              isOverdue: true,
            }),
          ]),
        ),
    })
    renderRoute(`/month/${month}`)

    const expected = await screen.findByRole('link', { name: 'Car lease' })
    expect(expected.closest('li')).toHaveAttribute('data-expected', 'true')
    expect(screen.getByRole('link', { name: 'Example flat' }).closest('li')).not.toHaveAttribute(
      'data-expected',
    )
    expect(screen.getByText(t.labels.changed)).toBeInTheDocument()
    expect(screen.getByText(t.labels.overdue)).toBeInTheDocument()
    // Paid on another day than due: both dates are shown.
    expect(screen.getByText(/16\.10\.2026 \(due 15\.10\.2026\)/)).toBeInTheDocument()
    expect(screen.getByText(t.expenses.totals.total).nextElementSibling).toHaveTextContent(
      '800,00 €',
    )
  })

  it('asks for the account when a bill is ticked, prefilled with the only primary account', async () => {
    const fetchMock = stubApi({
      ...monthApi,
      'GET /api/loan-payments': () =>
        json(aLoanMonth([aPayment({ paymentMethod: 'Bill', account: null })])),
      'POST /api/loans/loan-1/payments/2026-10/Regular/paid': () =>
        json(aPayment({ paymentMethod: 'Bill', isPaid: true, paidDate: '2026-10-20' })),
    })
    renderRoute(`/month/${month}`)

    await userEvent.click(await screen.findByRole('checkbox', { name: /Example flat/ }))
    const dialog = await screen.findByRole('alertdialog')
    const account = within(dialog).getByLabelText<HTMLSelectElement>(t.payment.account)
    expect(account.value).toBe('tekuci')
    await userEvent.click(within(dialog).getByRole('button', { name: t.actions.save }))

    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([request]) => (request as Request).method === 'POST')).toBe(
        true,
      ),
    )
    const post = fetchMock.mock.calls
      .map(([request]) => request as Request)
      .find((r) => r.method === 'POST')!
    expect(await post.clone().json()).toMatchObject({ accountId: 'tekuci' })
  })

  it('unticks a paid standing order payment', async () => {
    const fetchMock = stubApi({
      ...monthApi,
      'GET /api/loan-payments': () =>
        json(aLoanMonth([aPayment({ isPaid: true, paidDate: '2026-10-15' })])),
      'DELETE /api/loans/loan-1/payments/2026-10/Regular/paid': () => json(aPayment()),
    })
    renderRoute(`/month/${month}`)

    await userEvent.click(await screen.findByRole('checkbox', { name: /Example flat/ }))
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([request]) => (request as Request).method === 'DELETE'),
      ).toBe(true),
    )
  })
})

describe('Loans screen and Dashboard widget', () => {
  it('shows an empty state with Add loan', async () => {
    stubApi({ 'GET /api/loans': () => json([]) })
    renderRoute('/loans')

    expect(await screen.findByText(t.empty)).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: t.addLoan })[0]).toHaveAttribute(
      'href',
      '/loans/new',
    )
  })

  it('is not shown on the Dashboard without loans', async () => {
    const fetchMock = stubApi({
      ...healthy,
      'GET /api/cards/dashboard': () => json({ hasAnyCards: false, cards: [], balances: [] }),
      'GET /api/loans/dashboard': () =>
        json({
          hasAnyLoans: false,
          loans: [],
          totalMonthlyPaymentCents: 0,
          totalRemainingCents: 0,
        }),
    })
    renderRoute('/')

    // Both widgets load together; once the Cards widget shows its data, the loans call is answered too.
    expect(await screen.findByText(cardTexts.dashboardNoCards)).toBeInTheDocument()
    expect(
      fetchMock.mock.calls.some(([request]) =>
        (request as Request).url.endsWith('/api/loans/dashboard'),
      ),
    ).toBe(true)
    expect(screen.queryByRole('link', { name: t.actions.viewAll })).not.toBeInTheDocument()
  })

  it('shows totals and highlights an overdue loan', async () => {
    stubApi({
      ...healthy,
      'GET /api/cards/dashboard': () => json({ hasAnyCards: false, cards: [], balances: [] }),
      'GET /api/loans/dashboard': () =>
        json({
          hasAnyLoans: true,
          loans: [
            aLoan(),
            aLoan({
              id: 'loan-2',
              name: 'Car lease',
              hasOverduePayment: true,
              remainingAmountCents: 300000,
            }),
          ],
          totalMonthlyPaymentCents: 100000,
          totalRemainingCents: 750000,
        }),
    })
    renderRoute('/')

    const overdue = await screen.findByRole('link', { name: /Car lease/ })
    expect(overdue.closest('li')).toHaveAttribute('data-highlighted', 'true')
    expect(screen.getByRole('link', { name: /Example flat/ }).closest('li')).not.toHaveAttribute(
      'data-highlighted',
    )
    expect(overdue).toHaveAttribute('href', '/loans/loan-2')
    expect(screen.getByText(t.widget.totalMonthly).nextElementSibling).toHaveTextContent(
      '1.000,00 €',
    )
    expect(screen.getByText(t.widget.totalRemaining).nextElementSibling).toHaveTextContent(
      '7.500,00 €',
    )
  })
})
