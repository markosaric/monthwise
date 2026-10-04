import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderRoute } from '@/test/renderRoute'
import { json, stubApi } from '@/test/stubApi'

function stubHealth(status: number, body: unknown) {
  stubApi({
    'GET /api/health': () => json(body, status),
    'GET /api/cards/dashboard': () => json({ hasAnyCards: false, cards: [] }),
  })
}

describe('app shell', () => {
  it('shows healthy API and database status on the dashboard', async () => {
    stubHealth(200, { status: 'Healthy', database: 'Healthy' })

    renderRoute('/')

    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeInTheDocument()
    expect(await screen.findByText('Reachable')).toBeInTheDocument()
    expect(screen.getByText('Healthy', { selector: '[data-slot=badge]' })).toBeInTheDocument()
  })

  it('shows an unhealthy database when the API returns 503', async () => {
    stubHealth(503, { status: 'Unhealthy', database: 'Unhealthy' })

    renderRoute('/')

    expect(await screen.findByText('Reachable')).toBeInTheDocument()
    expect(screen.getByText('Unhealthy')).toBeInTheDocument()
  })

  it('shows the API as unreachable when the request fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )

    renderRoute('/')

    expect(await screen.findByText('Unreachable')).toBeInTheDocument()
  })

  it('navigates between pages', async () => {
    stubHealth(200, { status: 'Healthy', database: 'Healthy' })
    renderRoute('/')

    const [desktopNav] = screen.getAllByRole('navigation', { name: 'Main' })
    await userEvent.click(within(desktopNav!).getByRole('link', { name: 'Cards' }))

    expect(screen.getByRole('heading', { name: 'Cards' })).toBeInTheDocument()
  })
})
