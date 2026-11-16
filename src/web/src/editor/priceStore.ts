import { create } from 'zustand'
import { fetchEstimate, PricingError, type PriceEstimate } from '../api/pricing'
import type { SceneDocument } from '../api/scene'

/** How long the scene must be still before the price is fetched, so a drag does not send a request per frame. */
const DEBOUNCE_MS = 500

type PriceState = {
  /** The latest price. Kept while a new one is fetched, so the panel does not flicker. */
  estimate: PriceEstimate | null
  loading: boolean
  error: PricingError['reason'] | null
}

/** The editor's live price. Separate from the scene store: a price is not a change to the design and is not undone. */
export const usePriceStore = create<PriceState>(() => ({ estimate: null, loading: false, error: null }))

let timer: ReturnType<typeof setTimeout> | undefined
let controller: AbortController | undefined
/** Numbers each request, so an answer that arrives after a newer request was sent is thrown away. */
let latest = 0

/**
 * Fetches the price of the scene half a second after the last call. The moment the scene changes, an answer still on
 * its way belongs to an old design: its request is aborted and its number is no longer the latest.
 */
export function schedulePrice(scene: SceneDocument) {
  clearTimeout(timer)
  controller?.abort()
  const request = ++latest
  usePriceStore.setState({ loading: true })
  timer = setTimeout(() => void fetchPrice(scene, request), DEBOUNCE_MS)
}

async function fetchPrice(scene: SceneDocument, request: number) {
  controller = new AbortController()
  try {
    const estimate = await fetchEstimate(scene, { signal: controller.signal })
    if (request === latest) usePriceStore.setState({ estimate, loading: false, error: null })
  } catch (error) {
    if (request !== latest) return
    usePriceStore.setState({ loading: false, error: error instanceof PricingError ? error.reason : 'unavailable' })
  }
}
