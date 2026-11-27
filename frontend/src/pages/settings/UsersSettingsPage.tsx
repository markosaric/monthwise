import { KeyRound, Pencil, Trash2, UserPlus } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { useCurrentUser } from '@/api/auth'
import { useChangeUser, useUsers, type User } from '@/api/users'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { appTexts } from '@/components/common/texts'
import { AddButton } from '@/components/design/AddButton'
import { sectionColors } from '@/components/design/colors'
import { PasswordField } from '@/components/login/PasswordField'
import { loginTexts } from '@/components/login/texts'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/pages/PageHeader'

const t = loginTexts.users

/**
 * Settings → Users (login FR-6, FR-7): a household admin adds, edits and deletes users and sets
 * temporary passwords; a member sees only themself.
 */
export function UsersSettingsPage() {
  const { data, isPending, isError } = useUsers()
  const me = useCurrentUser()
  const change = useChangeUser()
  const [removing, setRemoving] = useState<User | null>(null)
  const [resetting, setResetting] = useState<User | null>(null)
  const [temporaryPassword, setTemporaryPassword] = useState('')
  const canManage = data?.canManage ?? false

  return (
    <>
      <PageHeader title={t.section} description={t.description} />
      <div className="flex max-w-2xl flex-col gap-3">
        {canManage && (
          <div>
            <AddButton
              to="/settings/users/new"
              label={t.add}
              icon={UserPlus}
              color={sectionColors.settings}
            />
          </div>
        )}
        {data && !canManage && <p className="text-sm text-muted-foreground">{t.memberNotice}</p>}
        {isPending && <p className="text-sm text-muted-foreground">{appTexts.common.loading}</p>}
        {isError && <p className="text-sm text-destructive">{loginTexts.errors.users}</p>}
        {data?.users.length === 0 && <p className="text-sm text-muted-foreground">{t.empty}</p>}
        <ul className="flex flex-col gap-2">
          {data?.users.map((user) => (
            <li
              key={user.id}
              className="flex flex-wrap items-center gap-3 rounded-xl bg-card p-3 ring-1 ring-foreground/8"
            >
              <div className="flex min-w-0 flex-1 flex-col">
                <span className="font-semibold">
                  {user.displayName}
                  {user.id === me?.id && (
                    <span className="font-normal text-muted-foreground"> ({t.you})</span>
                  )}
                </span>
                <span className="text-sm text-muted-foreground">
                  {user.username} · {t.roles[user.role]}
                  {!user.isActive && ` · ${t.inactive}`}
                  {user.mustChangePassword && ` · ${t.mustChange}`}
                </span>
              </div>
              {canManage && (
                <div className="flex gap-1">
                  <Button
                    asChild
                    variant="ghost"
                    size="icon"
                    aria-label={`${t.edit}: ${user.displayName}`}
                  >
                    <Link to={`/settings/users/${user.id}/edit`}>
                      <Pencil aria-hidden />
                    </Link>
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`${t.resetPassword}: ${user.displayName}`}
                    onClick={() => {
                      setTemporaryPassword('')
                      setResetting(user)
                    }}
                  >
                    <KeyRound aria-hidden />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`${t.remove}: ${user.displayName}`}
                    onClick={() => setRemoving(user)}
                  >
                    <Trash2 aria-hidden />
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      </div>
      <CardActionDialog
        open={removing !== null}
        onOpenChange={(open) => !open && setRemoving(null)}
        title={removing ? t.removeTitle(removing.displayName) : t.remove}
        description={t.removeDescription}
        confirmLabel={t.remove}
        destructive
        onConfirm={async () => {
          await change.mutateAsync({ action: 'delete', id: removing!.id })
          toast.success(t.removed)
        }}
      />
      <CardActionDialog
        open={resetting !== null}
        onOpenChange={(open) => !open && setResetting(null)}
        title={resetting ? t.resetTitle(resetting.displayName) : t.resetPassword}
        description={t.resetDescription}
        confirmLabel={t.resetPassword}
        canConfirm={temporaryPassword.length >= 12}
        onConfirm={async () => {
          await change.mutateAsync({
            action: 'resetPassword',
            id: resetting!.id,
            temporaryPassword,
          })
          toast.success(t.resetDone)
        }}
      >
        <PasswordField
          id="reset-temporary-password"
          label={t.temporaryPassword}
          hint={t.temporaryPasswordHint}
          autoComplete="new-password"
          value={temporaryPassword}
          onChange={(e) => setTemporaryPassword(e.target.value)}
        />
      </CardActionDialog>
    </>
  )
}
