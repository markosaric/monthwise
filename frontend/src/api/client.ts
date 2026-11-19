import createClient from 'openapi-fetch'
import { currentLanguage } from '@/i18n/language'
import type { components, paths } from './schema'

/**
 * Typed client for the Monthwise API. Types come from `schema.d.ts`, generated from the
 * back-end OpenAPI document with `npm run api:generate`; never edit that file by hand.
 * Requests go to the same origin (`/api/...`); in development Vite proxies them to the API.
 * Every request names the app's language, so the API answers with messages and names in it
 * (croatian-language.md FR-3, FR-4).
 */
export const api = createClient<paths>({
  baseUrl: typeof window === 'undefined' ? 'http://localhost' : window.location.origin,
  credentials: 'same-origin',
  headers: { 'Accept-Language': currentLanguage() },
  // Look up fetch per request (not once at start-up) so tests can stub it.
  fetch: (request) => globalThis.fetch(request),
})

export type Schemas = components['schemas']
