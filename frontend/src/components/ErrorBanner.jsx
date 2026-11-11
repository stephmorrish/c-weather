// role="alert" makes screen readers announce the message as soon as it appears.
export default function ErrorBanner({ error, onRetry, onDismiss }) {
  if (!error) return null

  return (
    <div className="error-banner" role="alert">
      <p className="error-banner__message">{error.message}</p>
      <div className="error-banner__actions">
        {/* Retrying won't fix a validation error, so only offer it for other errors. */}
        {error.kind !== 'validation' && onRetry && (
          <button type="button" className="button" onClick={onRetry}>
            Retry
          </button>
        )}
        <button type="button" className="button button--ghost" onClick={onDismiss} aria-label="Dismiss error">
          ✕
        </button>
      </div>
    </div>
  )
}
