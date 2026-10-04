/** One stubbed endpoint: "GET /api/cards/dashboard" → handler. The path is matched without the query string. */
export type ApiHandlers = Record<string, (request: Request) => Response | Promise<Response>>

export const json = (body: unknown, status = 200) =>
  Response.json(body, { status, headers: { 'Content-Type': 'application/json' } })

/** Replaces fetch with the given handlers; unknown requests get a 404. Returns the fetch mock. */
export function stubApi(handlers: ApiHandlers) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : new Request(input, init)
    const url = new URL(request.url)
    const handler = handlers[`${request.method} ${url.pathname}`]
    return handler ? handler(request) : json({ title: 'Not found' }, 404)
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

export const healthy = { 'GET /api/health': () => json({ status: 'Healthy', database: 'Healthy' }) }
