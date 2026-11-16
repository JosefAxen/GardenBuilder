import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { PriceEstimate } from '../api/pricing'
import type { SceneDocument } from '../api/scene'
import { schedulePrice, usePriceStore } from './priceStore'

const scene = (formatVersion = 1): SceneDocument => ({ formatVersion, elements: [] })

function estimate(total: number): PriceEstimate {
  const zero = { low: 0, high: 0 }
  return {
    lines: [],
    subtotals: { material: zero, labor: zero, removal: zero, fixed: zero },
    total: { low: total, high: total },
    vat: { low: total / 5, high: total / 5 },
    confidence: 'high',
    missingArticles: 0,
  }
}

const ok = (body: PriceEstimate) => new Response(JSON.stringify(body), { status: 200 })

/** A fetch whose answers the test hands out by hand, in any order. */
function manualFetch() {
  const calls: { init?: RequestInit; answer: (r: Response) => void }[] = []
  const fetchMock = vi.fn(
    (_url: string, init?: RequestInit) => new Promise<Response>((resolve) => calls.push({ init, answer: resolve })),
  )
  vi.stubGlobal('fetch', fetchMock)
  return { fetchMock, calls }
}

describe('schedulePrice', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    usePriceStore.setState({ estimate: null, loading: false, error: null })
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('waits half a second after the last change', async () => {
    const { fetchMock } = manualFetch()

    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(200)
    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(200)
    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(499)
    expect(fetchMock).not.toHaveBeenCalled()

    await vi.advanceTimersByTimeAsync(1)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('aborts the previous request when the scene changes', async () => {
    const { calls } = manualFetch()

    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(500)
    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(500)

    expect(calls).toHaveLength(2)
    expect(calls[0].init?.signal?.aborted).toBe(true)
    expect(calls[1].init?.signal?.aborted).toBe(false)
  })

  it('never lets an old answer replace a newer one', async () => {
    const { calls } = manualFetch()
    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(500)
    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(500)

    calls[1].answer(ok(estimate(2000)))
    await vi.advanceTimersByTimeAsync(0)
    calls[0].answer(ok(estimate(1000)))
    await vi.advanceTimersByTimeAsync(0)

    expect(usePriceStore.getState().estimate?.total.low).toBe(2000)
    expect(usePriceStore.getState().loading).toBe(false)
  })

  it('ignores an answer that arrives while waiting for the next request', async () => {
    const { calls } = manualFetch()
    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(500)

    schedulePrice(scene())
    calls[0].answer(ok(estimate(1000)))
    await vi.advanceTimersByTimeAsync(100)

    expect(usePriceStore.getState()).toMatchObject({ estimate: null, loading: true })
    expect(calls[0].init?.signal?.aborted).toBe(true)
  })

  it('keeps the old estimate while loading', async () => {
    const { calls } = manualFetch()
    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(500)
    calls[0].answer(ok(estimate(1000)))
    await vi.advanceTimersByTimeAsync(0)

    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(500)

    expect(usePriceStore.getState()).toMatchObject({ loading: true, error: null })
    expect(usePriceStore.getState().estimate?.total.low).toBe(1000)
  })

  it('reports an unreachable server as unavailable', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 500 })))

    schedulePrice(scene())
    await vi.advanceTimersByTimeAsync(500)

    expect(usePriceStore.getState()).toMatchObject({ loading: false, error: 'unavailable' })
  })
})
