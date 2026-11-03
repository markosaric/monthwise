import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { PurchaseForm } from '@/components/cardPurchases/PurchaseForm'
import { emptyPurchaseFormValues } from '@/components/cardPurchases/purchaseFormModel'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { cardTexts } from '@/components/cards/texts'
import { payableAccounts, payableCreditCards } from '@/components/expenses/paidFrom'
import { payingAccounts } from '@/components/loans/loanFormModel'
import { aCard, visaIssuer, zaba } from '@/test/cardFixtures'
import { renderRoute } from '@/test/renderRoute'
import { account, aBill, aRevolvingCard, aRevolvingListCard } from '@/test/revolvingFixtures'
import { healthy, json, stubApi } from '@/test/stubApi'
import { RevolvingBillRow } from './RevolvingBillRow'
import { revolvingTexts } from './texts'

const t = revolvingTexts

function withClient(children: ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>)
}

describe('Card form for a revolving card', () => {
  const lists = {
    ...healthy,
    'GET /api/card-issuers': () => json([visaIssuer]),
    'GET /api/banks': () => json([zaba]),
    'GET /api/cards': () => json([account]),
  }

  it('shows the revolving terms instead of the primary, installments and billing fields', async () => {
    stubApi(lists)
    renderRoute('/settings/cards/new')

    await userEvent.selectOptions(await screen.findByLabelText(cardTexts.fields.type), 'Revolving')

    expect(screen.getByLabelText(t.form.minPaymentPercent)).toBeInTheDocument()
    expect(screen.getByLabelText(t.form.billDay)).toBeInTheDocument()
    expect(screen.getByLabelText(t.form.startingDebt)).toBeInTheDocument()
    expect(screen.queryByLabelText(cardTexts.fields.isPrimary)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(cardTexts.fields.allowsInstallments)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(cardPurchaseTexts.billing.statementDay)).not.toBeInTheDocument()
    // The only primary account is prefilled as the paying account.
    await waitFor(() =>
      expect(screen.getByLabelText(t.form.payingAccount)).toHaveValue('account-1'),
    )
  })

  it('asks for the limit and valid terms before sending', async () => {
    const fetchMock = stubApi(lists)
    renderRoute('/settings/cards/new')

    await userEvent.selectOptions(await screen.findByLabelText(cardTexts.fields.type), 'Revolving')
    await userEvent.type(screen.getByLabelText(t.form.minPaymentPercent), '8,333')
    await userEvent.type(screen.getByLabelText(t.form.billDay), '32')
    await userEvent.click(screen.getByRole('button', { name: cardTexts.addCard }))

    expect(await screen.findByText(t.form.errors.debtLimitRequired)).toBeInTheDocument()
    expect(screen.getByText(t.form.errors.minPaymentPercentInvalid)).toBeInTheDocument()
    expect(screen.getAllByText(t.form.errors.dayInvalid).length).toBeGreaterThan(0)
    expect(fetchMock).not.toHaveBeenCalledWith(
      expect.objectContaining({ method: 'POST' }),
      expect.anything(),
    )
  })

  it('sends percentages as basis points and the starting debt in cents', async () => {
    let posted: Record<string, unknown> | null = null
    stubApi({
      ...lists,
      'POST /api/cards': async (request) => {
        posted = (await request.json()) as Record<string, unknown>
        return json(aRevolvingCard({ id: 'card-new' }), 201)
      },
      'GET /api/cards/card-new': () => json(aRevolvingCard({ id: 'card-new' })),
    })
    renderRoute('/settings/cards/new')

    await userEvent.type(await screen.findByLabelText(cardTexts.fields.name), 'Revolving Visa')
    await waitFor(() => expect(screen.getByRole('option', { name: 'Visa' })).toBeInTheDocument())
    await userEvent.selectOptions(screen.getByLabelText(cardTexts.fields.issuer), visaIssuer.id)
    await userEvent.selectOptions(screen.getByLabelText(cardTexts.fields.bank), zaba.id)
    await userEvent.type(screen.getByLabelText(cardTexts.fields.validThrough), '1229')
    await userEvent.selectOptions(screen.getByLabelText(cardTexts.fields.type), 'Revolving')
    await userEvent.type(screen.getByLabelText(cardTexts.fields.debtLimit), '1.500')
    await userEvent.type(screen.getByLabelText(t.form.minPaymentPercent), '8,33')
    await userEvent.type(screen.getByLabelText(t.form.minPaymentAmount), '13,27')
    await userEvent.type(screen.getByLabelText(t.form.interestPercent), '12,75')
    await userEvent.type(screen.getByLabelText(t.form.billDay), '5')
    await userEvent.type(screen.getByLabelText(t.form.dueDay), '20')
    await userEvent.type(screen.getByLabelText(t.form.startingDebt), '1000')
    await userEvent.clear(screen.getByLabelText(t.form.startingDebtDate))
    await userEvent.type(screen.getByLabelText(t.form.startingDebtDate), '2026-09-01')
    await waitFor(() =>
      expect(screen.getByLabelText(t.form.payingAccount)).toHaveValue('account-1'),
    )
    await userEvent.click(screen.getByRole('button', { name: cardTexts.addCard }))

    await waitFor(() => expect(posted).not.toBeNull())
    expect(posted).toMatchObject({
      type: 'Revolving',
      isPrimary: false,
      debtLimitCents: 150_000,
      minPaymentBasisPoints: 833,
      minPaymentAmountCents: 1327,
      interestBasisPoints: 1275,
      minPaymentInterestBasisPoints: null,
      billDay: 5,
      dueDay: 20,
      payingAccountId: 'account-1',
      statementDay: null,
      maxInstallments: null,
      startingDebtCents: 100_000,
      startingDebtDate: '2026-09-01',
    })
  })
})

