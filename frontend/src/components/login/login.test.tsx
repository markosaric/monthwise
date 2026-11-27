import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderRoute } from '@/test/renderRoute'
import { healthy, json, signedInAdmin, stubApi, type ApiHandlers } from '@/test/stubApi'
import { loginTexts } from './texts'

const t = loginTexts

/** Who is signed in can change while the test runs: `state.current` is what the API answers. */
function authState(initial: unknown) {
  const state = { current: initial }
  const handlers: ApiHandlers = { 'GET /api/auth/state': () => json(state.current) }
  return { state, handlers }
}

const member = {
  ...signedInAdmin,
  id: 'user-ivo',
  displayName: 'Ivo',
  username: 'ivo',
  role: 'Member' as const,
}

describe('login', () => {
  it('asks for the setup code while there are no users and signs the new admin in', async () => {
    const { state, handlers } = authState({ setupNeeded: true, user: null })
    let body: unknown
    stubApi({
      ...handlers,
      ...healthy,
      'POST /api/auth/setup': async (request) => {
        body = await request.json()
        state.current = { setupNeeded: false, user: signedInAdmin }
        return new Response(null, { status: 204 })
      },
    })
    renderRoute('/')

    expect(await screen.findByRole('heading', { name: t.setup.title })).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(t.setup.setupCode), 'ABCD-EFGH')
    await userEvent.type(screen.getByLabelText(t.setup.displayName), 'Ana')
    await userEvent.type(screen.getByLabelText(t.setup.username), 'ana')
    await userEvent.type(screen.getByLabelText(t.setup.password), 'a long password')
    await userEvent.type(screen.getByLabelText(t.setup.repeatPassword), 'a long passwort')
    await userEvent.click(screen.getByRole('button', { name: t.setup.submit }))
    expect(await screen.findByText(t.errors.passwordsDiffer)).toBeInTheDocument()

    await userEvent.clear(screen.getByLabelText(t.setup.repeatPassword))
    await userEvent.type(screen.getByLabelText(t.setup.repeatPassword), 'a long password')
    await userEvent.click(screen.getByRole('button', { name: t.setup.submit }))

    expect(await screen.findByRole('navigation', { name: 'Main' })).toBeInTheDocument()
    expect(body).toEqual({
      setupCode: 'ABCD-EFGH',
      displayName: 'Ana',
      username: 'ana',
      password: 'a long password',
    })
  })

  it('signs in and then shows the screen that was asked for', async () => {
    const { state, handlers } = authState({ setupNeeded: false, user: null })
    const fetchMock = stubApi({
      ...handlers,
      ...healthy,
      'GET /api/cards': () => json([]),
      'POST /api/auth/sign-in': async (request) => {
        const { password } = (await request.json()) as { password: string }
        if (password !== 'the right password') {
          return json({ errors: { '': ['The username or password is not right.'] } }, 400)
        }
        state.current = { setupNeeded: false, user: signedInAdmin }
        return new Response(null, { status: 204 })
      },
    })
    renderRoute('/cards')

    expect(await screen.findByRole('heading', { name: t.signIn.title })).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Main' })).not.toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(t.signIn.username), 'ana')
    await userEvent.type(screen.getByLabelText(t.signIn.password), 'a wrong password')
    await userEvent.click(screen.getByRole('button', { name: t.signIn.submit }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The username or password is not right.',
    )

    await userEvent.clear(screen.getByLabelText(t.signIn.password))
    await userEvent.type(screen.getByLabelText(t.signIn.password), 'the right password')
    await userEvent.click(screen.getByLabelText(t.signIn.staySignedIn))
    await userEvent.click(screen.getByRole('button', { name: t.signIn.submit }))

    expect(await screen.findByRole('heading', { name: 'Cards' })).toBeInTheDocument()
    const signIn = fetchMock.mock.calls
      .map(([input]) => input as Request)
      .filter((r) => r.url.endsWith('/api/auth/sign-in'))
    expect(signIn.at(-1)?.headers.get('X-Monthwise-Request')).toBe('1')
  })

  it('asks for a new password first after signing in with a temporary one', async () => {
    const { state, handlers } = authState({
      setupNeeded: false,
      user: { ...member, mustChangePassword: true },
    })
    stubApi({
      ...handlers,
      ...healthy,
      'POST /api/auth/password': () => {
        state.current = { setupNeeded: false, user: member }
        return new Response(null, { status: 204 })
      },
    })
    renderRoute('/')

    expect(await screen.findByRole('heading', { name: t.password.forcedTitle })).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(t.password.currentTemporary), 'temporary password')
    await userEvent.type(screen.getByLabelText(t.password.new), 'my own password')
    await userEvent.type(screen.getByLabelText(t.password.repeat), 'my own password')
    await userEvent.click(screen.getByRole('button', { name: t.password.submit }))

    expect(await screen.findByRole('navigation', { name: 'Main' })).toBeInTheDocument()
  })

  it('shows the sign-in screen when a call answers that the session has ended', async () => {
    const { state, handlers } = authState({ setupNeeded: false, user: signedInAdmin })
    stubApi({
      ...handlers,
      ...healthy,
      'GET /api/cards': () => {
        state.current = { setupNeeded: false, user: null }
        return json({ title: 'Sign in to continue.' }, 401)
      },
    })
    renderRoute('/settings/cards')

    expect(await screen.findByRole('heading', { name: t.signIn.title })).toBeInTheDocument()
  })

  it('signs out from Settings, where the signed-in user is shown', async () => {
    const { state, handlers } = authState({ setupNeeded: false, user: signedInAdmin })
    const signOut = vi.fn(() => {
      state.current = { setupNeeded: false, user: null }
      return new Response(null, { status: 204 })
    })
    stubApi({ ...handlers, ...healthy, 'POST /api/auth/sign-out': signOut })
    renderRoute('/settings')

    const account = await screen.findByRole('region', { name: t.account.title })
    expect(within(account).getByText(t.account.signedInAs('Signed-in admin'))).toBeInTheDocument()
    await userEvent.click(within(account).getByRole('button', { name: t.account.signOut }))

    expect(await screen.findByRole('heading', { name: t.signIn.title })).toBeInTheDocument()
    expect(signOut).toHaveBeenCalledOnce()
  })
})

