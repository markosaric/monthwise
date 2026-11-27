import { useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { useChangeUser, useUser } from '@/api/users'
import { appTexts } from '@/components/common/texts'
import { UserForm } from '@/components/login/UserForm'
import { loginTexts } from '@/components/login/texts'
import { PageHeader } from '@/pages/PageHeader'

const t = loginTexts.users

/** Add a user with a temporary password (login FR-6, FR-7). */
export function UserNewPage() {
  const navigate = useNavigate()
  const change = useChangeUser()

  return (
    <>
      <PageHeader title={t.add} />
      <UserForm
        isNew
        defaultValues={{
          displayName: '',
          username: '',
          role: 'Member',
          isActive: true,
          temporaryPassword: '',
        }}
        onCancel={() => navigate(-1)}
        onSubmit={async ({ displayName, username, role, temporaryPassword }) => {
          await change.mutateAsync({
            action: 'create',
            request: { displayName, username, role, temporaryPassword },
          })
          toast.success(t.saved)
          navigate('/settings/users', { replace: true })
        }}
      />
    </>
  )
}

/** Edit a user: name, username, role and whether they may sign in (FR-6). */
export function UserEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: user, isPending, isError } = useUser(id)
  const change = useChangeUser()
  if (isPending) return <p className="text-sm text-muted-foreground">{appTexts.common.loading}</p>
  if (isError) return <p className="text-sm text-destructive">{loginTexts.errors.users}</p>

  return (
    <>
      <PageHeader title={`${t.edit}: ${user.displayName}`} />
      <UserForm
        isNew={false}
        defaultValues={{ ...user, temporaryPassword: '' }}
        onCancel={() => navigate(-1)}
        onSubmit={async ({ displayName, username, role, isActive }) => {
          await change.mutateAsync({
            action: 'update',
            id: user.id,
            request: { displayName, username, role, isActive },
          })
          toast.success(t.saved)
          navigate('/settings/users', { replace: true })
        }}
      />
    </>
  )
}

export { PasswordPage } from './PasswordPage'
