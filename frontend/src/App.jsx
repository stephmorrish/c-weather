import ErrorBanner from './components/ErrorBanner.jsx'
import RecentSearches from './components/RecentSearches.jsx'
import RefreshControls from './components/RefreshControls.jsx'
import SearchBar from './components/SearchBar.jsx'
import WeatherCard, { WeatherCardSkeleton } from './components/WeatherCard.jsx'
import { useSearchHistory } from './hooks/useSearchHistory.js'
import { useWeather } from './hooks/useWeather.js'
import './App.css'

// App owns the state, through the two hooks, and passes data down to components as
// props. Components report user actions back up through callback props like onSelect.
// This "state down, events up" flow is the core pattern of React.
export default function App() {
  const {
    weather,
    status,
    error,
    autoRefresh,
    nextRefreshAt,
    load,
    reload,
    setAutoRefresh,
    clearError,
  } = useWeather()
  // Each new weather response may mean the API just logged a search, so the history
  // refetches whenever `weather` changes.
  const history = useSearchHistory(weather)

  function handleSelect(location) {
    load(location, { logSearch: true })
  }

  const isLoading = status === 'loading'

  return (
    <div className="app">
      <header className="app__header">
        <h1 className="app__title">Weather</h1>
        <SearchBar onSelect={handleSelect} />
      </header>

      <ErrorBanner error={error} onRetry={reload} onDismiss={clearError} />

      <main className="app__main">
        {/* aria-live="polite" tells screen readers to announce new weather when it changes. */}
        <section className="app__weather" aria-live="polite">
          {weather ? (
            <WeatherCard weather={weather} isUpdating={isLoading}>
              <RefreshControls
                fetchedAt={weather.fetchedAt}
                isLoading={isLoading}
                onRefresh={reload}
                autoRefresh={autoRefresh}
                nextRefreshAt={nextRefreshAt}
                onAutoRefreshChange={setAutoRefresh}
              />
            </WeatherCard>
          ) : isLoading ? (
            <WeatherCardSkeleton />
          ) : (
            <div className="card empty-state">
              <p className="empty-state__title">Search for a city to see its weather.</p>
              <p className="empty-state__hint">Try “Toronto”, “Tokyo” or “São Paulo”.</p>
            </div>
          )}
        </section>

        <aside className="app__sidebar">
          <RecentSearches
            items={history.items}
            loading={history.loading}
            error={history.error}
            onSelect={handleSelect}
          />
        </aside>
      </main>
    </div>
  )
}
