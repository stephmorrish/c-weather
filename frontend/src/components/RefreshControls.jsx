import { useNow } from '../hooks/useNow.js'
import { timeAgo } from '../utils/format.js'

const INTERVAL_MINUTES = [1, 5, 10, 15]

function formatCountdown(ms) {
  const totalSeconds = Math.max(0, Math.ceil(ms / 1000))
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = String(totalSeconds % 60).padStart(2, '0')
  return `${minutes}:${seconds}`
}

export default function RefreshControls({
  fetchedAt,
  isLoading,
  onRefresh,
  autoRefresh,
  nextRefreshAt,
  onAutoRefreshChange,
}) {
  // Ticks every second, so "updated 2 minutes ago" and the countdown stay current.
  const now = useNow(1000)

  return (
    <div className="refresh">
      <p className="refresh__status">
        {isLoading ? 'Refreshing…' : `Updated ${timeAgo(fetchedAt, now)}`}
        {!isLoading && nextRefreshAt && (
          <span className="refresh__next"> · next in {formatCountdown(nextRefreshAt - now)}</span>
        )}
      </p>

      <div className="refresh__actions">
        <label className="toggle">
          <input
            type="checkbox"
            className="toggle__input"
            checked={autoRefresh.enabled}
            onChange={(e) => onAutoRefreshChange({ enabled: e.target.checked })}
          />
          <span className="toggle__track" aria-hidden="true" />
          Auto-refresh
        </label>

        {/* A "controlled" select: React state decides its value, and onChange updates the state. */}
        <select
          className="refresh__interval"
          aria-label="Auto-refresh interval"
          value={autoRefresh.intervalMs}
          disabled={!autoRefresh.enabled}
          onChange={(e) => onAutoRefreshChange({ intervalMs: Number(e.target.value) })}
        >
          {INTERVAL_MINUTES.map((minutes) => (
            <option key={minutes} value={minutes * 60_000}>
              every {minutes} min
            </option>
          ))}
        </select>

        <button type="button" className="button button--secondary" onClick={onRefresh} disabled={isLoading}>
          <span className={`refresh__icon ${isLoading ? 'refresh__icon--spinning' : ''}`} aria-hidden="true">
            ↻
          </span>
          Refresh
        </button>
      </div>
    </div>
  )
}
