import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderRoute } from '@/test/renderRoute'
import { json, stubApi } from '@/test/stubApi'

// One screen that fails while it is shown (page loading and errors FR-3).
vi.mock('@/pages/SavingsPage', () => ({
  SavingsPage: () => {
    throw new Error('Savings failed')
  },
}))

const menu = () => screen.getByRole('navigation', { name: 'Main' })

describe('a failing screen', () => {
  beforeEach(() => {
    stubApi({ 'GET /api/health': () => json({ status: 'Healthy', database: 'Healthy' }) })
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => vi.restoreAllMocks())

  it('shows a message below the app shell instead of a blank page', async () => {
    renderRoute('/savings')

    expect(
      await screen.findByRole('heading', { name: 'This screen could not be shown' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reload' })).toBeInTheDocument()
    expect(within(menu()).getByRole('link', { name: 'Cards' })).toBeInTheDocument()
  })

  it('shows another screen normally when the user opens it from the menu', async () => {
    renderRoute('/savings')

    await screen.findByRole('navigation', { name: 'Main' })
    await userEvent.click(within(menu()).getByRole('link', { name: 'Cards' }))

    expect(screen.getByRole('heading', { name: 'Cards' })).toBeInTheDocument()
    expect(screen.queryByText('This screen could not be shown')).not.toBeInTheDocument()
  })

  it('still shows "Page not found" for an unknown address', async () => {
    renderRoute('/no-such-screen')

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument()
    expect(menu()).toBeInTheDocument()
  })
})
