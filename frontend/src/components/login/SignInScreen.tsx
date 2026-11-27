import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { useSignIn } from '@/api/auth'
import { applyApiErrors } from '@/components/expenses/formErrors'
import { Field } from '@/components/expenses/formParts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { LoginFrame } from './LoginFrame'
import { PasswordField } from './PasswordField'
import { loginTexts } from './texts'

const t = loginTexts.signIn

interface SignInValues {
  username: string
  password: string
  staySignedIn: boolean
}

/**
 * Sign in with username and password (login FR-2 to FR-4). After signing in the screen that was asked
 * for shows, because the address does not change.
 */
export function SignInScreen() {
  const signIn = useSignIn()
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<SignInValues>({ defaultValues: { username: '', password: '', staySignedIn: false } })

  async function submit(values: SignInValues) {
    setFormError(null)
    try {
      await signIn.mutateAsync(values)
    } catch (error) {
      setFormError(applyApiErrors(error, values, setError))
    }
  }

  return (
    <LoginFrame title={t.title} description={t.description}>
      <form onSubmit={handleSubmit(submit)} noValidate className="flex flex-col gap-4">
        <Field id="username" label={t.username} error={errors.username?.message}>
          <Input
            id="username"
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            aria-invalid={!!errors.username}
            {...register('username', { required: loginTexts.errors.required })}
          />
        </Field>
        <PasswordField
          id="password"
          label={t.password}
          autoComplete="current-password"
          error={errors.password?.message}
          {...register('password', { required: loginTexts.errors.required })}
        />
        <div className="flex flex-col gap-1">
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" className="size-4" {...register('staySignedIn')} />
            {t.staySignedIn}
          </label>
          <p className="pl-6 text-xs text-muted-foreground">{t.staySignedInHint}</p>
        </div>
        {formError && (
          <p role="alert" className="text-sm text-destructive">
            {formError}
          </p>
        )}
        <Button type="submit" disabled={isSubmitting}>
          {t.submit}
        </Button>
        <p className="text-xs text-muted-foreground">{t.forgotPassword}</p>
        <Link to="/setup" className="text-sm text-muted-foreground underline underline-offset-4">
          {t.haveSetupCode}
        </Link>
      </form>
    </LoginFrame>
  )
}
