import { act, render, screen } from '@testing-library/react'
import type { ComponentType } from 'react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { ScreenError } from '@/components/layout/ScreenError'
import { LOADING_SIGN_DELAY_MS } from '@/components/layout/ScreenLoading'
import { lazyScreen, pageReload, RELOAD_FLAG_KEY, type LazyScreen } from './lazyScreen'

function Hello() {
  return <h1>Hello screen</h1>
}

function renderScreen(Screen: LazyScreen) {
  const router = createMemoryRouter([
    { path: '/', element: <Screen />, errorElement: <ScreenError /> },
  ])
  return render(<RouterProvider router={router} />)
}

function deferred() {
  let resolve!: (component: ComponentType) => void
  let reject!: (error: unknown) => void
  const promise = new Promise<ComponentType>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

const moduleError = () => new TypeError('Failed to fetch dynamically imported module')

describe('lazyScreen', () => {
  let reload: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    window.sessionStorage.clear()
    reload = vi.spyOn(pageReload, 'reload').mockImplementation(() => {})
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('shows the loading sign only after a moment while the screen downloads', async () => {
    vi.useFakeTimers()
    const Screen = lazyScreen(() => new Promise<ComponentType>(() => {}))

    renderScreen(Screen)
    await act(() => vi.advanceTimersByTimeAsync(LOADING_SIGN_DELAY_MS - 50))
    expect(screen.queryByRole('status')).not.toBeInTheDocument()

    await act(() => vi.advanceTimersByTimeAsync(50))
    expect(screen.getByRole('status')).toHaveTextContent('Loading…')
  })

  it('shows the screen once downloaded and at once the next time', async () => {
    const load = deferred()
    const loader = vi.fn(() => load.promise)
    const Screen = lazyScreen(loader)

    const { unmount } = renderScreen(Screen)
    load.resolve(Hello)
    expect(await screen.findByRole('heading', { name: 'Hello screen' })).toBeInTheDocument()
    unmount()

    renderScreen(Screen)
    expect(screen.getByRole('heading', { name: 'Hello screen' })).toBeInTheDocument()
    expect(loader).toHaveBeenCalledTimes(1)
  })

  it('reloads the page once when the screen cannot be downloaded', async () => {
    const Screen = lazyScreen(() => Promise.reject(moduleError()))

    renderScreen(Screen)

    await vi.waitFor(() => expect(reload).toHaveBeenCalledTimes(1))
    expect(window.sessionStorage.getItem(RELOAD_FLAG_KEY)).toBe('1')
    expect(screen.queryByText('This screen could not be shown')).not.toBeInTheDocument()
  })

  it('shows the error message instead of reloading again after the reload', async () => {
    window.sessionStorage.setItem(RELOAD_FLAG_KEY, '1')
    const Screen = lazyScreen(() => Promise.reject(moduleError()))

    renderScreen(Screen)

    expect(await screen.findByText('This screen could not be shown')).toBeInTheDocument()
    expect(reload).not.toHaveBeenCalled()
  })

  it('shows the error message without reloading when session storage is unavailable', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    const Screen = lazyScreen(() => Promise.reject(moduleError()))

    renderScreen(Screen)

    expect(await screen.findByText('This screen could not be shown')).toBeInTheDocument()
    expect(reload).not.toHaveBeenCalled()
  })

  it('clears the reload flag when a screen downloads', async () => {
    window.sessionStorage.setItem(RELOAD_FLAG_KEY, '1')
    const Screen = lazyScreen(() => Promise.resolve(Hello))

    await Screen.preload()

    expect(window.sessionStorage.getItem(RELOAD_FLAG_KEY)).toBeNull()
  })

  it('tries the download again the next time after a failure', async () => {
    window.sessionStorage.setItem(RELOAD_FLAG_KEY, '1')
    const loader = vi
      .fn<() => Promise<ComponentType>>()
      .mockRejectedValueOnce(moduleError())
      .mockResolvedValueOnce(Hello)
    const Screen = lazyScreen(loader)

    const { unmount } = renderScreen(Screen)
    expect(await screen.findByText('This screen could not be shown')).toBeInTheDocument()
    unmount()

    renderScreen(Screen)
    expect(await screen.findByRole('heading', { name: 'Hello screen' })).toBeInTheDocument()
  })
})
