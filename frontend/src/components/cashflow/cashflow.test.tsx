import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { aCard } from '@/test/cardFixtures'
import { aMonth, anEntry, otherIncome, salary } from '@/test/cashFlowFixtures'
import { renderRoute } from '@/test/renderRoute'
import { healthy, json, stubApi } from '@/test/stubApi'
import { addMonths, lastDayOf, todayIso } from '@/lib/dates'
import { parseSignedEuroInput } from '@/lib/money'
import { cashFlowTexts } from './texts'

const t = cashFlowTexts
const lists = {
  'GET /api/input-categories': () => json([otherIncome, salary]),
  'GET /api/payers': () => json([{ id: 'payer-1', name: 'Example Employer', isRemoved: false }]),
}

describe('date and amount helpers', () => {
  it('adds months across years and finds the last day', () => {
    expect(addMonths('2026-11', 3)).toBe('2027-02')
    expect(addMonths('2026-01', -1)).toBe('2025-12')
    expect(lastDayOf('2028-02')).toBe('2028-02-29')
  })

  it('parses signed balances', () => {
    expect(parseSignedEuroInput('-250,00')).toBe(-25000)
    expect(parseSignedEuroInput('1.234,56')).toBe(123456)
    expect(parseSignedEuroInput('abc')).toBeNull()
  })
})

describe('Add input form', () => {
  it('prefills the account when exactly one account is primary', async () => {
    stubApi({
      ...lists,
      'GET /api/cards': () =>
        json([
          aCard({ id: 'tekuci', name: 'Tekući', type: 'Debit', isPrimary: true }),
          aCard({ id: 'visa' }),
        ]),
    })
    renderRoute('/month/2026-10/inputs/new')

    const account = await screen.findByLabelText<HTMLSelectElement>(t.fields.account)
    await waitFor(() => expect(account.value).toBe('tekuci'))
  })

  it('makes the user choose when several accounts are primary', async () => {
    stubApi({
      ...lists,
      'GET /api/cards': () =>
        json([
          aCard({ id: 'a', name: 'PBZ', type: 'Debit', isPrimary: true }),
          aCard({ id: 'b', name: 'ZaBa', type: 'Debit', isPrimary: true }),
        ]),
    })
    renderRoute('/month/2026-10/inputs/new')

    const account = await screen.findByLabelText<HTMLSelectElement>(t.fields.account)
    expect(account.value).toBe('')
    expect(screen.getByText(t.hints.account)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: t.addInput }))
    expect(await screen.findByText(t.errors.accountRequired)).toBeInTheDocument()
  })

  it('asks to set up an account when none can receive money', async () => {
    stubApi({ ...lists, 'GET /api/cards': () => json([aCard({ status: 'Blocked' })]) })
    renderRoute('/month/2026-10/inputs/new')

    expect(await screen.findByText(t.noAccounts)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: t.setUpAccount })).toHaveAttribute(
      'href',
      '/settings/cards',
    )
  })

  it('sets the received date to today when Received is ticked, and sends the request', async () => {
    let body: Record<string, unknown> | undefined
    stubApi({
      ...lists,
      'GET /api/cards': () => json([aCard({ id: 'tekuci', type: 'Debit', isPrimary: true })]),
      'POST /api/inputs': async (request) => {
        body = (await request.json()) as Record<string, unknown>
        return json(anEntry({ receivedDate: todayIso(), isReceived: true }))
      },
      'GET /api/inputs': () => json(aMonth([])),
    })
    renderRoute('/month/2026-10/inputs/new')

    await userEvent.type(await screen.findByLabelText(t.fields.payer), 'Example Employer')
    await userEvent.selectOptions(screen.getByLabelText(t.fields.category), salary.id)
    await userEvent.type(screen.getByLabelText(t.fields.amount), '1.850,00')
    await userEvent.click(screen.getByLabelText(t.fields.received))
    expect(screen.getByLabelText<HTMLInputElement>(t.fields.receivedDate).value).toBe(todayIso())
    await userEvent.click(screen.getByRole('button', { name: t.addInput }))

    await waitFor(() => expect(body).toBeDefined())
    expect(body).toMatchObject({
      payerName: 'Example Employer',
      accountId: 'tekuci',
      categoryId: salary.id,
      amountCents: 185000,
      receivedDate: todayIso(),
      recurrence: null,
    })
  })

  it('shows API field errors next to their fields', async () => {
    stubApi({
      ...lists,
      'GET /api/cards': () => json([aCard({ id: 'tekuci', type: 'Debit', isPrimary: true })]),
      'POST /api/inputs': () =>
        json(
          {
            title: 'One or more validation errors occurred.',
            errors: { accountId: ['This account is blocked.'] },
          },
          400,
        ),
    })
    renderRoute('/month/2026-10/inputs/new')

    await userEvent.type(await screen.findByLabelText(t.fields.payer), 'Example Employer')
    await userEvent.selectOptions(screen.getByLabelText(t.fields.category), salary.id)
    await userEvent.type(screen.getByLabelText(t.fields.amount), '10')
    await userEvent.click(screen.getByRole('button', { name: t.addInput }))

    expect(await screen.findByText('This account is blocked.')).toBeInTheDocument()
  })
})

