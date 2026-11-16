import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { pageReload } from '@/lib/lazyScreen'
import { ScreenError } from './ScreenError'

function Broken(): never {
  throw new Error('secret detail at Broken.tsx:12')
}

function renderBroken(fullPage = false) {
  const router = createMemoryRouter([
    { path: '/', element: <Broken />, errorElement: <ScreenError fullPage={fullPage} /> },
  ])
  return render(<RouterProvider router={router} />)
}

describe('ScreenError', () => {
  let consoleError: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => vi.restoreAllMocks())

  it('offers to reload or go back without showing the error, and logs it', () => {
    renderBroken()

    expect(
      screen.getByRole('heading', { name: 'This screen could not be shown' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reload' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to the dashboard' })).toHaveAttribute('href', '/')
    expect(screen.queryByText(/secret detail/)).not.toBeInTheDocument()
    expect(consoleError).toHaveBeenCalledWith(
      expect.objectContaining({ message: 'secret detail at Broken.tsx:12' }),
    )
  })

  it('reloads the page', async () => {
    const reload = vi.spyOn(pageReload, 'reload').mockImplementation(() => {})
    renderBroken()

    await userEvent.click(screen.getByRole('button', { name: 'Reload' }))

    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('fills the whole page when the app shell failed', () => {
    renderBroken(true)

    expect(
      screen.getByRole('heading', { name: 'This screen could not be shown' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reload' })).toBeInTheDocument()
  })
})