describe('Extra payment form', () => {
  function stubPayment(onPost?: (body: unknown) => void) {
    return stubApi({
      ...healthy,
      'GET /api/cards': () => json([account, aRevolvingCard()]),
      'GET /api/revolving-cards': () =>
        json({ cards: [aRevolvingListCard()], totalDebtCents: 100_000 }),
      'GET /api/revolving-cards/revolving-1/debt': () =>
        json({ cardId: 'revolving-1', date: '2026-11-03', debtCents: 100_000 }),
      'POST /api/revolving-cards/revolving-1/extra-payments': async (request) => {
        onPost?.(await request.json())
        return json({}, 201)
      },
    })
  }

  it('fills in the whole debt and prefills the paying account', async () => {
    let posted: Record<string, unknown> | null = null
    stubPayment((body) => (posted = body as Record<string, unknown>))
    renderRoute('/cards/revolving/revolving-1/pay')

    await waitFor(() =>
      expect(screen.getByRole('button', { name: t.payment.payWholeDebt })).toBeEnabled(),
    )
    expect(screen.getByLabelText(t.payment.account)).toHaveValue('account-1')
    await userEvent.click(screen.getByRole('button', { name: t.payment.payWholeDebt }))
    expect(screen.getByLabelText(t.payment.amount)).toHaveValue('1000')

    await userEvent.click(screen.getByRole('button', { name: t.payment.save }))
    await waitFor(() => expect(posted).not.toBeNull())
    expect(posted).toMatchObject({ accountId: 'account-1', amountCents: 100_000 })
  })

  it('refuses an amount over the debt and a date in the future', async () => {
    const fetchMock = stubPayment()
    renderRoute('/cards/revolving/revolving-1/pay')

    await waitFor(() =>
      expect(screen.getByRole('button', { name: t.payment.payWholeDebt })).toBeEnabled(),
    )
    await userEvent.type(screen.getByLabelText(t.payment.amount), '2000')
    await userEvent.clear(screen.getByLabelText(t.payment.date))
    await userEvent.type(screen.getByLabelText(t.payment.date), '2999-01-01')
    await userEvent.click(screen.getByRole('button', { name: t.payment.save }))

    expect(await screen.findByText(t.payment.errors.dateInFuture)).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalledWith(
      expect.objectContaining({ method: 'POST' }),
      expect.anything(),
    )
  })

  it('refuses an amount over the debt', async () => {
    stubPayment()
    renderRoute('/cards/revolving/revolving-1/pay')

    await waitFor(() =>
      expect(screen.getByRole('button', { name: t.payment.payWholeDebt })).toBeEnabled(),
    )
    await userEvent.type(screen.getByLabelText(t.payment.amount), '2000')
    await userEvent.click(screen.getByRole('button', { name: t.payment.save }))

    expect(await screen.findByText(t.payment.errors.overDebt)).toBeInTheDocument()
  })
})

describe('Revolving bill row', () => {
  it('asks for the amount, prefilled with the minimum, when ticked', async () => {
    let posted: Record<string, unknown> | null = null
    stubApi({
      'GET /api/cards': () => json([account]),
      'POST /api/revolving-cards/revolving-1/bills/2026-10/paid': async (request) => {
        posted = (await request.json()) as Record<string, unknown>
        return json(aBill({ isPaid: true }))
      },
    })
    withClient(<RevolvingBillRow bill={aBill()} showCard={false} />)

    await userEvent.click(screen.getByRole('checkbox', { name: /Paid: Revolving Visa 2026-10/ }))

    expect(await screen.findByLabelText(t.bill.amount)).toHaveValue('84,18')
    await waitFor(() =>
      expect(screen.getByRole('option', { name: account.name })).toBeInTheDocument(),
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(posted).not.toBeNull())
    expect(posted).toMatchObject({ accountId: 'account-1', amountCents: 8_418 })
  })

  it('cannot tick an estimated bill', () => {
    stubApi({})
    withClient(<RevolvingBillRow bill={aBill({ isEstimated: true })} showCard={false} />)

    expect(screen.getByRole('checkbox', { name: /Paid: Revolving Visa 2026-10/ })).toBeDisabled()
    expect(screen.getByText(t.bill.estimated)).toBeInTheDocument()
  })
})

describe('Paid from lists', () => {
  const credit = aCard({ id: 'credit-1', name: 'Family Visa', type: 'Credit' })
  const revolving = aRevolvingCard()
  const cards = [account, credit, revolving]

  it('list revolving cards with the credit cards and never with the accounts', () => {
    expect(payableAccounts(cards).map((c) => c.id)).toEqual(['account-1'])
    expect(payableCreditCards(cards).map((c) => c.id)).toEqual(['credit-1', 'revolving-1'])
    expect(payingAccounts(cards).map((c) => c.id)).not.toContain('revolving-1')
  })
})

describe('Purchase form', () => {
  it('has no installments for a revolving card', async () => {
    stubApi({ 'GET /api/expense-categories': () => json([]) })
    withClient(
      <PurchaseForm
        cards={[
          { id: 'revolving-1', name: 'Revolving Visa', maxInstallments: null, isRevolving: true },
        ]}
        defaultValues={emptyPurchaseFormValues('revolving-1', '2026-11-03')}
        submitLabel="Save"
        onSubmit={async () => {}}
        onCancel={() => {}}
      />,
    )

    expect(await screen.findByText(cardPurchaseTexts.hints.revolving)).toBeInTheDocument()
    expect(screen.queryByLabelText(cardPurchaseTexts.fields.inInstallments)).not.toBeInTheDocument()
  })
})
