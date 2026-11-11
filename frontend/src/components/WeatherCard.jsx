import {
  formatPrecip,
  formatRegion,
  formatSpeed,
  formatTemp,
  formatTime,
} from '../utils/format.js'

// Purely presentational: it receives data through props and renders it.
// No fetching and no state, which makes it easy to reason about.
export default function WeatherCard({ weather, isUpdating }) {
  const { location, current, fetchedAt, fromCache, isStale } = weather

  return (
    <article
      className={`card weather-card ${isUpdating ? 'weather-card--updating' : ''}`}
      data-condition={current.condition}
      data-daytime={current.isDay ? 'day' : 'night'}
      aria-busy={isUpdating}
    >
      <header className="weather-card__header">
        <div>
          <h2 className="weather-card__place">{location.name}</h2>
          <p className="weather-card__region">{formatRegion(location)}</p>
        </div>
        <div className="weather-card__badges">
          {isUpdating && <span className="badge">Updating…</span>}
          {isStale ? (
            <span className="badge badge--warning" title="The weather service is unavailable">
              Offline data
            </span>
          ) : (
            fromCache && <span className="badge" title="Saved within the last 10 minutes">Cached</span>
          )}
        </div>
      </header>

      <div className="weather-card__main">
        <p className="weather-card__temp">{formatTemp(current.temperature)}</p>
        <div>
          <p className="weather-card__description">{current.description}</p>
          <p className="weather-card__feels">Feels like {formatTemp(current.feelsLike)}</p>
        </div>
      </div>

      {/* <dl> (description list) is the semantic HTML element for label/value pairs. */}
      <dl className="weather-card__details">
        <div>
          <dt>Humidity</dt>
          <dd>{current.humidity}%</dd>
        </div>
        <div>
          <dt>Wind</dt>
          <dd>{formatSpeed(current.windSpeed)}</dd>
        </div>
        <div>
          <dt>Precipitation</dt>
          <dd>{formatPrecip(current.precipitation)}</dd>
        </div>
        <div>
          <dt>Observed</dt>
          <dd>{formatTime(current.observedAt)}</dd>
        </div>
      </dl>

      <footer className="weather-card__footer">
        {isStale
          ? `Showing the last reading from ${formatTime(fetchedAt)}. The weather service is unavailable.`
          : `Updated ${formatTime(fetchedAt)}`}
      </footer>
    </article>
  )
}

// Shown on the very first load, before there's any data. It has the same shape as the
// real card, so the page doesn't jump when the data arrives.
export function WeatherCardSkeleton() {
  return (
    <article className="card weather-card weather-card--skeleton" aria-busy="true" aria-label="Loading weather">
      <div className="skeleton skeleton--title" />
      <div className="skeleton skeleton--subtitle" />
      <div className="skeleton skeleton--temp" />
      <div className="skeleton skeleton--row" />
    </article>
  )
}
