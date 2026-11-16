import { useState } from 'react'
import type { PriceEstimate, PriceLine } from '../api/pricing'
import { formatMissing, formatRange } from './priceFormat'
import { usePriceStore } from './priceStore'

const CONFIDENCE: Record<PriceEstimate['confidence'], string> = { low: 'låg', medium: 'medel', high: 'hög' }

const SUBTOTALS: [keyof PriceEstimate['subtotals'], string][] = [
  ['material', 'Material'],
  ['labor', 'Arbete'],
  ['removal', 'Borttagning'],
  ['fixed', 'Fasta poster'],
]

const UNITS: Record<PriceLine['unit'], string> = { piece: 'st', meter: 'm', 'square-meter': 'm²', hour: 'h' }

const quantity = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 1 })

function Lines({ lines }: { lines: PriceLine[] }) {
  return (
    <ul className="price-lines">
      {lines.map((line, index) => (
        <li key={index}>
          <span className="label">{line.label}</span>
          <span className="quantity">
            {quantity.format(line.quantity)} {UNITS[line.unit]}
          </span>
          <span className="amount">{formatRange(line.low, line.high)}</span>
        </li>
      ))}
    </ul>
  )
}

function Estimate({ estimate }: { estimate: PriceEstimate }) {
  const [showLines, setShowLines] = useState(false)

  return (
    <>
      <p className="price-total">{formatRange(estimate.total.low, estimate.total.high)}</p>
      <p className="price-confidence">Säkerhet: {CONFIDENCE[estimate.confidence]}</p>
      {/* Only areas and lengths can be measured better; with verified measurements there is nothing to tighten. */}
      {estimate.confidence !== 'high' && <p className="price-hint">Spannet blir snävare när måtten är uppmätta.</p>}
      <dl className="price-subtotals">
        {SUBTOTALS.filter(([key]) => estimate.subtotals[key].high !== 0).map(([key, name]) => (
          <div key={key}>
            <dt>{name}</dt>
            <dd>{formatRange(estimate.subtotals[key].low, estimate.subtotals[key].high)}</dd>
          </div>
        ))}
        <div className="vat">
          <dt>varav moms</dt>
          <dd>{formatRange(estimate.vat.low, estimate.vat.high)}</dd>
        </div>
      </dl>
      {estimate.missingArticles > 0 && <p className="price-warning">{formatMissing(estimate.missingArticles)}</p>}
      {estimate.lines.length > 0 && (
        <>
          <button className="price-toggle" onClick={() => setShowLines(!showLines)}>
            {showLines ? 'Dölj rader' : 'Visa alla rader'}
          </button>
          {showLines && <Lines lines={estimate.lines} />}
        </>
      )}
    </>
  )
}

/** The approximate price of the design, top right over the 3D view. Every amount comes from the server. */
export function PricePanel() {
  const estimate = usePriceStore((s) => s.estimate)
  const loading = usePriceStore((s) => s.loading)
  const error = usePriceStore((s) => s.error)

  return (
    <aside className="price-panel" aria-live="polite">
      <h2>Ungefärligt pris</h2>
      {loading && <p className="price-loading">Räknar…</p>}
      {!loading && error === 'invalid-scene' && <p className="price-warning">Priset kan inte räknas, eftersom designen har fel.</p>}
      {!loading && error === 'unavailable' && <p className="price-warning">Priset kunde inte hämtas. Försök igen om en stund.</p>}
      {/* The old price stays, faded, while a new one is fetched; after an error it belongs to another design. */}
      {estimate && (!error || loading) && (
        <div className={loading ? 'price-body stale' : 'price-body'}>
          <Estimate estimate={estimate} />
        </div>
      )}
      <p className="price-note">Priset är ungefärligt. Den bindande offerten kommer från säljaren.</p>
    </aside>
  )
}
