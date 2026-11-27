import { useNavigate } from 'react-router'
import { toast } from 'sonner'
import { PasswordForm } from '@/components/login/PasswordForm'
import { loginTexts } from '@/components/login/texts'
import { PageHeader } from '@/pages/PageHeader'

const t = loginTexts.password

/** Settings → Change password (login FR-8); the user's other devices are signed out. */
export function PasswordPage() {
  const navigate = useNavigate()
  return (
    <>
      <PageHeader title={t.title} description={t.description} />
      <PasswordForm
        onCancel={() => navigate(-1)}
        onChanged={() => {
          toast.success(t.changed)
          navigate('/settings', { replace: true })
        }}
      />
    </>
  )
}
