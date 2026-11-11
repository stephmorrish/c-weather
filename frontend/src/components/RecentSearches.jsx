import { formatRegion, timeAgo } from '../utils/format.js'

export default function RecentSearches({ items, loading, error, onSelect }) {
  return (
    <section className="card recent" aria-labelledby="recent-heading">
      <h2 id="recent-heading" className="recent__heading">Recent searches</h2>

      {/* Conditional rendering: && renders the right side only when the left is true. */}
      {loading && <p className="recent__note">Loading…</p>}
      {!loading && error && <p className="recent__note">History is unavailable right now.</p>}
      {!loading && !error && items.length === 0 && (
        <p className="recent__note">Places you search for will appear here.</p>
      )}

      {items.length > 0 && (
        <ul className="recent__list">
          {/* key tells React which item is which between renders. Use a stable id, not the index. */}
          {items.map((item) => (
            <li key={item.locationId}>
              <button type="button" className="recent__item" onClick={() => onSelect(item)}>
                <span className="recent__name">{item.name}</span>
                <span className="recent__meta">
                  {formatRegion(item)} · {timeAgo(item.lastSearchedAt)}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