describe('Users screen', () => {
  const users = [
    signedInAdmin,
    { ...member, mustChangePassword: true },
    { ...member, id: 'user-eva', displayName: 'Eva', username: 'eva', isActive: false },
  ]

  it('lets a household admin add, edit, reset and delete users', async () => {
    stubApi({ ...healthy, 'GET /api/users': () => json({ users, canManage: true }) })
    renderRoute('/settings/users')

    expect(await screen.findByText('Ivo')).toBeInTheDocument()
    expect(screen.getByText(/ivo · Member · Must choose a password/)).toBeInTheDocument()
    expect(screen.getByText(/eva · Member · Inactive/)).toBeInTheDocument()
    expect(screen.getByText(`(${t.users.you})`)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: t.users.add })).toHaveAttribute(
      'href',
      '/settings/users/new',
    )
    expect(
      screen.getByRole('button', { name: `${t.users.resetPassword}: Ivo` }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: `${t.users.remove}: Eva` })).toBeInTheDocument()
  })

  it('shows a member only themself, without actions', async () => {
    stubApi({
      ...healthy,
      'GET /api/auth/state': () => json({ setupNeeded: false, user: member }),
      'GET /api/users': () => json({ users: [member], canManage: false }),
    })
    renderRoute('/settings/users')

    expect(await screen.findByText(t.users.memberNotice)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: t.users.add })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: `${t.users.remove}: Ivo` })).not.toBeInTheDocument()
  })

  it('adds a member with a temporary password', async () => {
    let body: unknown
    stubApi({
      ...healthy,
      'GET /api/users': () => json({ users, canManage: true }),
      'POST /api/users': async (request) => {
        body = await request.json()
        return json({ ...member, id: 'user-new' }, 201)
      },
    })
    renderRoute('/settings/users/new')

    await userEvent.type(await screen.findByLabelText(t.setup.displayName), 'Marko')
    await userEvent.type(screen.getByLabelText(t.setup.username), 'marko')
    await userEvent.type(screen.getByLabelText(t.users.temporaryPassword), 'temporary password')
    await userEvent.click(screen.getByRole('button', { name: t.users.save }))

    await waitFor(() =>
      expect(body).toEqual({
        displayName: 'Marko',
        username: 'marko',
        role: 'Member',
        temporaryPassword: 'temporary password',
      }),
    )
    expect(await screen.findByRole('heading', { name: t.users.section })).toBeInTheDocument()
  })
})
