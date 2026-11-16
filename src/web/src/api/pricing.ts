import type { SceneDocument } from './scene'

export type PriceRange = { low: number; high: number }

/** One line of the estimate. Amounts are SEK including VAT, as a range. */
export type PriceLine = {
  kind: 'material' | 'labor' | 'removal' | 'fixed'
  label: string
  quantity: number
  unit: 'piece' | 'meter' | 'square-meter' | 'hour'
  low: number
  high: number
}

/**
 * The answer from POST /api/pricing/estimate (EstimatePrice.EstimateResponse in the Pricing module). Written by hand,
 * like the catalog types. The server calculates every amount; the editor only shows them.
 */
export type PriceEstimate = {
  lines: PriceLine[]
  subtotals: { material: PriceRange; labor: PriceRange; removal: PriceRange; fixed: PriceRange }
  total: PriceRange
  /** The VAT part of the total, which already includes VAT. */
  vat: PriceRange
  confidence: 'low' | 'medium' | 'high'
  /** New elements whose article is not in the catalog. They are not in the price. */
  missingArticles: number
}

/** Why there is no price: the design has errors, or the server could not be reached in time. */
export class PricingError extends Error {
  readonly reason: 'invalid-scene' | 'unavailable'

  constructor(reason: 'invalid-scene' | 'unavailable', message: string) {
    super(message)
    this.name = 'PricingError'
    this.reason = reason
  }
}

/**
 * Asks the server for the price of a scene. Throws PricingError, except when the caller aborts through `signal`: then
 * the abort error comes through as it is, since the caller already knows.
 */
export async function fetchEstimate(
  scene: SceneDocument,
  { signal, timeoutMs = 10_000 }: { signal?: AbortSignal; timeoutMs?: number } = {},
): Promise<PriceEstimate> {
  const timeout = AbortSignal.timeout(timeoutMs)
  let response: Response
  try {
    response = await fetch('/api/pricing/estimate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(scene),
      signal: signal ? AbortSignal.any([signal, timeout]) : timeout,
    })
  } catch (error) {
    if (signal?.aborted) throw error
    throw new PricingError('unavailable', 'The price could not be fetched: no answer from the server.')
  }

  if (response.status === 400) throw new PricingError('invalid-scene', 'The scene has errors, so it cannot be priced.')
  if (!response.ok) throw new PricingError('unavailable', `The price could not be fetched: HTTP ${response.status}`)
  return (await response.json()) as PriceEstimate
}
