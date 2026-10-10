import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { aCard } from '@/test/cardFixtures'
import { aMonth } from '@/test/cashFlowFixtures'
import { aBill, aBillMonth, anExpenseMonth, expenseCategories } from '@/test/expenseFixtures'
import { aLoanMonth } from '@/test/loanFixtures'
import { renderRoute } from '@/test/renderRoute'
import {
  aContribution,
  aContributionEntry,
  ana,
  aSharedCosts,
  halfWithAna,
} from '@/test/sharedCostFixtures'
import { json, stubApi } from '@/test/stubApi'
import { ownPartOf, partOf, percentageOf } from './shareModel'
import { sharedCostTexts } from './texts'

const t = sharedCostTexts
const month = '2026-10'
const tekuci = aCard({ id: 'tekuci', name: 'Tekući račun', type: 'Debit', isPrimary: true })
const monthApi = {
  'GET /api/cards': () => json([tekuci]),
  'GET /api/expense-categories': () => json(expenseCategories),
  'GET /api/participants': () => json([ana]),
  'GET /api/expenses': () => json(anExpenseMonth([])),
  'GET /api/loan-payments': () => json(aLoanMonth([])),
  'GET /api/card-payments': () =>
    json({ month, payments: [], totals: { expectedCents: 0, paidCents: 0, totalCents: 0 } }),
}

function lastRequest(fetchMock: ReturnType<typeof stubApi>, method: string) {
  return fetchMock.mock.calls
    .map(([request]) => request as Request)
    .findLast((r) => r.method === method)
}

describe('Share amounts', () => {
  it('match the back-end rounding: to the nearest cent, half a cent up, adding up to the amount', () => {
    expect(partOf(10_001, 2, 1)).toBe(5_001)
    expect(partOf(10_000, 3, 1)).toBe(3_333)
    expect(partOf(10_000, 3, 2)).toBe(6_667)
    expect(partOf(10_002, 4, 1)).toBe(2_501)
    expect(partOf(10_001, 4, 3)).toBe(7_501)
    expect(partOf(10_001, 4, 3) + ownPartOf(10_001, 4, 3)).toBe(10_001)
    expect(percentageOf(3, 2)).toBe(67)
  })
})

describe('Sharing a bill', () => {
  it('offers 2 to 4 shares, fewer participant shares than shares, and shows both parts', async () => {
    const fetchMock = stubApi({
      ...monthApi,
      'POST /api/bills': () => json(aBill({ share: halfWithAna })),
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/inputs': () => json(aMonth([])),
    })
    renderRoute(`/month/${month}/bills/new`)

    await userEvent.type(await screen.findByLabelText('Name'), 'Electricity, apartment')
    await userEvent.selectOptions(screen.getByLabelText('Category'), 'Electricity')
    await userEvent.type(screen.getByLabelText('Amount (€)'), '85,01')
    await userEvent.type(screen.getByLabelText('Due date'), '2026-10-15')
    await userEvent.click(screen.getByLabelText(t.share.shared))

    const shares = screen.getByLabelText(t.share.shares)
    expect(
      within(shares)
        .getAllByRole('option')
        .map((o) => o.textContent),
    ).toEqual(['2 shares', '3 shares', '4 shares'])
    await userEvent.selectOptions(screen.getByLabelText(t.share.participant), 'Ana')
    expect(screen.getByText(/Ana pays 50 %/)).toBeInTheDocument()
    expect(screen.getByText(/Their part 42,51 €, your part 42,50 €/)).toBeInTheDocument()

    await userEvent.selectOptions(shares, '4')
    const participantShares = screen.getByLabelText(t.share.participantShares)
    expect(within(participantShares).getAllByRole('option')).toHaveLength(3)

    await userEvent.click(screen.getByRole('button', { name: 'Add bill' }))
    await waitFor(() => expect(lastRequest(fetchMock, 'POST')).toBeDefined())
    const body = await lastRequest(fetchMock, 'POST')!.clone().json()
    expect(body.share).toEqual({
      participantId: 'ana',
      shares: 4,
      participantShares: 1,
      contributionAccountId: null,
    })
  })

  it('refuses a shared bill without a participant', async () => {
    stubApi({ ...monthApi, 'GET /api/bills': () => json(aBillMonth([])) })
    renderRoute(`/month/${month}/bills/new`)

    await userEvent.click(await screen.findByLabelText(t.share.shared))
    await userEvent.click(screen.getByRole('button', { name: 'Add bill' }))

    expect(await screen.findByText(t.share.participantRequired)).toBeInTheDocument()
  })
})

