import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

/**
 * Typed client for the Monthwise API. Types come from `schema.d.ts`, generated from the
 * back-end OpenAPI document with `npm run api:generate`; never edit that file by hand.
 * Requests go to the same origin (`/api/...`); in development Vite proxies them to the API.
 */
export const api = createClient<paths>({
  baseUrl: typeof window === 'undefined' ? 'http://localhost' : window.location.origin,
  credentials: 'same-origin',
  // Look up fetch per request (not once at start-up) so tests can stub it.
  fetch: (request) => globalThis.fetch(request),
})

export type Schemas = components['schemas']
