// Small display helpers. Intl.* formats numbers and dates for the user's own locale,
// so a German browser sees "5,3" and a US one sees "5.3", with no extra code.

const wholeNumber = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 })
const oneDecimal = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 })

export const formatTemp = (celsius) => `${wholeNumber.format(celsius)}°C`
export const formatSpeed = (kmh) => `${wholeNumber.format(kmh)} km/h`
export const formatPrecip = (mm) => `${oneDecimal.format(mm)} mm`

export const formatTime = (iso) =>
  new Date(iso).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })

// "São Paulo, São Paulo, Brazil" → "São Paulo, Brazil". A Set drops duplicates.
export function formatPlace({ name, admin1, country }) {
  return [...new Set([name, admin1, country].filter(Boolean))].join(', ')
}

// Region and country without repeating the name, for subtitles.
export function formatRegion({ name, admin1, country }) {
  return [...new Set([admin1, country].filter((part) => part && part !== name))].join(', ')
}

const relative = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' })

// "5 minutes ago", "yesterday", ...
export function timeAgo(iso, now = Date.now()) {
  const seconds = Math.round((new Date(iso).getTime() - now) / 1000)
  const abs = Math.abs(seconds)
  if (abs < 60) return relative.format(0, 'second')          // "now"
  if (abs < 3600) return relative.format(Math.round(seconds / 60), 'minute')
  if (abs < 86400) return relative.format(Math.round(seconds / 3600), 'hour')
  return relative.format(Math.round(seconds / 86400), 'day')
}
