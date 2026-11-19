import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

// The language is fixed when the texts load, so it is chosen before any app module is imported.
vi.hoisted(() => window.localStorage.setItem('monthwise.language', 'hr'))

const { renderRoute } = await import('@/test/renderRoute')
const { healthy, json, stubApi } = await import('@/test/stubApi')
const { languageReload, setLanguageForTests } = await import('./language')

afterAll(() => window.localStorage.clear())

describe('the app in Croatian (croatian-language.md)', () => {
  it('shows the menu and Settings in Croatian and names the language in every request', async () => {
    const fetchMock = stubApi({
      ...healthy,
      'GET /api/cards': () => json([]),
      'GET /api/payers': () => json([]),
      'GET /api/input-categories': () => json([]),
      'GET /api/expense-categories': () => json([]),
      'GET /api/loan-categories': () => json([]),
      'GET /api/recurring-bills': () => json([]),
      'GET /api/data': () => json({ lastExport: null, lastImport: null }),
    })

    renderRoute('/settings')

    expect(await screen.findByRole('heading', { name: 'Postavke' })).toBeInTheDocument()
    const menu = screen.getByRole('navigation', { name: 'Glavni izbornik' })
    expect(within(menu).getByRole('link', { name: /Pregled/ })).toBeInTheDocument()
    expect(within(menu).getByRole('link', { name: /Krediti/ })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Hrvatski' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'English' })).not.toBeChecked()

    const requests = fetchMock.mock.calls.map(([input]) => input as Request)
    expect(requests.length).toBeGreaterThan(0)
    for (const request of requests) {
      expect(request.headers.get('Accept-Language')).toBe('hr')
    }
  })

  it('switches back to English: stores the choice and reloads the page', async () => {
    stubApi({ ...healthy })
    const reload = vi.spyOn(languageReload, 'reload').mockImplementation(() => {})

    renderRoute('/settings')
    await userEvent.click(await screen.findByRole('radio', { name: 'English' }))

    expect(window.localStorage.getItem('monthwise.language')).toBe('en')
    expect(reload).toHaveBeenCalledOnce()
  })

  it('does not reload when the active language is chosen again', async () => {
    // The previous test switched to English without a real reload.
    setLanguageForTests('hr')
    stubApi({ ...healthy })
    const reload = vi.spyOn(languageReload, 'reload').mockImplementation(() => {})

    renderRoute('/settings')
    await userEvent.click(await screen.findByRole('radio', { name: 'Hrvatski' }))

    expect(reload).not.toHaveBeenCalled()
  })

  it('shows the planned Savings screen in Croatian', async () => {
    stubApi({ ...healthy })

    renderRoute('/savings')

    expect(await screen.findByRole('heading', { name: 'Štednja' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Planirano' })).toHaveTextContent(
      'Ovaj je zaslon planiran i još nije izrađen.',
    )
  })
})
