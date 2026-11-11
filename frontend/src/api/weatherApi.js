// Every call to the C# API goes through this module, so components never deal with
// URLs, status codes or JSON parsing. Each function returns a Promise that either
// resolves with data or rejects with an ApiError carrying a message we can show users.

const UNREACHABLE_MESSAGE = "Can't reach the weather server. Check that the API is running."

// Extending Error keeps the stack trace and lets callers check `err instanceof ApiError`.
export class ApiError extends Error {
  constructor(message, { status = 0, kind = 'server', fieldErrors = {} } = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status         // HTTP status, or 0 when we never got a response
    this.kind = kind             // 'network' | 'validation' | 'server'
    this.fieldErrors = fieldErrors
  }
}

// fetch() rejects with an AbortError when we cancel a request on purpose
// (for example, the user typed another letter). That isn't a real failure.
export const isAbortError = (err) => err?.name === 'AbortError'

async function request(path, params = {}, signal) {
  // URLSearchParams builds "?a=1&b=two" and escapes each value for us.
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      query.set(key, String(value))
    }
  }
  const url = query.size > 0 ? `${path}?${query}` : path

  let response
  try {
    response = await fetch(url, { signal, headers: { Accept: 'application/json' } })
  } catch (err) {
    if (isAbortError(err)) throw err
    // fetch only rejects when there's no response at all (offline, DNS failure, ...).
    throw new ApiError(UNREACHABLE_MESSAGE, { kind: 'network' })
  }

  // Unlike many HTTP libraries, fetch does NOT reject on 4xx/5xx. We have to check response.ok.
  if (response.ok) {
    return response.json()
  }
  throw await toApiError(response)
}

// The C# API always sends errors as ProblemDetails JSON:
//   { title, status, detail?, errors?: { field: [messages] } }
async function toApiError(response) {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('json')) {
    // Not from our API. In development, the Vite proxy answers with a bare 5xx when
    // the C# API isn't running.
    return new ApiError(UNREACHABLE_MESSAGE, { status: response.status, kind: 'network' })
  }

  const problem = await response.json().catch(() => ({}))
  const fieldErrors = problem.errors ?? {}
  const firstFieldError = Object.values(fieldErrors).flat()[0]

  return new ApiError(
    problem.detail ?? firstFieldError ?? problem.title ?? `Request failed (${response.status}).`,
    {
      status: response.status,
      kind: response.status === 400 ? 'validation' : 'server',
      fieldErrors,
    },
  )
}

export function searchLocations(query, signal) {
  return request('/api/locations/search', { q: query }, signal)
}

// logSearch = true records the visit in search history (user picked it), and false
// for refreshes.
export function getWeather(location, { logSearch = false } = {}, signal) {
  return request(
    '/api/weather',
    {
      lat: location.latitude,
      lon: location.longitude,
      name: location.name,
      country: location.country,
      admin1: location.admin1,
      logSearch,
    },
    signal,
  )
}

export function getHistory(limit = 8, signal) {
  return request('/api/history', { limit }, signal)
}
