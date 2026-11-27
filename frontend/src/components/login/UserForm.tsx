import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import type { UserRole } from '@/api/users'
import { applyApiErrors } from '@/components/expenses/formErrors'
import { Field } from '@/components/expenses/formParts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect } from '@/components/ui/native-select'
import { PasswordField } from './PasswordField'
import { loginTexts } from './texts'

const t = loginTexts.users
const roles: UserRole[] = ['HouseholdAdmin', 'Member']

export interface UserFormValues {
  displayName: string
  username: string
  role: UserRole
  isActive: boolean
  temporaryPassword: string
}

/** Add or edit a user (login FR-6, U1 to U5); a new user gets a temporary password (FR-7). */
export function UserForm({
  defaultValues,
  isNew,
  onSubmit,
  onCancel,
}: {
  defaultValues: UserFormValues
  isNew: boolean
  onSubmit: (values: UserFormValues) => Promise<void>
  onCancel: () => void
}) {
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<UserFormValues>({ defaultValues })
  const role = useWatch({ control, name: 'role' })

  async function submit(values: UserFormValues) {
    setFormError(null)
    try {
      await onSubmit(values)
    } catch (error) {
      setFormError(applyApiErrors(error, values, setError))
    }
  }

  const required = { required: loginTexts.errors.required }
  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      <Field
        id="displayName"
        label={loginTexts.setup.displayName}
        hint={loginTexts.setup.displayNameHint}
        error={errors.displayName?.message}
      >
        <Input
          id="displayName"
          maxLength={100}
          aria-invalid={!!errors.displayName}
          {...register('displayName', required)}
        />
      </Field>
      <Field
        id="username"
        label={loginTexts.setup.username}
        hint={loginTexts.setup.usernameHint}
        error={errors.username?.message}
      >
        <Input
          id="username"
          autoComplete="off"
          autoCapitalize="none"
          spellCheck={false}
          maxLength={50}
          aria-invalid={!!errors.username}
          {...register('username', required)}
        />
      </Field>
      <Field id="role" label={t.role} hint={t.roleHints[role]} error={errors.role?.message}>
        <NativeSelect id="role" {...register('role')}>
          {roles.map((r) => (
            <option key={r} value={r}>
              {t.roles[r]}
            </option>
          ))}
        </NativeSelect>
      </Field>
      {isNew ? (
        <PasswordField
          id="temporaryPassword"
          label={t.temporaryPassword}
          hint={t.temporaryPasswordHint}
          autoComplete="new-password"
          error={errors.temporaryPassword?.message}
          {...register('temporaryPassword', {
            ...required,
            minLength: { value: 12, message: loginTexts.errors.passwordTooShort },
          })}
        />
      ) : (
        <div className="flex flex-col gap-1">
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" className="size-4" {...register('isActive')} />
            {t.active}
          </label>
          <p className="pl-6 text-xs text-muted-foreground">{t.activeHint}</p>
        </div>
      )}
      {formError && (
        <p role="alert" className="text-sm text-destructive">
          {formError}
        </p>
      )}
      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {t.save}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          {t.cancel}
        </Button>
      </div>
    </form>
  )
}
