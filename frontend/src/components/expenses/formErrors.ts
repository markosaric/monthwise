import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { ApiError } from '@/api/cards'

/**
 * Shows API validation errors next to their fields; returns the message for the form when none of
 * them belongs to a field. `fieldMap` maps API field names that differ from the form's.
 */
export function applyApiErrors<T extends FieldValues>(
  error: unknown,
  fields: T,
  setError: UseFormSetError<T>,
  fieldMap: Record<string, keyof T> = {},
): string | null {
  if (!(error instanceof ApiError)) return 'Saving failed. Please try again.'
  let mapped = false
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const formField = (fieldMap[field] ?? field) as string
    if (formField in fields) {
      setError(formField as Path<T>, { message: messages[0] })
      mapped = true
    }
  }
  return mapped ? null : error.message
}
