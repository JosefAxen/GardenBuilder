import { afterEach, describe, expect, it, vi } from 'vitest'
import type { SceneDocument } from './scene'
import { fetchEstimate, PricingError } from './pricing'

const scene: SceneDocument = { formatVersion: 1, elements: [] }

describe('fetchEstimate', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('gives up when the server never answers', async () => {
    // A server that hangs: the request only ends when it is aborted.
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (_url: string, init?: RequestInit) =>
          new Promise<Response>((_resolve, reject) => init?.signal?.addEventListener('abort', () => reject(init.signal?.reason))),
      ),
    )

    const error = await fetchEstimate(scene, { timeoutMs: 50 }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(PricingError)
    expect((error as PricingError).reason).toBe('unavailable')
  })

  it('reports a 400 as invalid-scene', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ code: 'invalid-scene', problems: [] }), { status: 400 })))

    const error = await fetchEstimate(scene).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(PricingError)
    expect((error as PricingError).reason).toBe('invalid-scene')
  })

  it('sends the scene as JSON', async () => {
    const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => new Response(JSON.stringify({ total: { low: 1, high: 2 } }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)

    const estimate = await fetchEstimate(scene)

    expect(estimate.total).toEqual({ low: 1, high: 2 })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/pricing/estimate')
    expect(init?.method).toBe('POST')
    expect(init?.body).toBe(JSON.stringify(scene))
  })
})
