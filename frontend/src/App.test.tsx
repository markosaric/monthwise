import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MENU_STATE_KEY } from '@/components/layout/menuState'
import { renderRoute } from '@/test/renderRoute'
import { json, stubApi } from '@/test/stubApi'

function stubHealth(status: number, body: unknown) {
  return stubApi({ 'GET /api/health': () => json(body, status) })
}

const menu = () => screen.getByRole('navigation', { name: 'Main' })

describe('app shell', () => {
  beforeEach(() => window.localStorage.clear())

  it('shows no system status while the API and database are healthy', async () => {
    const fetchMock = stubHealth(200, { status: 'Healthy', database: 'Healthy' })

    renderRoute('/')

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
    expect(screen.queryByText(/System status/)).not.toBeInTheDocument()
  })

  it('shows an unhealthy database in the menu and on phones', async () => {
    stubHealth(503, { status: 'Unhealthy', database: 'Unhealthy' })

    renderRoute('/')

    const alerts = await screen.findAllByRole('alert')
    expect(alerts).toHaveLength(2)
    alerts.forEach((alert) => expect(alert).toHaveTextContent('Database unhealthy'))
  })

  it('shows a failed database update in the menu and on phones', async () => {
    stubHealth(503, {
      status: 'Unhealthy',
      database: 'Healthy',
      migrations: 'Failed',
      version: '1.0.0',
    })

    renderRoute('/')

    const alerts = await screen.findAllByRole('alert')
    alerts.forEach((alert) => expect(alert).toHaveTextContent('Database update failed'))
  })

  it('shows the API as unreachable when the request fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )

    renderRoute('/')

    expect((await screen.findAllByRole('alert'))[0]).toHaveTextContent('API unreachable')
  })

  it('collapses and expands the menu with one button and remembers it', async () => {
    stubHealth(200, { status: 'Healthy', database: 'Healthy' })
    const { unmount } = renderRoute('/')

    const toggle = screen.getByRole('button', { name: 'Collapse menu' })
    expect(toggle).toHaveAttribute('aria-expanded', 'true')
    expect(within(menu()).getByText('Month')).toBeInTheDocument()

    await userEvent.click(toggle)

    const expand = screen.getByRole('button', { name: 'Expand menu' })
    expect(expand).toHaveAttribute('aria-expanded', 'false')
    expect(within(menu()).queryByText('Month')).not.toBeInTheDocument()
    expect(within(menu()).getByRole('link', { name: 'Month' })).toBeInTheDocument()
    expect(window.localStorage.getItem(MENU_STATE_KEY)).toBe('collapsed')

    unmount()
    renderRoute('/')
    expect(screen.getByRole('button', { name: 'Expand menu' })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Expand menu' }))
    expect(screen.getByRole('button', { name: 'Collapse menu' })).toBeInTheDocument()
  })

  it('still toggles the menu when storage is unavailable', async () => {
    stubHealth(200, { status: 'Healthy', database: 'Healthy' })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    renderRoute('/')

    await userEvent.click(screen.getByRole('button', { name: 'Collapse menu' }))

    expect(screen.getByRole('button', { name: 'Expand menu' })).toBeInTheDocument()
    vi.restoreAllMocks()
  })

  it('navigates between pages', async () => {
    stubHealth(200, { status: 'Healthy', database: 'Healthy' })
    renderRoute('/')

    await userEvent.click(within(menu()).getByRole('link', { name: 'Cards' }))

    expect(screen.getByRole('heading', { name: 'Cards' })).toBeInTheDocument()
  })
})