describe('Shared costs on the Month screen', () => {
  it('shows the own part of a shared bill and of the bills total', async () => {
    stubApi({
      ...monthApi,
      'GET /api/inputs': () => json(aMonth([])),
      'GET /api/bills': () =>
        json(aBillMonth([aBill({ share: halfWithAna, ownPartCents: 2125, amountCents: 4250 })])),
    })
    renderRoute(`/month/${month}`)

    expect(await screen.findByText('Ana pays 50 %')).toBeInTheDocument()
    expect(screen.getByText('Your part 21,25 €')).toBeInTheDocument()
    expect(screen.getByText(t.share.ownPartTotal)).toBeInTheDocument()
  })

  it('lists a contribution in Income with its bill, ticks it as received and offers no delete', async () => {
    const fetchMock = stubApi({
      ...monthApi,
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/inputs': () => json(aMonth([aContributionEntry()])),
      'POST /api/bills/bill-1/contribution/received': () =>
        json(aContribution({ isReceived: true })),
    })
    renderRoute(`/month/${month}`)

    await userEvent.click(await screen.findByRole('button', { name: /Show 1/ }))
    const link = screen.getByRole('link', { name: t.contribution.of('Electricity, apartment') })
    expect(link).toHaveAttribute('href', '/bills/bill-1/edit')
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: /Received: Ana, Electricity/ }))
    await waitFor(() =>
      expect(lastRequest(fetchMock, 'POST')?.url).toContain(
        '/api/bills/bill-1/contribution/received',
      ),
    )
  })

  it('marks a contribution without an account and asks for one when it is ticked', async () => {
    stubApi({
      ...monthApi,
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/inputs': () =>
        json(
          aMonth([
            aContributionEntry({
              account: { id: '00000000-0000-0000-0000-000000000000', name: 'No account yet' },
              contribution: { ...aContributionEntry().contribution!, needsAccount: true },
            }),
          ]),
        ),
    })
    renderRoute(`/month/${month}`)

    await userEvent.click(await screen.findByRole('button', { name: /Show 1/ }))
    expect(screen.getByText(t.contribution.needsAccount)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('checkbox', { name: /Received: Ana, Electricity/ }))
    expect(await screen.findByLabelText(t.contribution.account)).toBeInTheDocument()
  })
})

describe('Contributions grouped by contributor', () => {
  const mortgage = aContributionEntry({
    amountCents: 30293,
    description: 'Mortgage 1/156',
    contribution: {
      ...aContributionEntry().contribution!,
      kind: 'LoanPayment',
      billId: null,
      loanId: 'loan-1',
      month: '2026-10',
      paymentKind: 'Regular',
      costName: 'Mortgage 1/156',
      partCents: 30293,
    },
  })

  it('shows one line with the summed amount that opens the single contributions', async () => {
    stubApi({
      ...monthApi,
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/inputs': () => json(aMonth([aContributionEntry(), mortgage])),
    })
    renderRoute(`/month/${month}`)

    expect(await screen.findByText(t.contribution.group(2))).toBeInTheDocument()
    const group = screen
      .getByRole('checkbox', { name: t.contribution.groupReceived('Ana') })
      .closest('li')!
    expect(within(group).getByText('324,18 €')).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: t.contribution.of('Mortgage 1/156') }),
    ).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /Show 2/ }))
    expect(
      screen.getByRole('link', { name: t.contribution.of('Mortgage 1/156') }),
    ).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /Received: Ana, Electricity/ })).toBeInTheDocument()
  })

  it('ticks every contribution of the contributor with the group check box', async () => {
    const fetchMock = stubApi({
      ...monthApi,
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/inputs': () => json(aMonth([aContributionEntry(), mortgage])),
      'POST /api/bills/bill-1/contribution/received': () =>
        json(aContribution({ isReceived: true })),
      'POST /api/loans/loan-1/payments/2026-10/Regular/contribution/received': () =>
        json(aContribution({ isReceived: true })),
    })
    renderRoute(`/month/${month}`)

    await userEvent.click(
      await screen.findByRole('checkbox', { name: t.contribution.groupReceived('Ana') }),
    )

    await waitFor(() => {
      const posted = fetchMock.mock.calls
        .map(([request]) => request as Request)
        .filter((r) => r.method === 'POST')
        .map((r) => new URL(r.url).pathname)
      expect(posted).toEqual([
        '/api/bills/bill-1/contribution/received',
        '/api/loans/loan-1/payments/2026-10/Regular/contribution/received',
      ])
    })
  })

  it('shows a partly received group as neither ticked nor empty', async () => {
    stubApi({
      ...monthApi,
      'GET /api/bills': () => json(aBillMonth([])),
      'GET /api/inputs': () =>
        json(
          aMonth([
            aContributionEntry({ id: 'input-1', isReceived: true, receivedDate: '2026-10-12' }),
            mortgage,
          ]),
        ),
    })
    renderRoute(`/month/${month}`)

    const box = await screen.findByRole('checkbox', { name: t.contribution.groupReceived('Ana') })
    expect(box).not.toBeChecked()
    expect((box as HTMLInputElement).indeterminate).toBe(true)
    expect(screen.getByText(/1 of 2 received/)).toBeInTheDocument()
  })
})

describe('The Shared costs screen', () => {
  it('shows what a participant shares and their outstanding contributions, overdue ones marked', async () => {
    stubApi({
      'GET /api/participants': () => json([ana]),
      'GET /api/shared-costs': () =>
        json(
          aSharedCosts([
            aContribution({ isOverdue: true }),
            aContribution({
              billId: 'bill-2',
              costName: 'Internet',
              isReceived: true,
              receivedDate: '2026-10-10',
            }),
          ]),
        ),
    })
    renderRoute(`/shared-costs/${month}`)

    expect(await screen.findByRole('link', { name: 'Electricity, apartment' })).toHaveAttribute(
      'href',
      '/recurring-bills/recurring-1/edit',
    )
    const outstanding = screen.getByRole('heading', {
      name: new RegExp(t.screen.outstandingList),
    }).parentElement!
    expect(within(outstanding).getByText('Electricity, apartment')).toBeInTheDocument()
    expect(within(outstanding).queryByText('Internet')).not.toBeInTheDocument()
    expect(within(outstanding).getByText(t.screen.overdue)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ana' })).toHaveAttribute(
      'href',
      '/shared-costs/participants/ana/edit',
    )
  })
})
