import { useEffect, useState } from 'react'

// Returns `value`, but only after it has stopped changing for `delayMs`.
// Typing "Toronto" quickly then makes one search request instead of seven.
export function useDebounce(value, delayMs) {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)
    // The cleanup runs before the next effect, so each keystroke cancels the
    // previous timer.
    return () => clearTimeout(timer)
  }, [value, delayMs])

  return debounced
}
