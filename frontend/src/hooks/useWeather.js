import { useCallback, useEffect, useState } from 'react'
import { getWeather, isAbortError } from '../api/weatherApi.js'

const STORAGE_KEY = 'weather-app:last-location'

// localStorage can throw (private windows, storage disabled), so guard every access.
function readSavedLocation() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

function saveLocation({ name, country, admin1, latitude, longitude }) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ name, country, admin1, latitude, longitude }))
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
    const saved = readSavedLocation()
    return saved ? { location: saved, logSearch: false } : null
  })
  const [weather, setWeather] = useState(null)     // last successful response
  const [error, setError] = useState(null)
  const [status, setStatus] = useState(request ? 'loading' : 'idle')   // 'idle' | 'loading' | 'success' | 'error'

  useEffect(() => {
    if (!request) return

    const controller = new AbortController()
    getWeather(request.location, { logSearch: request.logSearch }, controller.signal)
      .then((data) => {
        setWeather(data)
        setStatus('success')
        saveLocation(request.location)
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
    setRequest({ location, logSearch })
    setStatus('loading')
    setError(null)
  }, [])

  // Load the current location again. Used by Retry and, in Phase 4, the refresh controls.
  const reload = useCallback(() => {
    if (request) load(request.location)
  }, [request, load])

  const clearError = useCallback(() => setError(null), [])

  return { location: request?.location ?? null, weather, status, error, load, reload, clearError }
}
