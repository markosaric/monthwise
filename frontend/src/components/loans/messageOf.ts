import { ApiError } from '@/api/cards'

/** The first API message of a failed save, or a general one. */
export function messageOf(error: unknown): string {
  if (error instanceof ApiError) return Object.values(error.fieldErrors)[0]?.[0] ?? error.message
  return 'Saving failed.'
}
