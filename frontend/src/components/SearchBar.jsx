import { useEffect, useId, useState } from 'react'
import { isAbortError, searchLocations } from '../api/weatherApi.js'
import { useDebounce } from '../hooks/useDebounce.js'
import { formatRegion } from '../utils/format.js'

const MIN_QUERY_LENGTH = 2

// An autocomplete ("combobox"): type a place name and pick from matching locations.
// Keyboard: ↑/↓ move, Enter picks, Escape closes.
export default function SearchBar({ onSelect }) {
  const [query, setQuery] = useState('')
  // The last answer from the API, and which query it was for.
  const [search, setSearch] = useState({ query: '', results: [], error: null })
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)

  const debouncedQuery = useDebounce(query.trim(), 300)
  const listId = useId()   // unique id, used to link the input to its list for screen readers

  // Run a search whenever the debounced text changes.
  useEffect(() => {
    if (debouncedQuery.length < MIN_QUERY_LENGTH) return

    const controller = new AbortController()
    searchLocations(debouncedQuery, controller.signal)
      .then((results) => {
        setSearch({ query: debouncedQuery, results, error: null })
        setActiveIndex(results.length > 0 ? 0 : -1)
      })
      .catch((err) => {
        if (isAbortError(err)) return
        setSearch({ query: debouncedQuery, results: [], error: err })
      })

    // If the text changes again before this search finishes, cancel it.
    return () => controller.abort()
  }, [debouncedQuery])

  // Derived state: computed from existing state on every render instead of stored.
  // "Loading" simply means the answer we hold isn't for the current query yet.
  const isActive = debouncedQuery.length >= MIN_QUERY_LENGTH
  const isCurrent = search.query === debouncedQuery
  const status = !isActive ? 'idle' : !isCurrent ? 'loading' : search.error ? 'error' : 'done'
  const results = status === 'done' ? search.results : []

  function choose(location) {
    onSelect(location)
    setQuery('')
    setOpen(false)
  }

  function handleKeyDown(event) {
    if (event.key === 'Escape') {
      setOpen(false)
      return
    }
    if (!open || results.length === 0) return

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault()   // stop the cursor jumping to the end of the input
        setActiveIndex((i) => (i + 1) % results.length)
        break
      case 'ArrowUp':
        event.preventDefault()
        setActiveIndex((i) => (i - 1 + results.length) % results.length)
        break
      case 'Enter':
        event.preventDefault()
        if (activeIndex >= 0) choose(results[activeIndex])
        break
    }
  }

  const showList = open && status !== 'idle'
  const optionId = (index) => `${listId}-option-${index}`

  return (
    <div className="search">
      <label htmlFor={`${listId}-input`} className="visually-hidden">
        Search for a city
      </label>
      <input
        id={`${listId}-input`}
        className="search__input"
        type="search"
        placeholder="Search for a city…"
        autoComplete="off"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onKeyDown={handleKeyDown}
        role="combobox"
        aria-expanded={showList}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={showList && activeIndex >= 0 ? optionId(activeIndex) : undefined}
      />

      {showList && (
        <ul id={listId} className="search__results" role="listbox">
          {status === 'loading' && <li className="search__message">Searching…</li>}
          {status === 'error' && <li className="search__message search__message--error">{search.error.message}</li>}
          {status === 'done' && results.length === 0 && (
            <li className="search__message">No places found for “{debouncedQuery}”.</li>
          )}
          {status === 'done' &&
            results.map((location, index) => (
              <li
                key={`${location.latitude},${location.longitude}`}
                id={optionId(index)}
                role="option"
                aria-selected={index === activeIndex}
                className={`search__option ${index === activeIndex ? 'search__option--active' : ''}`}
                // onMouseDown fires before the input's onBlur. preventDefault keeps the
                // focus in the input, so the list doesn't close before the click lands.
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => choose(location)}
                onMouseEnter={() => setActiveIndex(index)}
              >
                <span className="search__name">{location.name}</span>
                <span className="search__region">{formatRegion(location)}</span>
              </li>
            ))}
        </ul>
      )}
    </div>
  )
}
