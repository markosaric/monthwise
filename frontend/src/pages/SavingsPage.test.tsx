import { screen } from '@testing-library/react'
import { renderRoute } from '@/test/renderRoute'
import { json, stubApi } from '@/test/stubApi'

describe('Savings screen', () => {
  it('says the screen is planned and not built yet', async () => {
    stubApi({ 'GET /api/health': () => json({ status: 'Healthy', database: 'Healthy' }) })

    renderRoute('/savings')

    expect(await screen.findByRole('heading', { name: 'Savings' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Planned' })).toHaveTextContent(
      'This screen is planned and not built yet.',
    )
  })
})
