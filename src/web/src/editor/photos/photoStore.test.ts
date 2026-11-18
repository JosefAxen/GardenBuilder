import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useSceneStore } from '../sceneStore'
import { headingFrom, pinClick, startPin, usePhotoStore } from './photoStore'

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
