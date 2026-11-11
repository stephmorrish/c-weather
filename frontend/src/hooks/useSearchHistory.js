import { useEffect, useState } from 'react'
import { getHistory, isAbortError } from '../api/weatherApi.js'

// Fetches recent searches, and fetches again whenever `refreshKey` changes.
// App passes the latest weather response as the key: each new response means the
// API may have just logged a search.
export function useSearchHistory(refreshKey, limit = 8) {
  const [items, setItems] = useState([])
  const [error, setError] = useState(null)
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    getHistory(limit, controller.signal)
      .then((data) => {
        setItems(data)
        setError(null)
        setLoaded(true)
      })
      .catch((err) => {
        if (isAbortError(err)) return
        setError(err)   // e.g. 503 when Oracle is down; the panel shows a quiet note
        setLoaded(true)
      })
    return () => controller.abort()
  }, [refreshKey, limit])

  // "loading" is derived, not stored: it's true only before the first answer.
  // Later refreshes keep showing the old list until the new one arrives.
  return { items, error, loading: !loaded }
}
