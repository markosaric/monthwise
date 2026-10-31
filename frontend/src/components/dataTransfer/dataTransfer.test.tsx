import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { DataOverview } from '@/api/dataTransfer'
import { aDashboard } from '@/test/dashboardFixtures'
import { renderRoute } from '@/test/renderRoute'
import { healthy, json, stubApi, type ApiHandlers } from '@/test/stubApi'
import { dataTexts as t } from './texts'

function anOverview(overrides: Partial<DataOverview> = {}): DataOverview {
  return {
    lastExport: null,
    lastImport: null,
    safetyCopyCreatedAt: null,
    reminder: {
      enabled: true,
      intervalDays: 30,
      intervalChoices: [7, 14, 30, 60, 90],
      isDue: true,
    },
    canManage: true,
    ...overrides,
  }
}

const summary = { cards: 3, loans: 1, inputs: 40, cardPurchases: 5, bills: 12, expenses: 30 }

function stubData(overview = anOverview(), extra: ApiHandlers = {}) {
  return stubApi({
    ...healthy,
    'GET /api/data': () => json(overview),
    'GET /api/data/history': () => json({ items: [], page: 1, pageSize: 50, totalCount: 0 }),
    ...extra,
  })
}

describe('Data and backups', () => {
  it('has a tile in a Data group on the Settings screen with the age of the last export', async () => {
    stubData(
      anOverview({
        lastExport: {
          id: 'e1',
          occurredAt: new Date().toISOString(),
          kind: 'Export',
          userName: null,
          succeeded: true,
          fileName: 'monthwise-export.monthwise',
          release: '1.0.0',
          fileCreatedAt: null,
          message: null,
        },
      }),
    )
    renderRoute('/settings')

    const group = await screen.findByRole('region', { name: t.group })
    const tile = within(group).getByRole('link', { name: new RegExp(t.section) })
    expect(tile).toHaveAttribute('href', '/settings/data')
    expect(await within(tile).findByText('Last export today')).toBeInTheDocument()
  })

  it('shows "No export yet" before the first export', async () => {
    stubData()
    renderRoute('/settings')

    expect(await screen.findByText(t.noExport)).toBeInTheDocument()
  })

  it('exports with a password of at least 6 characters entered twice', async () => {
    const user = userEvent.setup()
    let sent: unknown = null
    vi.stubGlobal(
      'URL',
      Object.assign(URL, { createObjectURL: () => 'blob:x', revokeObjectURL: () => {} }),
    )
    stubData(anOverview(), {
      'POST /api/data/export': async (request) => {
        sent = await request.json()
        return new Response(new Blob(['file']), {
          headers: {
            'Content-Disposition':
              "attachment; filename*=UTF-8''monthwise-export-2026-10-31-0815.monthwise",
          },
        })
      },
    })
    renderRoute('/settings/data')

    await user.click(await screen.findByRole('button', { name: t.export.action }))
    await user.type(screen.getByLabelText(t.export.password), '12345')
    await user.type(screen.getByLabelText(t.export.repeat), '12345')
    await user.click(screen.getByRole('button', { name: t.export.confirm }))
    expect(screen.getByRole('alert')).toHaveTextContent(t.export.tooShort)
    expect(sent).toBeNull()

    await user.type(screen.getByLabelText(t.export.password), '6')
    await user.type(screen.getByLabelText(t.export.repeat), '7')
    await user.click(screen.getByRole('button', { name: t.export.confirm }))
    expect(screen.getByRole('alert')).toHaveTextContent(t.export.mismatch)

    await user.clear(screen.getByLabelText(t.export.repeat))
    await user.type(screen.getByLabelText(t.export.repeat), '123456')
    await user.click(screen.getByRole('button', { name: t.export.confirm }))
    await waitFor(() => expect(sent).toEqual({ password: '123456' }))
  })

  it('checks the file, compares it with the current data and imports only after typing REPLACE', async () => {
    const user = userEvent.setup()
    // The raw multipart body: request.formData() cannot parse a jsdom File on Node 24 (CI).
    const imported: string[] = []
    stubData(anOverview(), {
      'POST /api/data/import/check': () =>
        json({
          fileCreatedAt: '2026-10-12T08:15:00Z',
          fileRelease: '1.0.0',
          file: summary,
          current: { ...summary, cards: 4 },
        }),
      'POST /api/data/import': async (request) => {
        imported.push(await request.text())
        return json({ fileCreatedAt: '2026-10-12T08:15:00Z', summary })
      },
    })
    renderRoute('/settings/data')

    await user.click(await screen.findByRole('button', { name: t.import.action }))
    await user.upload(
      screen.getByLabelText(t.import.file),
      new File(['x'], 'monthwise-export.monthwise'),
    )
    await user.type(screen.getByLabelText(t.import.password), 'secret1')
    await user.click(screen.getByRole('button', { name: t.import.check }))

    expect(await screen.findByText(t.import.compareTitle)).toBeInTheDocument()
    const cardsRow = screen.getByRole('row', { name: new RegExp(t.summary.cards) })
    expect(cardsRow).toHaveTextContent('34')
    const replace = screen.getByRole('button', { name: t.import.confirm })
    expect(replace).toBeDisabled()

    await user.type(screen.getByLabelText(t.import.typeWord(t.confirmationWord)), 'REPLACE')
    await user.click(replace)
    await waitFor(() => expect(imported).toHaveLength(1))
    expect(imported[0]).toMatch(/name="confirmation"\r\n\r\nREPLACE\r\n/)
    expect(imported[0]).toMatch(/name="password"\r\n\r\nsecret1\r\n/)
    // Under jsdom the file part is an unnamed blob (empty on Node 24); a browser sends the file.
    expect(imported[0]).toMatch(/name="file"; filename="/)
  })

  it('changes the reminder setting', async () => {
    const user = userEvent.setup()
    const saved: unknown[] = []
    stubData(anOverview(), {
      'PUT /api/data/reminder': async (request) => {
        saved.push(await request.json())
        return json({
          enabled: true,
          intervalDays: 7,
          intervalChoices: [7, 14, 30, 60, 90],
          isDue: true,
        })
      },
    })
    renderRoute('/settings/data')

    await user.selectOptions(await screen.findByLabelText(t.reminder.interval), '7')
    await waitFor(() => expect(saved).toEqual([{ enabled: true, intervalDays: 7 }]))
  })
})

describe('Export reminder on the Dashboard', () => {
  function stubDashboard(overview: DataOverview, extra: ApiHandlers = {}) {
    return stubData(overview, {
      'GET /api/dashboard': () => json(aDashboard()),
      'GET /api/card-purchases/cards': () => json([]),
      ...extra,
    })
  }

  it('shows the reminder while it is due and dismisses it', async () => {
    const user = userEvent.setup()
    let dismissed = false
    stubDashboard(anOverview(), {
      'POST /api/data/reminder/dismiss': () => {
        dismissed = true
        return json({
          enabled: true,
          intervalDays: 30,
          intervalChoices: [7, 14, 30, 60, 90],
          isDue: false,
        })
      },
    })
    renderRoute('/')

    const reminder = await screen.findByRole('region', { name: t.reminder.dashboardTitle })
    expect(reminder).toHaveTextContent('You have not exported your data yet.')
    expect(within(reminder).getByRole('link', { name: t.reminder.open })).toHaveAttribute(
      'href',
      '/settings/data',
    )
    await user.click(within(reminder).getByRole('button', { name: t.reminder.dismiss }))
    await waitFor(() => expect(dismissed).toBe(true))
  })

  it('shows nothing when the reminder is not due', async () => {
    stubDashboard(anOverview({ reminder: { ...anOverview().reminder, isDue: false } }))
    renderRoute('/')

    await screen.findByRole('heading', { name: 'October 2026' })
    expect(
      screen.queryByRole('region', { name: t.reminder.dashboardTitle }),
    ).not.toBeInTheDocument()
  })
})
