import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { HandCoins, Users } from 'lucide-react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { StatCard, StatGrid } from './StatCard'
import { LONG_PRESS_MS } from './useLongPress'

function renderFigures() {
  render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route
          path="/"
          element={
            <StatGrid label="Key figures">
              <StatCard
                icon={HandCoins}
                color="red"
                label="Outstanding"
                value="427,69 €"
                detail="2 overdue"
                to="/shared"
              />
              <StatCard icon={Users} color="red" label="Participants" value={1} />
            </StatGrid>
          }
        />
        <Route path="/shared" element={<p>Shared costs screen</p>} />
      </Routes>
    </MemoryRouter>,
  )
  return screen.getByRole('region', { name: 'Key figures' })
}

describe('Key figure labels as icon tooltips', () => {
  afterEach(() => vi.useRealTimers())

  it('reads the label before the number and shows the detail alone under it', () => {
    const figures = renderFigures()

    const linked = within(figures).getByRole('link', { name: /Outstanding/ })
    expect(linked).toHaveTextContent(/^Outstanding 427,69 €2 overdue$/)
    expect(within(figures).getByText('Outstanding')).toHaveClass('sr-only')
    expect(within(figures).getByText('Participants')).toHaveClass('sr-only')
    expect(screen.queryByText(/·/)).not.toBeInTheDocument()
  })

  it('shows the label of a linked figure when the card gets keyboard focus', async () => {
    renderFigures()

    act(() => screen.getByRole('link', { name: /Outstanding/ }).focus())
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Outstanding')
  })

  it('shows no label when the card is focused by a click', () => {
    renderFigures()

    const link = screen.getByRole('link', { name: /Outstanding/ })
    fireEvent.pointerDown(link, { pointerType: 'mouse' })
    act(() => link.focus())
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument()
  })

  it('lets the icon of a figure that opens nothing take the focus and show its label', async () => {
    renderFigures()

    const icon = screen.getByText('Participants').parentElement!.querySelector('[tabindex="0"]')
    expect(icon).toHaveAccessibleName('Participants')
    act(() => (icon as HTMLElement).focus())
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Participants')
  })

  it('shows the label on a long press of the icon without opening the screen', () => {
    vi.useFakeTimers()
    renderFigures()

    const link = screen.getByRole('link', { name: /Outstanding/ })
    const icon = link.querySelector('[data-state]')!
    fireEvent.pointerDown(icon, { pointerType: 'touch' })
    act(() => vi.advanceTimersByTime(LONG_PRESS_MS))
    fireEvent.pointerUp(icon, { pointerType: 'touch' })
    fireEvent.click(icon)

    expect(screen.getByRole('tooltip')).toHaveTextContent('Outstanding')
    expect(screen.queryByText('Shared costs screen')).not.toBeInTheDocument()
  })

  it('opens the screen on a short tap of the icon', () => {
    vi.useFakeTimers()
    renderFigures()

    const icon = screen.getByRole('link', { name: /Outstanding/ }).querySelector('[data-state]')!
    fireEvent.pointerDown(icon, { pointerType: 'touch' })
    act(() => vi.advanceTimersByTime(100))
    fireEvent.pointerUp(icon, { pointerType: 'touch' })
    fireEvent.click(icon)

    expect(screen.getByText('Shared costs screen')).toBeInTheDocument()
  })
})
