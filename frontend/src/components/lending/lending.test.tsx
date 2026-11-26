import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { Lending, LendingList } from '@/api/lending'
import { aCard } from '@/test/cardFixtures'
import { renderRoute } from '@/test/renderRoute'
import { json, stubApi } from '@/test/stubApi'
import { repaymentErrors, repaymentFormValuesOf } from './lendingFormModel'
import { lendingTexts } from './texts'

const t = lendingTexts

// Made-up borrowers, accounts and amounts only (NFR-4).
const tekuci = aCard({ id: 'tekuci', name: 'Tekući', type: 'Debit', isPrimary: true })
const stednja = aCard({ id: 'stednja', name: 'Štednja', type: 'Debit' })
const visa = aCard({ id: 'visa', name: 'Visa', type: 'Credit' })

function aLending(overrides: Partial<Lending> = {}): Lending {
  return {
    id: 'lending-1',
    borrower: { id: 'payer-ivo', name: 'Ivo' },
    amountCents: 50000,
    lendDate: '2026-10-05',
    fromAccount: { id: 'tekuci', name: 'Tekući' },
    expectedReturnDate: '2026-12-15',
    description: 'For the car repair',
    returnedCents: 20000,
    outstandingCents: 30000,
    state: 'Open',
    repayments: [
      {
        id: 'repayment-1',
        amountCents: 20000,
        toAccount: { id: 'tekuci', name: 'Tekući' },
        receivedDate: '2026-11-01',
        note: null,
      },
    ],
    ...overrides,
  }
}

function aList(lendings: Lending[]): LendingList {
  return {
    lendings,
    outstandingCents: lendings.reduce((s, l) => s + l.outstandingCents, 0),
    overdueCents: lendings
      .filter((l) => l.state === 'Overdue')
      .reduce((s, l) => s + l.outstandingCents, 0),
  }
}

function lastBody(fetchMock: ReturnType<typeof stubApi>, method: string) {
  const call = fetchMock.mock.calls.findLast(([request]) => (request as Request).method === method)
  return call ? (call[0] as Request).clone().json() : null
}

const lists = {
  'GET /api/cards': () => json([tekuci, stednja, visa]),
  'GET /api/payers': () => json([{ id: 'payer-ivo', name: 'Ivo', isRemoved: false }]),
  'GET /api/lendings/borrowers': () => json([]),
}

describe('repayment rules', () => {
  it('prefills everything outstanding, the lending account and today (FR-4, R2, R3)', () => {
    expect(repaymentFormValuesOf(aLending(), '2026-11-26')).toEqual({
      amount: '300',
      toAccountId: 'tekuci',
      receivedDate: '2026-11-26',
      note: '',
    })
  })

  it('refuses more than outstanding, counting an edited repayment as outstanding again', () => {
    const lending = aLending()
    const values = { amount: '300,01', toAccountId: 'tekuci', receivedDate: '2026-11-26', note: '' }
    expect(repaymentErrors(values, lending).amount).toBe(t.errors.aboveOutstanding)
    expect(repaymentErrors(values, lending, lending.repayments[0]).amount).toBeUndefined()
  })

  it('refuses a received date before the lend date (R3)', () => {
    const values = { amount: '10', toAccountId: 'tekuci', receivedDate: '2026-10-04', note: '' }
    expect(repaymentErrors(values, aLending()).receivedDate).toBe(t.errors.receivedBeforeLend)
  })
})

describe('Add lending form', () => {
  it('prefills the only primary account, offers no credit cards and suggests payers', async () => {
    stubApi(lists)
    renderRoute('/lending/new')

    const account = await screen.findByLabelText<HTMLSelectElement>(t.fields.fromAccount)
    await waitFor(() => expect(account.value).toBe('tekuci'))
    const options = within(account)
      .getAllByRole('option')
      .map((o) => o.textContent)
    expect(options).toEqual([t.fields.chooseAccount, 'Tekući', 'Štednja'])
    await waitFor(() =>
      expect(document.querySelector('#borrower-suggestions option')).toHaveAttribute(
        'value',
        'Ivo',
      ),
    )
  })

  it('shows each error next to its field and saves nothing', async () => {
    const fetchMock = stubApi(lists)
    renderRoute('/lending/new')

    await userEvent.type(await screen.findByLabelText(t.fields.amount), '0')
    const back = screen.getByLabelText(t.fields.expectedReturnDate)
    await userEvent.type(back, '01012020')
    await userEvent.click(screen.getByRole('button', { name: t.actions.save }))

    expect(await screen.findByText(t.errors.borrowerRequired)).toBeInTheDocument()
    expect(screen.getByText(t.errors.amountInvalid)).toBeInTheDocument()
    expect(screen.getByText(t.errors.returnBeforeLend)).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(false)
  })

  it('saves a lending with a new borrower', async () => {
    const fetchMock = stubApi({
      ...lists,
      'POST /api/lendings': () => json(aLending(), 201),
      'GET /api/lendings': () => json(aList([aLending()])),
    })
    renderRoute('/lending/new')

    await userEvent.type(await screen.findByLabelText(t.fields.borrower), 'Marta')
    await userEvent.type(screen.getByLabelText(t.fields.amount), '250,50')
    const lendDate = screen.getByLabelText(t.fields.lendDate)
    await userEvent.clear(lendDate)
    await userEvent.type(lendDate, '01112026')
    await userEvent.type(screen.getByLabelText(t.fields.expectedReturnDate), '31122026')
    await userEvent.click(screen.getByRole('button', { name: t.actions.save }))

    await waitFor(async () =>
      expect(await lastBody(fetchMock, 'POST')).toEqual({
        borrowerName: 'Marta',
        amountCents: 25050,
        lendDate: '2026-11-01',
        fromAccountId: 'tekuci',
        expectedReturnDate: '2026-12-31',
        description: null,
      }),
    )
  })

  it('says an account must be set up first', async () => {
    stubApi({ ...lists, 'GET /api/cards': () => json([visa]) })
    renderRoute('/lending/new')

    expect(await screen.findByText(t.hints.noAccount)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: t.actions.setUpAccounts })).toHaveAttribute(
      'href',
      '/settings/cards',
    )
  })
})

