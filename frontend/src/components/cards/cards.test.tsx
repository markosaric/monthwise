import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { cardTypes, type CardStatus } from '@/api/cards'
import { aCard, visaIssuer, zaba } from '@/test/cardFixtures'
import { renderRoute } from '@/test/renderRoute'
import { healthy, json, stubApi } from '@/test/stubApi'
import { CardStatusBadge } from './CardStatusBadge'
import { RevealableNumber } from './RevealableNumber'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { cardTexts } from './texts'

const billing = cardPurchaseTexts.billing

const primaryAccount = aCard({
  id: 'account-1',
  name: 'Tekući račun',
  type: 'Debit',
  isPrimary: true,
  allowsInstallments: false,
})

const lists = {
  'GET /api/card-issuers': () => json([visaIssuer]),
  'GET /api/banks': () => json([zaba]),
  'GET /api/cards': () => json([primaryAccount]),
}

describe('Dashboard cards widget', () => {
  it('asks to set up cards when there are none', async () => {
    stubApi({
      ...healthy,
      'GET /api/cards/dashboard': () => json({ hasAnyCards: false, cards: [] }),
    })
    renderRoute('/')

    expect(await screen.findByText(cardTexts.dashboardNoCards)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: cardTexts.setUpCards })).toHaveAttribute(
      'href',
      '/settings/cards',
    )
  })

  it('says no card is valid when cards exist but none is shown', async () => {
    stubApi({
      ...healthy,
      'GET /api/cards/dashboard': () => json({ hasAnyCards: true, cards: [] }),
    })
    renderRoute('/')

    expect(await screen.findByText(cardTexts.dashboardNoValidCards)).toBeInTheDocument()
  })

  it('lists valid cards with masked numbers', async () => {
    stubApi({
      ...healthy,
      'GET /api/cards/dashboard': () =>
        json({
          hasAnyCards: true,
          cards: [aCard({ isPrimary: true, type: 'Debit', name: 'Tekući' })],
        }),
    })
    renderRoute('/')

    expect(await screen.findByText('Tekući')).toBeInTheDocument()
    expect(screen.getByText('•••• 1111')).toBeInTheDocument()
    expect(screen.getByText(cardTexts.primary)).toBeInTheDocument()
    // Issuer, bank, type, valid through and limit stay off the Dashboard (Appendix 1).
    expect(screen.queryByText(/Visa · ZaBa/)).not.toBeInTheDocument()
    expect(screen.queryByText(/Valid through/)).not.toBeInTheDocument()
    expect(screen.queryByText(/Limit/)).not.toBeInTheDocument()
  })
})

describe('Settings card list', () => {
  it('shows the empty state with an Add card action', async () => {
    stubApi({ 'GET /api/cards': () => json([]) })
    renderRoute('/settings/cards')

    expect(await screen.findByText(cardTexts.emptyList)).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: cardTexts.addCard }).length).toBeGreaterThan(0)
  })

  it('asks the API for the chosen filter', async () => {
    const fetchMock = stubApi({ 'GET /api/cards': () => json([]) })
    renderRoute('/settings/cards')

    await userEvent.click(await screen.findByRole('button', { name: cardTexts.filters.Archived }))

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([request]) =>
          String((request as Request).url).includes('show=Archived'),
        ),
      ).toBe(true),
    )
  })
})

