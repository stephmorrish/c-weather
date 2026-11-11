import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, getWeather, searchLocations } from './weatherApi.js'

// These tests replace the browser's global fetch with a fake (vi.fn), so no server is
// needed. Each test decides what "the server" answers, then checks how weatherApi.js
// handles it.

function respondWith(status, body, contentType = 'application/json') {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(typeof body === 'string' ? body : JSON.stringify(body), {
      status,
      headers: { 'content-type': contentType },
    }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => {
  vi.unstubAllGlobals()   // put the real fetch back after every test
})

describe('request building', () => {
  it('encodes the search query', async () => {
    const fetchMock = respondWith(200, [])

    await searchLocations('São Paulo')

    const url = fetchMock.mock.calls[0][0]
    expect(url).toBe('/api/locations/search?q=S%C3%A3o+Paulo')
  })

  it('sends location fields and leaves out empty ones', async () => {
    const fetchMock = respondWith(200, {})

    await getWeather({ name: 'Tokyo', country: 'Japan', admin1: null, latitude: 35.6895, longitude: 139.69171 }, { logSearch: true })

    const params = new URL(fetchMock.mock.calls[0][0], 'http://localhost').searchParams
    expect(params.get('lat')).toBe('35.6895')
    expect(params.get('name')).toBe('Tokyo')
    expect(params.get('logSearch')).toBe('true')
    expect(params.has('admin1')).toBe(false)
  })
})

describe('error handling', () => {
  it('returns the JSON body on success', async () => {
    respondWith(200, [{ name: 'Toronto' }])

    await expect(searchLocations('Toronto')).resolves.toEqual([{ name: 'Toronto' }])
  })

  it('uses ProblemDetails "detail" as the message', async () => {
    respondWith(503, { title: 'Weather service unavailable', status: 503, detail: 'Try again shortly.' }, 'application/problem+json')

    const error = await getWeather({ latitude: 1, longitude: 2 }).catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.message).toBe('Try again shortly.')
    expect(error.status).toBe(503)
    expect(error.kind).toBe('server')
  })

  it('uses the first field error for validation problems', async () => {
    respondWith(400, {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { lat: ['Latitude must be between -90 and 90.'] },
    }, 'application/problem+json')

    const error = await getWeather({ latitude: 200, longitude: 0 }).catch((e) => e)

    expect(error.kind).toBe('validation')
    expect(error.message).toBe('Latitude must be between -90 and 90.')
    expect(error.fieldErrors.lat).toHaveLength(1)
  })

  it('treats a non-JSON 5xx (the dev proxy with the API stopped) as unreachable', async () => {
    respondWith(502, 'Bad Gateway', 'text/plain')

    const error = await searchLocations('Toronto').catch((e) => e)

    expect(error.kind).toBe('network')
    expect(error.message).toMatch(/can't reach the weather server/i)
  })

  it('treats a rejected fetch (offline) as unreachable', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const error = await searchLocations('Toronto').catch((e) => e)

    expect(error.kind).toBe('network')
  })

  it('passes AbortError through untouched, so callers can ignore it', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new DOMException('Aborted', 'AbortError')))

    const error = await searchLocations('Toronto').catch((e) => e)

    expect(error.name).toBe('AbortError')
    expect(error).not.toBeInstanceOf(ApiError)
  })
})