describe('Lending screen', () => {
  it('shows the empty state with an Add lending action (FR-1)', async () => {
    stubApi({ ...lists, 'GET /api/lendings': () => json(aList([])) })
    renderRoute('/lending')

    expect(await screen.findByText(t.empty)).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: t.add }).length).toBeGreaterThan(0)
  })

  it('highlights an overdue lending and shows the totals (FR-1, FR-5)', async () => {
    stubApi({
      ...lists,
      'GET /api/lendings': () =>
        json(
          aList([
            aLending({ id: 'late', state: 'Overdue', expectedReturnDate: '2026-11-01' }),
            aLending({ id: 'fine', borrower: { id: 'payer-ana', name: 'Ana' } }),
          ]),
        ),
    })
    renderRoute('/lending')

    const late = (await screen.findByText('Ivo')).closest('li')!
    expect(late).toHaveAttribute('data-overdue', 'true')
    expect(within(late).getByText(t.states.Overdue)).toBeInTheDocument()
    expect(screen.getByText('Ana').closest('li')).not.toHaveAttribute('data-overdue')
    const figures = screen.getByRole('region', { name: /key figures/i })
    expect(within(figures).getByText(/600,00/)).toBeInTheDocument()
  })

  it('shows returned lendings with the Returned filter', async () => {
    let asked: string | null = null
    stubApi({
      ...lists,
      'GET /api/lendings': (request) => {
        asked = new URL(request.url).searchParams.get('state')
        return json(
          aList(
            asked === 'Returned'
              ? [aLending({ state: 'Returned', outstandingCents: 0, returnedCents: 50000 })]
              : [],
          ),
        )
      },
    })
    renderRoute('/lending')

    await userEvent.click(await screen.findByRole('button', { name: t.filters.Returned }))
    const card = (await screen.findByText('Ivo')).closest('li')!
    expect(within(card).getAllByText(t.states.Returned).length).toBeGreaterThan(0)
    expect(asked).toBe('Returned')
    expect(screen.queryByRole('button', { name: t.actions.recordRepayment })).toBeNull()
  })

  it('records a repayment prefilled with the outstanding amount (FR-4)', async () => {
    const fetchMock = stubApi({
      ...lists,
      'GET /api/lendings': () => json(aList([aLending()])),
      'POST /api/lendings/lending-1/repayments': () => json(aLending(), 201),
    })
    renderRoute('/lending')

    await userEvent.click(await screen.findByRole('button', { name: t.actions.recordRepayment }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByLabelText<HTMLInputElement>(t.fields.amount).value).toBe('300')
    expect(within(dialog).getByLabelText<HTMLSelectElement>(t.fields.toAccount).value).toBe(
      'tekuci',
    )
    await userEvent.click(within(dialog).getByRole('button', { name: t.actions.save }))

    await waitFor(async () =>
      expect(await lastBody(fetchMock, 'POST')).toMatchObject({
        amountCents: 30000,
        toAccountId: 'tekuci',
      }),
    )
  })

  it('names the borrower, amount, date and repayments before deleting (FR-3)', async () => {
    stubApi({ ...lists, 'GET /api/lendings': () => json(aList([aLending()])) })
    renderRoute('/lending')

    await userEvent.click(await screen.findByRole('button', { name: t.actions.delete }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(t.dialogs.deleteTitle('Ivo'))).toBeInTheDocument()
    expect(
      within(dialog).getByText(/500,00.*05\.10\.2026.*repayment is deleted too/),
    ).toBeInTheDocument()
  })
})
