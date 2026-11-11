import { useCallback, useEffect, useState } from 'react'
import { getWeather, isAbortError } from '../api/weatherApi.js'

const LOCATION_KEY = 'weather-app:last-location'
const AUTO_REFRESH_KEY = 'weather-app:auto-refresh'
const DEFAULT_AUTO_REFRESH = { enabled: true, intervalMs: 5 * 60_000 }

// localStorage can throw (private windows, storage disabled), so guard every access.
function readJson(key) {
  try {
    const raw = localStorage.getItem(key)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

function writeJson(key, value) {
  try {
    localStorage.setItem(key, JSON.stringify(value))
  } catch {
    // Not being remembered next visit is fine.
  }
}

// A custom hook: a function whose name starts with "use" and calls other hooks.
// It wraps all the "load weather" state so App.jsx stays simple.
//
// The pattern: event handlers (load / reload) only record WHAT we want in `request`.
// The effect below reacts to `request` and does the fetching. Each request is a new
// object, so even reloading the same place re-runs the effect.
export function useWeather() {
  // Passing a function to useState ("lazy initial state") runs it only on the first
  // render. On startup, reopen the last location the user viewed.
  const [request, setRequest] = useState(() => {
    const saved = readJson(LOCATION_KEY)
    return saved ? { location: saved, logSearch: false, at: Date.now() } : null
  })
  const [weather, setWeather] = useState(null)     // last successful response
  const [error, setError] = useState(null)
  const [status, setStatus] = useState(request ? 'loading' : 'idle')   // 'idle' | 'loading' | 'success' | 'error'
  const [autoRefresh, setAutoRefreshState] = useState(
    () => ({ ...DEFAULT_AUTO_REFRESH, ...readJson(AUTO_REFRESH_KEY) }),
  )

  // --- Fetch whenever the request changes ---------------------------------------
  useEffect(() => {
    if (!request) return

    const controller = new AbortController()
    getWeather(request.location, { logSearch: request.logSearch }, controller.signal)
      .then((data) => {
        setWeather(data)
        setStatus('success')
        writeJson(LOCATION_KEY, pickLocation(request.location))
      })
      .catch((err) => {
        if (isAbortError(err)) return
        setError(err)
        setStatus('error')
      })

    // The cleanup runs when `request` changes or the component unmounts. Aborting here
    // fixes the race where the user picks Paris and then London quickly, and the slow
    // Paris response would otherwise arrive last and overwrite London.
    return () => controller.abort()
  }, [request])

  // useCallback keeps the same function object between renders, so it's safe to pass
  // down as a prop or list in another hook's dependency array.
  const load = useCallback((location, { logSearch = false } = {}) => {
    setRequest({ location, logSearch, at: Date.now() })
    setStatus('loading')
    setError(null)
  }, [])

  // Load the current location again. Used by Retry, the Refresh button and auto-refresh.
  const reload = useCallback(() => {
    if (request) load(request.location)
  }, [request, load])

  // --- Auto-refresh ---------------------------------------------------------------
  // One setTimeout, measured from the latest request. Every load (including a manual
  // refresh) creates a new request, which re-runs this effect and restarts the countdown.
  // A setInterval would keep its own rhythm and could fire seconds after a manual refresh.
  useEffect(() => {
    if (!autoRefresh.enabled || !request) return

    let due = false
    const refresh = () => load(request.location)
    const delay = Math.max(0, request.at + autoRefresh.intervalMs - Date.now())

    const timer = setTimeout(() => {
      // Don't fetch for a background tab nobody is looking at. Catch up when it's shown again.
      if (document.hidden) {
        due = true
      } else {
        refresh()
      }
    }, delay)

    const onVisibilityChange = () => {
      if (due && !document.hidden) refresh()
    }
    document.addEventListener('visibilitychange', onVisibilityChange)

    return () => {
      clearTimeout(timer)
      document.removeEventListener('visibilitychange', onVisibilityChange)
    }
  }, [autoRefresh, request, load])

  // Remember the auto-refresh settings. Writing to localStorage is a side effect, so it
  // belongs in an effect, not in the state-updater function (which React may call twice).
  useEffect(() => {
    writeJson(AUTO_REFRESH_KEY, autoRefresh)
  }, [autoRefresh])

  // Merge partial changes: setAutoRefresh({ enabled: false }) keeps intervalMs.
  const setAutoRefresh = useCallback((changes) => {
    setAutoRefreshState((current) => ({ ...current, ...changes }))
  }, [])

  const clearError = useCallback(() => setError(null), [])

  // Derived value: computed during render from existing state.
  const nextRefreshAt = autoRefresh.enabled && request ? request.at + autoRefresh.intervalMs : null

  return {
    location: request?.location ?? null,
    weather,
    status,
    error,
    autoRefresh,
    nextRefreshAt,
    load,
    reload,
    setAutoRefresh,
    clearError,
  }
}

// Recent-search items carry extra fields (locationId, lastSearchedAt); store only the location.
function pickLocation({ name, country, admin1, latitude, longitude }) {
  return { name, country, admin1, latitude, longitude }
}
