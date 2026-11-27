import { CircleUserRound, KeyRound, LogOut } from 'lucide-react'
import { Link } from 'react-router'
import { useCurrentUser, useSignOut } from '@/api/auth'
import { sectionColors } from '@/components/design/colors'
import { IconTile } from '@/components/design/IconTile'
import { Button } from '@/components/ui/button'
import { loginTexts } from './texts'

const t = loginTexts.account

/** Who is signed in, with Change password and Sign out (login FR-5, FR-8), in Settings on every device. */
export function AccountSection() {
  const user = useCurrentUser()
  const signOut = useSignOut()
  if (!user) return null

  return (
    <section aria-labelledby="account-title" className="flex flex-col gap-2">
      <h2
        id="account-title"
        className="font-sans text-xs font-semibold tracking-wider text-muted-foreground uppercase"
      >
        {t.title}
      </h2>
      <div className="flex flex-wrap items-center gap-3 rounded-xl bg-card p-4 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8">
        <IconTile icon={CircleUserRound} color={sectionColors.settings} solid />
        <p className="flex min-w-0 flex-1 flex-col text-sm">
          <span className="font-semibold">{t.signedInAs(user.displayName)}</span>
          <span className="text-muted-foreground">
            {user.username} · {loginTexts.users.roles[user.role]}
          </span>
        </p>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline" size="sm">
            <Link to="/settings/password">
              <KeyRound aria-hidden />
              {t.changePassword}
            </Link>
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => signOut.mutate()}
            disabled={signOut.isPending}
          >
            <LogOut aria-hidden />
            {t.signOut}
          </Button>
        </div>
      </div>
    </section>
  )
}
