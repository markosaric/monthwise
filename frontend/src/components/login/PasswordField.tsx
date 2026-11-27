import { forwardRef, type ComponentProps } from 'react'
import { Field } from '@/components/expenses/formParts'
import { Input } from '@/components/ui/input'

/** A labelled password input with its hint or error. */
export const PasswordField = forwardRef<
  HTMLInputElement,
  ComponentProps<'input'> & { id: string; label: string; hint?: string; error?: string }
>(function PasswordField({ id, label, hint, error, ...props }, ref) {
  return (
    <Field id={id} label={label} hint={hint} error={error}>
      <Input id={id} ref={ref} type="password" aria-invalid={!!error} {...props} />
    </Field>
  )
})
