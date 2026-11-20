import { useEffect, useRef, useState } from 'react'
import type { PriceEstimate, PriceLine } from '../../api/pricing'
import { Button, Card, Notice } from '../../ui'
import { useEditorUiStore } from '../editorUiStore'
import { formatMissing, formatRange } from '../priceFormat'
import { usePriceStore } from '../priceStore'

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
          <span className="price-lines__label">{line.label}</span>
          <span className="price-lines__quantity num">
            {quantity.format(line.quantity)} {UNITS[line.unit]}
          </span>
          <span className="price-lines__amount num">{formatRange(line.low, line.high)}</span>
        </li>
      ))}
    </ul>
  )
}

function Estimate({ estimate }: { estimate: PriceEstimate }) {
  const [showLines, setShowLines] = useState(false)

  return (
    <>
      <p className="price-details__total num">{formatRange(estimate.total.low, estimate.total.high)}</p>
      <p className="price-details__confidence">
        <span className={`confidence confidence--${estimate.confidence}`} aria-hidden />
        Säkerhet: {CONFIDENCE[estimate.confidence]}
      </p>
      {/* Only areas and lengths can be measured better; with verified measurements there is nothing to tighten. */}
      {estimate.confidence !== 'high' && <p className="price-details__hint">Spannet blir snävare när måtten är uppmätta.</p>}
      <dl className="price-subtotals">
        {SUBTOTALS.filter(([key]) => estimate.subtotals[key].high !== 0).map(([key, name]) => (
          <div key={key}>
            <dt>{name}</dt>
            <dd className="num">{formatRange(estimate.subtotals[key].low, estimate.subtotals[key].high)}</dd>
          </div>
        ))}
        <div className="price-subtotals__vat">
          <dt>varav moms</dt>
          <dd className="num">{formatRange(estimate.vat.low, estimate.vat.high)}</dd>
        </div>
      </dl>
      {estimate.missingArticles > 0 && <Notice tone="warn">{formatMissing(estimate.missingArticles)}</Notice>}
      {estimate.lines.length > 0 && (
        <>
          <Button size="s" variant="quiet" className="price-details__toggle" aria-expanded={showLines} onClick={() => setShowLines(!showLines)}>
            {showLines ? 'Dölj rader' : 'Visa alla rader'}
          </Button>
          {showLines && <Lines lines={estimate.lines} />}
        </>
      )}
    </>
  )
}

/**
 * The price broken down, under the header on the right. Closes with Esc (through the editor's Esc), a click outside,
 * or another click on the price.
 */
export function PriceDetails() {
  const open = useEditorUiStore((s) => s.priceOpen)
  const estimate = usePriceStore((s) => s.estimate)
  const loading = usePriceStore((s) => s.loading)
  const error = usePriceStore((s) => s.error)
  const card = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Element
      // The summary button toggles by itself; anything else outside the card closes it.
      if (card.current?.contains(target) || target.closest?.('.price-summary')) return
      useEditorUiStore.getState().setPriceOpen(false)
    }
    window.addEventListener('pointerdown', onPointerDown)
    return () => window.removeEventListener('pointerdown', onPointerDown)
  }, [open])

  if (!open) return null
  const shown = estimate && (!error || loading) ? estimate : null

  return (
    <div ref={card} className="price-details-anchor">
      <Card as="section" id="price-details" aria-label="Prisdetaljer" className={loading ? 'price-details is-stale' : 'price-details'}>
        {shown ? <Estimate estimate={shown} /> : <p className="price-details__hint">Inget pris att visa ännu.</p>}
        <p className="price-details__note">Priset är ungefärligt. Den bindande offerten kommer från säljaren.</p>
      </Card>
    </div>
  )
}
