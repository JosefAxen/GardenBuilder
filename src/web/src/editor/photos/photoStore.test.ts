import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useSceneStore } from '../sceneStore'
import { FALLBACK, headingFrom, interpretPhotos, loadPhotos, pinClick, startPin, usePhotoStore } from './photoStore'
import { useReviewStore } from './reviewStore'
import { useSaveStore } from '../saveStore'

describe('pinning where a photo was taken', () => {
  beforeEach(() => {
    useSceneStore.getState().setTool({ mode: 'select' })
    usePhotoStore.setState({ gardenId: 'g1', photos: [{ id: 'p1', width: 800, height: 600, url: 'u', camera: null }] })
  })
  afterEach(() => vi.unstubAllGlobals())

  it('heading is degrees from north, clockwise', () => {
    const at = { x: 0, y: 0 }
    expect(headingFrom(at, { x: 0, y: 5 })).toBeCloseTo(0)
    expect(headingFrom(at, { x: 5, y: 0 })).toBeCloseTo(90)
    expect(headingFrom(at, { x: 0, y: -5 })).toBeCloseTo(180)
    expect(headingFrom(at, { x: -5, y: 0 })).toBeCloseTo(270)
  })

  it('the first click places the camera, the second sets the direction and saves it', async () => {
    const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    startPin('p1')
    expect(useSceneStore.getState().view).toBe('top')
    await pinClick({ x: 10, y: 0 })
    expect(useSceneStore.getState().tool).toEqual({ mode: 'pin', photoId: 'p1', position: { x: 10, y: 0 } })
    await pinClick({ x: 15, y: 0 })

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/gardens/g1/photos/p1/camera')
    expect(JSON.parse(init?.body as string)).toEqual({ x: 10, y: 0, heading: 90 })
    expect(usePhotoStore.getState().photos[0].camera).toEqual({ x: 10, y: 0, heading: 90 })
    expect(useSceneStore.getState().tool).toEqual({ mode: 'select' })
  })
})

describe('photo interpretation in the panel', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.useRealTimers()
  })

  it('a proposal from before the page was left is shown again', async () => {
    const tree = { kind: 'point', id: 't1', status: 'existing-keep', measurementSource: 'estimated', articleId: null, form: 'tree-large', position: { x: 5, y: 5 }, rotation: 0 }
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) =>
        url.endsWith('/latest')
          ? new Response(JSON.stringify({ id: 'job-1', status: 'succeeded', result: { elements: [{ label: 'Björk', confidence: 0.8, element: tree }], house: null, dropped: [], skippedPhotos: 0 }, failure: null }), { status: 200 })
          : new Response('[]', { status: 200 }),
      ),
    )

    await loadPhotos('g1')

    expect(useReviewStore.getState().proposals.map((p) => p.label)).toEqual(['Björk'])
    expect(usePhotoStore.getState().interpret).toBe('done')
  })

  it('gives up after six minutes and offers the fallback', async () => {
    vi.useFakeTimers()
    usePhotoStore.setState({ gardenId: 'g1', interpret: 'idle', message: null })
    useSaveStore.setState({ designId: 'd1' })
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) =>
        init?.method === 'POST'
          ? new Response(JSON.stringify({ id: 'job-1' }), { status: 202 })
          : new Response(JSON.stringify({ status: 'queued', result: null, failure: null }), { status: 200 }),
      ),
    )

    const running = interpretPhotos()
    await vi.advanceTimersByTimeAsync(6 * 60_000 + 5_000)
    await running

    expect(usePhotoStore.getState().interpret).toBe('failed')
    expect(usePhotoStore.getState().message).toBe(FALLBACK)
  })
})
