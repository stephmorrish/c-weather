import { useEffect, useState } from 'react'

// The current time, updated every `intervalMs`. Use it in the smallest component that
// needs a ticking clock: only that component re-renders each tick, not the whole app.
export function useNow(intervalMs = 1000) {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(id)
  }, [intervalMs])

  return now
}
