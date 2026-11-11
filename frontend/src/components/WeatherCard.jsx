import WeatherAnimation from './WeatherAnimation.jsx'
import {
  formatPrecip,
  formatRegion,
  formatSpeed,
  formatTemp,
  formatTime,
} from '../utils/format.js'

// Presentational: it receives data through props and renders it, with no fetching
// and no state.
//
// `children` is whatever App puts between <WeatherCard> and </WeatherCard>. Here that's
// the refresh controls. This is "composition": the card doesn't need to know what goes
// in its footer.
export default function WeatherCard({ weather, isUpdating, children }) {
  const { location, current, fetchedAt, fromCache, isStale } = weather

  return (
    <article
      className={`card weather-card ${isUpdating ? 'weather-card--updating' : ''}`}
      aria-busy={isUpdating}
    >
      <div className="weather-card__hero">
        <WeatherAnimation condition={current.condition} isDay={current.isDay} />

        <div className="weather-card__hero-content">
          <header className="weather-card__header">
            <div>
              <h2 className="weather-card__place">{location.name}</h2>
              <p className="weather-card__region">{formatRegion(location)}</p>
            </div>
            <div className="weather-card__badges">
              {isStale ? (
                <span className="badge badge--warning" title="The weather service is unavailable">
                  Offline data
                </span>
              ) : (
                fromCache && <span className="badge badge--glass" title="Saved within the last 10 minutes">Cached</span>
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
        </div>
      </div>

      <div className="weather-card__body">
        {isStale && (
          <p className="weather-card__stale">
            Showing the last reading from {formatTime(fetchedAt)}. The weather service is unavailable.
          </p>
        )}

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

        {children}
      </div>
    </article>
  )
}

// Shown on the very first load, before there's any data. It has the same shape as the
// real card, so the page doesn't jump when the data arrives.
export function WeatherCardSkeleton() {
  return (
    <article className="card weather-card weather-card--skeleton" aria-busy="true" aria-label="Loading weather">
      <div className="skeleton skeleton--hero" />
      <div className="weather-card__body">
        <div className="skeleton skeleton--row" />
      </div>
    </article>
  )
}
