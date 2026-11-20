import { ChevronDown } from 'lucide-react'
import { useEditorUiStore } from '../editorUiStore'
import { formatRange } from '../priceFormat'
import { usePriceStore } from '../priceStore'

const ERRORS = {
  'invalid-scene': 'Priset kan inte räknas, eftersom designen har fel.',
  unavailable: 'Priset kunde inte hämtas. Försök igen om en stund.',
} as const

/**
 * The approximate price, right in the fir header. Always a range with "uppskattade mått", never one exact number;
 * a click opens the details. Every amount comes from the server.
 */
export function PriceSummary() {
  const estimate = usePriceStore((s) => s.estimate)
  const loading = usePriceStore((s) => s.loading)
  const error = usePriceStore((s) => s.error)
  const open = useEditorUiStore((s) => s.priceOpen)
  // After an error the old estimate belongs to another design, so it is only shown while loading.
  const shown = estimate && (!error || loading) ? estimate : null

  return (
    <button
      type="button"
      className="price-summary"
      aria-expanded={open}
      aria-controls="price-details"
      aria-describedby={error && !loading ? 'price-summary-error' : undefined}
      onClick={() => useEditorUiStore.getState().setPriceOpen(!open)}
    >
      <span className="price-summary__label">
        Ungefärligt pris
        <ChevronDown aria-hidden size={14} strokeWidth={2} className={open ? 'price-summary__chevron is-open' : 'price-summary__chevron'} />
      </span>
      {shown && <span className={loading ? 'price-summary__value num is-stale' : 'price-summary__value num'}>{formatRange(shown.total.low, shown.total.high)}</span>}
      {loading ? (
        <span className="price-summary__note" role="status">
          Räknar…
        </span>
      ) : error ? (
        <span id="price-summary-error" className="price-summary__note price-summary__note--error" role="alert">
          {ERRORS[error]}
        </span>
      ) : (
        shown && <span className="price-summary__note">inkl. moms · uppskattade mått</span>
      )}
    </button>
  )
}
