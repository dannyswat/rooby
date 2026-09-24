import type { ProblemDetails } from './types'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`)
    this.status = status
    this.problem = problem
  }
}

interface RequestOptions {
  body?: unknown
  ifMatch?: string
}

async function request<T>(method: string, path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = {}
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (options.ifMatch !== undefined) {
    headers['If-Match'] = options.ifMatch
  }

  const response = await fetch(`/api${path}`, {
    method,
    headers,
    credentials: 'include',
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

  if (response.status === 204) {
    return undefined as T
  }

  const contentType = response.headers.get('content-type') ?? ''
  const isJson = contentType.includes('json')
  const payload = isJson ? await response.json() : undefined

  if (!response.ok) {
    throw new ApiError(response.status, isJson ? (payload as ProblemDetails) : null)
  }

  return payload as T
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown, ifMatch?: string) => request<T>('POST', path, { body, ifMatch }),
  put: <T>(path: string, body?: unknown, ifMatch?: string) => request<T>('PUT', path, { body, ifMatch }),
  patch: <T>(path: string, body?: unknown, ifMatch?: string) => request<T>('PATCH', path, { body, ifMatch }),
  delete: <T>(path: string, ifMatch?: string) => request<T>('DELETE', path, { ifMatch }),
}
