import { Navigate, Outlet, useLocation } from 'react-router'
import { toast } from 'sonner'
import { useAuthState, useSignOut } from '@/api/auth'
import { ScreenLoading } from '@/components/layout/ScreenLoading'
import { Button } from '@/components/ui/button'
import { LoginFrame } from './LoginFrame'
import { PasswordForm } from './PasswordForm'
import { SetupScreen } from './SetupScreen'
import { SignInScreen } from './SignInScreen'
import { loginTexts } from './texts'

/**
 * Shows the app only to a signed-in user (login FR-2): the setup form while there are no users
 * (FR-1), the setup form at /setup for a new setup code (FR-9), the sign-in screen otherwise, and a
 * new password first after a temporary one (FR-7). When who is signed in cannot be found out (the API
 * is not reachable), the app shows, so its system status explains the problem.
 */
export function AuthGate() {
  const { data: state, isPending } = useAuthState()
  const { pathname } = useLocation()
  const signOut = useSignOut()

  if (isPending) return <ScreenLoading />
  if (state?.setupNeeded) return <SetupScreen />
  if (state && !state.user)
    return pathname === '/setup' ? <SetupScreen recovery /> : <SignInScreen />
  if (state?.user?.mustChangePassword) {
    return (
      <LoginFrame
        title={loginTexts.password.forcedTitle}
        description={loginTexts.password.forcedDescription}
      >
        <PasswordForm temporary onChanged={() => toast.success(loginTexts.password.changed)} />
        <Button variant="ghost" className="self-start" onClick={() => signOut.mutate()}>
          {loginTexts.account.signOut}
        </Button>
      </LoginFrame>
    )
  }
  if (pathname === '/setup') return <Navigate to="/" replace />
  return <Outlet />
}
