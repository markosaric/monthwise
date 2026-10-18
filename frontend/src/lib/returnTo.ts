import { useSearchParams } from 'react-router'

/**
 * The screen a form opened from a quick action returns to (`?returnTo=/`, design refresh FR-14).
 * Only paths inside the app are accepted; anything else is ignored.
 */
export function useReturnTo(): string | null {
  const [searchParams] = useSearchParams()
  return safeReturnTo(searchParams.get('returnTo'))
}

export function safeReturnTo(value: string | null): string | null {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\'))
    return null
  return value
}