describe('Income section on the Month screen', () => {
  it('shows the empty state with an Add input action', async () => {
    stubApi({ 'GET /api/inputs': () => json(aMonth([])) })
    renderRoute('/month/2026-10')

    expect(await screen.findByText(t.emptyMonth)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: t.addInput })).toHaveAttribute(
      'href',
      '/month/2026-10/inputs/new',
    )
  })

  it('dims expected inputs and shows the totals', async () => {
    stubApi({
      'GET /api/inputs': () =>
        json(
          aMonth([
            anEntry({
              id: 'a',
              payer: { id: 'p1', name: 'Received payer' },
              isReceived: true,
              receivedDate: '2026-10-12',
              amountCents: 100000,
            }),
            anEntry({ id: 'b', payer: { id: 'p2', name: 'Expected payer' }, amountCents: 5000 }),
          ]),
        ),
    })
    renderRoute('/month/2026-10')

    const expectedRow = (await screen.findByText('Expected payer')).closest('li')!
    const receivedRow = screen.getByText('Received payer').closest('li')!
    expect(expectedRow).toHaveAttribute('data-expected', 'true')
    expect(receivedRow).not.toHaveAttribute('data-expected')
    // Received on another day than expected shows both dates.
    expect(
      within(receivedRow).getByText(/Received 12\.10\.2026 \(expected 10\.10\.2026\)/),
    ).toBeInTheDocument()
    expect(screen.getAllByText('50,00 €').length).toBeGreaterThan(0)
    expect(screen.getByText('1.050,00 €')).toBeInTheDocument()
  })

  it('ticks a calculated occurrence of a recurring input', async () => {
    const fetchMock = stubApi({
      'GET /api/inputs': () =>
        json(
          aMonth([
            anEntry({
              id: null,
              recurringInputId: 'rec-1',
              occurrenceDate: '2026-10-10',
              isRecurring: true,
              frequency: 'Monthly',
            }),
          ]),
        ),
      'POST /api/recurring-inputs/rec-1/occurrences/2026-10-10/received': () =>
        json(anEntry({ isReceived: true, receivedDate: todayIso() })),
    })
    renderRoute('/month/2026-10')

    expect(await screen.findByText(t.frequencies.Monthly)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('checkbox', { name: /Received: Example Employer/ }))

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) =>
          String(input instanceof Request ? input.url : input).includes(
            '/occurrences/2026-10-10/received',
          ),
        ),
      ).toBe(true),
    )
  })
})

describe('Dashboard balances', () => {
  it('shows current and expected balance and the starting balance hint', async () => {
    stubApi({
      ...healthy,
      'GET /api/cards/dashboard': () =>
        json({
          hasAnyCards: true,
          cards: [aCard({ id: 'tekuci', name: 'Tekući', type: 'Debit', isPrimary: true })],
          balances: [
            {
              accountId: 'tekuci',
              currentBalanceCents: -2500,
              expectedBalanceCents: 180000,
              hasStartingBalance: false,
            },
          ],
        }),
    })
    renderRoute('/')

    expect(await screen.findByText('-25,00 €')).toHaveClass('text-destructive')
    expect(screen.getByText('1.800,00 €')).toBeInTheDocument()
    expect(screen.getByText(t.balance.noStarting)).toBeInTheDocument()
  })
})

describe('Cash flow dashboard and report', () => {
  it('shows the chart data as a table', async () => {
    const month = todayIso().slice(0, 7)
    stubApi({
      'GET /api/cards': () => json([]),
      ...lists,
      'GET /api/cash-flow/chart': () =>
        json({
          from: `${month}-01`,
          to: lastDayOf(month),
          granularity: 'Day',
          buckets: [
            { start: `${month}-01`, receivedCents: 0, expectedCents: 0 },
            { start: `${month}-02`, receivedCents: 185000, expectedCents: 2000 },
          ],
        }),
    })
    renderRoute('/cash-flow')

    expect(await screen.findByText(t.cashFlow.received)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: t.cashFlow.showTable }))
    const table = await screen.findByRole('table')
    expect(within(table).getByText('1.850,00 €')).toBeInTheDocument()
    expect(within(table).getByText('20,00 €')).toBeInTheDocument()
    expect(within(table).getAllByRole('row')).toHaveLength(2)
  })

  it('shows report totals, groups and the difference to the previous month', async () => {
    stubApi({
      'GET /api/cash-flow/report': () =>
        json({
          month: '2026-10',
          totals: { expectedCents: 20000, receivedCents: 185000, totalCents: 205000 },
          byCategory: [
            {
              id: salary.id,
              name: 'Salary',
              totals: { expectedCents: 0, receivedCents: 185000, totalCents: 185000 },
            },
          ],
          byAccount: [
            {
              id: 'tekuci',
              name: 'Tekući',
              totals: { expectedCents: 20000, receivedCents: 185000, totalCents: 205000 },
            },
          ],
          byPayer: [],
          balanceEntries: [
            {
              account: { id: 'tekuci', name: 'Tekući' },
              kind: 'Correction',
              date: '2026-10-15',
              balanceCents: 10000,
              differenceCents: -500,
              note: null,
            },
          ],
          previousMonthTotalCents: 200000,
          differenceCents: 5000,
        }),
    })
    renderRoute('/cash-flow/2026-10')

    expect(await screen.findByText('+50,00 €')).toBeInTheDocument()
    expect(screen.getByText(t.report.byCategory)).toBeInTheDocument()
    expect(screen.getByText(t.balance.kinds.Correction)).toBeInTheDocument()
    expect(screen.getByText('(-5,00 €)')).toBeInTheDocument()
  })
})
