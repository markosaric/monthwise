import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { aCard } from '@/test/cardFixtures'
import {
  aCardPayment,
  aCardPaymentDetails,
  anInstallmentCard,
  aPurchaseDetails,
} from '@/test/cardPurchaseFixtures'
import { aMonth } from '@/test/cashFlowFixtures'
import { aLoanMonth } from '@/test/loanFixtures'
import { renderRoute } from '@/test/renderRoute'
import { json, stubApi } from '@/test/stubApi'
import { installmentAmounts } from './purchaseFormModel'
import { cardPurchaseTexts } from './texts'

const t = cardPurchaseTexts
const tekuci = aCard({ id: 'account-1', name: 'Tekući račun', type: 'Debit', isPrimary: true })
const formLists = {
  'GET /api/card-purchases/cards': () => json([anInstallmentCard()]),
  'GET /api/purchase-categories': () => json([{ id: 'home', name: 'Home', isHidden: false }]),
}

describe('installment amounts', () => {
  it('puts the remaining cents on the last installment', () => {
    expect(installmentAmounts(10000, 3)).toEqual([3333, 3333, 3334])
    expect(installmentAmounts(12000, 12)).toEqual(Array(12).fill(1000))
    expect(installmentAmounts(0, 3)).toEqual([])
  })
})

describe('Purchase form', () => {
  it('records a purchase paid in full by default on the only card', async () => {
    let posted: Record<string, unknown> | null = null
    stubApi({
      ...formLists,
      'POST /api/card-purchases': async (request) => {
        posted = (await request.json()) as Record<string, unknown>
        return json(aPurchaseDetails())
      },
      'GET /api/card-purchases/purchase-1': () => json(aPurchaseDetails()),
    })
    renderRoute('/cards/purchases/new')

    const card = await screen.findByLabelText<HTMLSelectElement>(t.fields.card)
    await waitFor(() => expect(card.value).toBe('card-1'))
    expect(screen.getByLabelText(t.fields.inInstallments)).not.toBeChecked()
    expect(screen.queryByLabelText(t.fields.installmentCount)).not.toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(t.fields.description), 'Groceries')
    await userEvent.type(screen.getByLabelText(t.fields.amount), '45,50')
    await userEvent.click(screen.getByRole('button', { name: t.addPurchase }))

    await waitFor(() => expect(posted).not.toBeNull())
    expect(posted).toMatchObject({
      cardId: 'card-1',
      description: 'Groceries',
      amountCents: 4550,
      inInstallments: false,
      installmentCount: null,
      postponeFirst: false,
      categoryId: null,
    })
  })

  it('previews the installments and checks the card limit', async () => {
    const fetchMock = stubApi(formLists)
    renderRoute('/cards/purchases/new')

    await userEvent.type(await screen.findByLabelText(t.fields.description), 'TV')
    await userEvent.type(screen.getByLabelText(t.fields.amount), '100')
    await userEvent.click(screen.getByLabelText(t.fields.inInstallments))
    const count = screen.getByLabelText(t.fields.installmentCount)
    await userEvent.clear(count)
    await userEvent.type(count, '3')

    expect(screen.getByText(/3 × 33,33 €/)).toHaveTextContent('33,34 €')
    expect(screen.getByLabelText(t.fields.postponeFirst)).toBeInTheDocument()

    await userEvent.clear(count)
    await userEvent.type(count, '24')
    await userEvent.click(screen.getByRole('button', { name: t.addPurchase }))

    expect(await screen.findByText(t.errors.installmentCountInvalid(12))).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([request]) => (request as Request).method === 'POST')).toBe(
      false,
    )
  })

  it('shows API field errors next to their fields', async () => {
    stubApi({
      ...formLists,
      'POST /api/card-purchases': () =>
        json(
          {
            title: 'One or more validation errors occurred.',
            errors: { purchaseDate: ['The purchase date is before the card’s valid from.'] },
          },
          400,
        ),
    })
    renderRoute('/cards/purchases/new')

    await userEvent.type(await screen.findByLabelText(t.fields.description), 'Bike')
    await userEvent.type(screen.getByLabelText(t.fields.amount), '500')
    await userEvent.click(screen.getByRole('button', { name: t.addPurchase }))

    expect(
      await screen.findByText('The purchase date is before the card’s valid from.'),
    ).toBeInTheDocument()
  })
})