describe('Card form', () => {
  it('shows the primary switch only for debit cards', async () => {
    stubApi(lists)
    renderRoute('/settings/cards/new')

    const type = await screen.findByLabelText(cardTexts.fields.type)
    expect(screen.queryByLabelText(cardTexts.fields.isPrimary)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(cardTexts.fields.allowsInstallments)).not.toBeInTheDocument()

    await userEvent.selectOptions(type, 'Debit')

    expect(screen.getByLabelText(cardTexts.fields.isPrimary)).toBeInTheDocument()
    expect(screen.getByLabelText(cardTexts.fields.allowsInstallments)).toBeInTheDocument()
  })

  it('saves a card without numbers and valid from, with the installments check box', async () => {
    let posted: Record<string, unknown> | null = null
    stubApi({
      ...lists,
      'POST /api/cards': async (request) => {
        posted = (await request.json()) as Record<string, unknown>
        return json(aCard({ id: 'card-new', name: 'Tekući', type: 'Debit' }), 201)
      },
      'GET /api/cards/card-new': () =>
        json(aCard({ id: 'card-new', name: 'Tekući', type: 'Debit' })),
    })
    renderRoute('/settings/cards/new')

    await userEvent.type(await screen.findByLabelText(cardTexts.fields.name), 'Tekući')
    await waitFor(() => expect(screen.getByRole('option', { name: 'Visa' })).toBeInTheDocument())
    await userEvent.selectOptions(screen.getByLabelText(cardTexts.fields.issuer), visaIssuer.id)
    await userEvent.selectOptions(screen.getByLabelText(cardTexts.fields.bank), zaba.id)
    await userEvent.type(screen.getByLabelText(cardTexts.fields.validThrough), '2029-12')
    await userEvent.selectOptions(screen.getByLabelText(cardTexts.fields.type), 'Debit')
    await userEvent.click(screen.getByLabelText(cardTexts.fields.allowsInstallments))
    await userEvent.type(screen.getByLabelText(billing.statementDay), '5')
    await userEvent.type(screen.getByLabelText(billing.paymentDay), '20')
    // The only primary account is prefilled as the paying account (installments B3).
    await waitFor(() =>
      expect(screen.getByLabelText(billing.payingAccount)).toHaveValue('account-1'),
    )
    await userEvent.click(screen.getByRole('button', { name: cardTexts.addCard }))

    await waitFor(() => expect(posted).not.toBeNull())
    expect(posted).toMatchObject({
      name: 'Tekući',
      cardNumber: null,
      iban: null,
      validFrom: null,
      validThrough: '2029-12',
      type: 'Debit',
      allowsInstallments: true,
      statementDay: 5,
      paymentDay: 20,
      payingAccountId: 'account-1',
      paysFromAccountDirectly: false,
      maxInstallments: 36,
    })
  })

  it('asks for a payment date later than the statement date', async () => {
    const fetchMock = stubApi(lists)
    renderRoute('/settings/cards/new')

    await userEvent.type(await screen.findByLabelText(billing.statementDay), '20')
    await userEvent.type(screen.getByLabelText(billing.paymentDay), '20')
    await userEvent.click(screen.getByRole('button', { name: cardTexts.addCard }))

    expect(await screen.findByText(billing.errors.paymentNotAfterStatement)).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([request]) => (request as Request).method === 'POST')).toBe(
      false,
    )
  })

  it('has no billing fields for a debit card without installments', async () => {
    stubApi(lists)
    renderRoute('/settings/cards/new')

    await userEvent.selectOptions(await screen.findByLabelText(cardTexts.fields.type), 'Debit')

    expect(screen.queryByLabelText(billing.statementDay)).not.toBeInTheDocument()
  })

  it('checks a given card number before sending', async () => {
    const fetchMock = stubApi(lists)
    renderRoute('/settings/cards/new')

    await userEvent.type(await screen.findByLabelText(cardTexts.fields.name), 'Family Visa')
    await userEvent.type(screen.getByLabelText(cardTexts.fields.cardNumber), '4111111111111112')
    await userEvent.click(screen.getByRole('button', { name: cardTexts.addCard }))

    expect(await screen.findByText(cardTexts.errors.cardNumberInvalid)).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([request]) => (request as Request).method === 'POST')).toBe(
      false,
    )
  })

  it('shows API field errors next to their fields', async () => {
    stubApi({
      ...lists,
      'POST /api/cards': () =>
        json(
          {
            title: 'One or more validation errors occurred.',
            errors: { name: ['Another card already has this name.'] },
          },
          400,
        ),
    })
    renderRoute('/settings/cards/new')

    await userEvent.type(await screen.findByLabelText(cardTexts.fields.name), 'Family Visa')
    await waitFor(() => expect(screen.getByRole('option', { name: 'Visa' })).toBeInTheDocument())
    await userEvent.selectOptions(screen.getByLabelText(cardTexts.fields.issuer), visaIssuer.id)
    await userEvent.selectOptions(screen.getByLabelText(cardTexts.fields.bank), zaba.id)
    await userEvent.type(screen.getByLabelText(cardTexts.fields.cardNumber), '4111 1111 1111 1111')
    await userEvent.type(screen.getByLabelText(cardTexts.fields.validFrom), '2025-01')
    await userEvent.type(screen.getByLabelText(cardTexts.fields.validThrough), '2029-12')
    await userEvent.type(screen.getByLabelText(billing.statementDay), '5')
    await userEvent.type(screen.getByLabelText(billing.paymentDay), '20')
    await userEvent.click(screen.getByRole('button', { name: cardTexts.addCard }))

    expect(await screen.findByText('Another card already has this name.')).toBeInTheDocument()
  })

  it('adds a new bank from the form', async () => {
    let banks = [zaba]
    stubApi({
      'GET /api/card-issuers': () => json([visaIssuer]),
      'GET /api/banks': () => json(banks),
      'POST /api/banks': async (request) => {
        const { name } = (await request.json()) as { name: string }
        const bank = { id: 'bank-new', name, isPredefined: false }
        banks = [...banks, bank]
        return json(bank)
      },
    })
    renderRoute('/settings/cards/new')

    await waitFor(() => expect(screen.getByRole('option', { name: 'ZaBa' })).toBeInTheDocument())
    await userEvent.selectOptions(
      screen.getByLabelText(cardTexts.fields.bank),
      cardTexts.actions.addNew,
    )
    await userEvent.type(screen.getByPlaceholderText('New bank'), 'Hrvatska poštanska banka')
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByLabelText(cardTexts.fields.bank)).toHaveValue('bank-new')
  })
})

describe('RevealableNumber', () => {
  it('shows the full number only after Reveal and hides it again', async () => {
    stubApi({
      'GET /api/cards/card-1/numbers/CardNumber': () => json({ value: '4111 1111 1111 1111' }),
    })
    render(<RevealableNumber cardId="card-1" kind="CardNumber" masked="•••• 1111" />)

    expect(screen.getByText('•••• 1111')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: cardTexts.actions.reveal }))
    expect(await screen.findByText('4111 1111 1111 1111')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: cardTexts.actions.hide }))
    expect(screen.getByText('•••• 1111')).toBeInTheDocument()
  })
})

describe('CardStatusBadge', () => {
  it.each(Object.keys(cardTexts.status) as CardStatus[])('renders %s', (status) => {
    render(<CardStatusBadge status={status} />)
    expect(screen.getByText(cardTexts.status[status])).toBeInTheDocument()
  })

  it('has a label for every card type', () => {
    expect(cardTypes.every((type) => cardTexts.types[type])).toBe(true)
  })
})
