/** A failed API call. `fieldErrors` holds validation messages keyed by request field. */
export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>

  constructor(
    status: number,
    title: string | null | undefined,
    fieldErrors?: Record<string, string[]>,
  ) {
    super(title ?? 'Something went wrong. Please try again.')
    this.status = status
    this.fieldErrors = fieldErrors ?? {}
  }
}

interface ApiResult<T> {
  data?: T
  error?: { title?: string | null; errors?: Record<string, string[]> } | unknown
  response: Response
}

/** Returns the response body, or throws an `ApiError` with the problem details. */
export function unwrap<T>({ data, error, response }: ApiResult<T>): T {
  if (data !== undefined && response.ok) return data
  const problem = (error ?? {}) as { title?: string | null; errors?: Record<string, string[]> }
  throw new ApiError(response.status, problem.title, problem.errors)
}

/** For endpoints that answer 204 No Content. */
export function ensureOk({ error, response }: { error?: unknown; response: Response }): void {
  if (response.ok) return
  const problem = (error ?? {}) as { title?: string | null; errors?: Record<string, string[]> }
  throw new ApiError(response.status, problem.title, problem.errors)
}
