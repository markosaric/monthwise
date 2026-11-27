import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router'
import { useSetup } from '@/api/auth'
import { applyApiErrors } from '@/components/expenses/formErrors'
import { Field } from '@/components/expenses/formParts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { LoginFrame } from './LoginFrame'
import { PasswordField } from './PasswordField'
import { loginTexts } from './texts'

const t = loginTexts.setup
const passwordMinLength = 12

interface SetupValues {
  setupCode: string
  displayName: string
  username: string
  password: string
  repeatPassword: string
}

/**
 * Set up Monthwise with the setup code (login FR-1): the first household admin, or later with a new
 * code a new password for a household admin or a new household admin (FR-9).
 */
export function SetupScreen({ recovery = false }: { recovery?: boolean }) {
  const setup = useSetup()
  const navigate = useNavigate()
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<SetupValues>({
    defaultValues: {
      setupCode: '',
      displayName: '',
      username: '',
      password: '',
      repeatPassword: '',
    },
  })

  async function submit({ repeatPassword: _repeat, ...request }: SetupValues) {
    setFormError(null)
    try {
      await setup.mutateAsync(request)
      if (recovery) navigate('/', { replace: true })
    } catch (error) {
      setFormError(applyApiErrors(error, getValues(), setError))
    }
  }

  const required = { required: loginTexts.errors.required }
  return (
    <LoginFrame title={t.title} description={recovery ? t.recoveryDescription : t.description}>
      <form onSubmit={handleSubmit(submit)} noValidate className="flex flex-col gap-4">
        <Field id="setupCode" label={t.setupCode} error={errors.setupCode?.message}>
          <Input
            id="setupCode"
            autoComplete="one-time-code"
            autoCapitalize="characters"
            spellCheck={false}
            aria-invalid={!!errors.setupCode}
            {...register('setupCode', required)}
          />
        </Field>
        <Field
          id="displayName"
          label={t.displayName}
          hint={t.displayNameHint}
          error={errors.displayName?.message}
        >
          <Input
            id="displayName"
            autoComplete="name"
            maxLength={100}
            aria-invalid={!!errors.displayName}
            {...register('displayName', required)}
          />
        </Field>
        <Field
          id="username"
          label={t.username}
          hint={t.usernameHint}
          error={errors.username?.message}
        >
          <Input
            id="username"
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            maxLength={50}
            aria-invalid={!!errors.username}
            {...register('username', required)}
          />
        </Field>
        <PasswordField
          id="password"
          label={t.password}
          hint={t.passwordHint}
          autoComplete="new-password"
          error={errors.password?.message}
          {...register('password', {
            ...required,
            minLength: { value: passwordMinLength, message: loginTexts.errors.passwordTooShort },
          })}
        />
        <PasswordField
          id="repeatPassword"
          label={t.repeatPassword}
          autoComplete="new-password"
          error={errors.repeatPassword?.message}
          {...register('repeatPassword', {
            validate: (value) =>
              value === getValues('password') || loginTexts.errors.passwordsDiffer,
          })}
        />
        {formError && (
          <p role="alert" className="text-sm text-destructive">
            {formError}
          </p>
        )}
        <Button type="submit" disabled={isSubmitting}>
          {t.submit}
        </Button>
        {recovery && (
          <Link to="/" className="text-sm text-muted-foreground underline underline-offset-4">
            {t.backToSignIn}
          </Link>
        )}
      </form>
    </LoginFrame>
  )
}