describe('Card payments', () => {
  it('shows one summed-up line per card on the Month screen, opening the card payment', async () => {
    stubApi({
      'GET /api/inputs': () => json(aMonth([])),
      'GET /api/loan-payments': () => json(aLoanMonth([])),
      'GET /api/card-payments': () =>
        json({
          month: '2026-10',
          payments: [aCardPayment({ isOverdue: true })],
          totals: { expectedCents: 12000, paidCents: 0, totalCents: 12000 },
        }),
    })
    renderRoute('/month/2026-10')

    const link = await screen.findByRole('link', { name: 'Family Visa' })
    expect(link).toHaveAttribute('href', '/cards/card-1/payments/2026-10')
    const row = link.closest('li')!
    expect(within(row).getByText('120,00 €')).toBeInTheDocument()
    expect(within(row).getByText(t.labels.overdue)).toBeInTheDocument()
  })

  it('asks for the account when ticking Paid, prefilled with the paying account', async () => {
    let posted: Record<string, unknown> | null = null
    stubApi({
      'GET /api/cards': () => json([tekuci]),
      'GET /api/cards/card-1/card-payments/2026-10': () => json(aCardPaymentDetails()),
      'POST /api/cards/card-1/card-payments/2026-10/paid': async (request) => {
        posted = (await request.json()) as Record<string, unknown>
        return json(aCardPayment({ isPaid: true, paidDate: '2026-10-20' }))
      },
    })
    renderRoute('/cards/card-1/payments/2026-10')

    await userEvent.click(await screen.findByRole('checkbox'))
    const account = await screen.findByLabelText<HTMLSelectElement>(t.payment.account)
    expect(account.value).toBe('account-1')
    await userEvent.click(screen.getByRole('button', { name: t.actions.save }))

    await waitFor(() => expect(posted).not.toBeNull())
    expect(posted).toMatchObject({ accountId: 'account-1' })
  })

  it('drills down to the purchases, installments and credits of a card payment', async () => {
    stubApi({ 'GET /api/cards/card-1/card-payments/2026-10': () => json(aCardPaymentDetails()) })
    renderRoute('/cards/card-1/payments/2026-10')

    expect(await screen.findByRole('link', { name: 'Washing machine' })).toHaveAttribute(
      'href',
      '/cards/purchases/purchase-1',
    )
    expect(screen.getByText(/Installment 1 of 3/)).toBeInTheDocument()
    expect(screen.getByText(/Paid in full/)).toBeInTheDocument()
    expect(screen.getByText('-10,00 €')).toBeInTheDocument()
    // The category, not the Home item of the phone navigation.
    expect(within(screen.getByRole('main')).getByText('Home')).toBeInTheDocument()
  })
})

describe('Purchase details', () => {
  it('reverses one installment with a reason', async () => {
    let posted: { url: string; body: unknown } | null = null
    stubApi({
      'GET /api/card-purchases/purchase-1': () => json(aPurchaseDetails()),
      'POST /api/card-purchases/purchase-1/installments/2/reverse': async (request) => {
        posted = { url: new URL(request.url).pathname, body: await request.json() }
        return json(aPurchaseDetails())
      },
    })
    renderRoute('/cards/purchases/purchase-1')

    const reverse = await screen.findAllByRole('button', { name: t.actions.reverseInstallment })
    await userEvent.click(reverse[1]!)
    await userEvent.type(await screen.findByLabelText(t.dialogs.reason), 'cancelled')
    const dialog = screen.getByRole('alertdialog')
    await userEvent.click(
      within(dialog).getByRole('button', { name: t.actions.reverseInstallment }),
    )

    await waitFor(() => expect(posted).not.toBeNull())
    expect(posted).toEqual({
      url: '/api/card-purchases/purchase-1/installments/2/reverse',
      body: { reason: 'cancelled' },
    })
  })

  it('warns before deleting a purchase with charged installments', async () => {
    stubApi({ 'GET /api/card-purchases/purchase-1': () => json(aPurchaseDetails()) })
    renderRoute('/cards/purchases/purchase-1')

    await userEvent.click(await screen.findByRole('button', { name: t.actions.delete }))

    expect(await screen.findByText(t.dialogs.chargedWarning)).toBeInTheDocument()
  })
})
