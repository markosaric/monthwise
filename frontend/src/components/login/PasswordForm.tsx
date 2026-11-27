import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useChangePassword } from '@/api/auth'
import { applyApiErrors } from '@/components/expenses/formErrors'
import { Button } from '@/components/ui/button'
import { PasswordField } from './PasswordField'
import { loginTexts } from './texts'

const t = loginTexts.password
const passwordMinLength = 12

interface PasswordValues {
  currentPassword: string
  newPassword: string
  repeatPassword: string
}

/** Change one's own password (login FR-8), also to replace a temporary password (FR-7). */
export function PasswordForm({
  temporary = false,
  onChanged,
  onCancel,
}: {
  temporary?: boolean
  onChanged: () => void
  onCancel?: () => void
}) {
  const change = useChangePassword()
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<PasswordValues>({
    defaultValues: { currentPassword: '', newPassword: '', repeatPassword: '' },
  })

  async function submit({ repeatPassword: _repeat, ...request }: PasswordValues) {
    setFormError(null)
    try {
      await change.mutateAsync(request)
      onChanged()
    } catch (error) {
      setFormError(applyApiErrors(error, getValues(), setError))
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-md flex-col gap-4">
      <PasswordField
        id="currentPassword"
        label={temporary ? t.currentTemporary : t.current}
        autoComplete="current-password"
        error={errors.currentPassword?.message}
        {...register('currentPassword', { required: loginTexts.errors.required })}
      />
      <PasswordField
        id="newPassword"
        label={t.new}
        hint={loginTexts.setup.passwordHint}
        autoComplete="new-password"
        error={errors.newPassword?.message}
        {...register('newPassword', {
          required: loginTexts.errors.required,
          minLength: { value: passwordMinLength, message: loginTexts.errors.passwordTooShort },
        })}
      />
      <PasswordField
        id="repeatPassword"
        label={t.repeat}
        autoComplete="new-password"
        error={errors.repeatPassword?.message}
        {...register('repeatPassword', {
          validate: (value) =>
            value === getValues('newPassword') || loginTexts.errors.passwordsDiffer,
        })}
      />
      {formError && (
        <p role="alert" className="text-sm text-destructive">
          {formError}
        </p>
      )}
      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {t.submit}
        </Button>
        {onCancel && (
          <Button type="button" variant="ghost" onClick={onCancel}>
            {loginTexts.users.cancel}
          </Button>
        )}
      </div>
    </form>
  )
}
