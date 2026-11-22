// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { DreamImage } from '../../api/dreams'
import type { SceneView } from '../../api/scene'
import { useSaveStore } from '../saveStore'
import { createDream, FAILED, loadDreams, remaining, setSendPhotos, useDreamStore } from './dreamStore'

const view: SceneView = { id: 'v1', name: 'Från gatan', eye: { x: 10, y: -5, height: 1.7 }, target: { x: 10, y: 10, height: 1 }, fieldOfView: 50 }
const png = new Blob([new Uint8Array([1])], { type: 'image/png' })

function image(id: string, status: DreamImage['status']): DreamImage {
  return {
    id,
    status,
    viewName: 'Från gatan',
    revision: 2,
    test: true,
    createdAt: '2026-11-22T10:00:00Z',
    imageUrl: status === 'succeeded' ? `https://blob/${id}/image.jpg` : null,
    renderUrl: `https://blob/${id}/render.jpg`,
    failure: status === 'failed' ? 'Bildtjänsten avböjde.' : null,
  }
}

/** Answers the POST with job-1 and each list call with the next status of job-1. */
function server(statuses: DreamImage['status'][]) {
  let call = 0
  const fetchMock = vi.fn(async (_url: string, init?: RequestInit) => {
    if (init?.method === 'POST') return new Response(JSON.stringify({ id: 'job-1' }), { status: 202 })
    const status = statuses[Math.min(call++, statuses.length - 1)]
    return new Response(JSON.stringify([image('job-1', status)]), { status: 200 })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('the dream store', () => {
  beforeEach(() => {
    useDreamStore.setState({ gardenId: 'g1', images: [], state: 'idle', message: null, sendPhotos: true })
    useSaveStore.setState({ designId: 'd1', gardenId: 'g1', status: 'saved' })
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  it('polls every 3 s and stops when the image is done', async () => {
    vi.useFakeTimers()
    const fetchMock = server(['queued', 'running', 'succeeded', 'succeeded'])

    const done = createDream(view, png)
    expect(useDreamStore.getState().state).toBe('working')
    await vi.advanceTimersByTimeAsync(3_000 * 5)
    await done

    expect(useDreamStore.getState().state).toBe('idle')
    expect(useDreamStore.getState().images[0].status).toBe('succeeded')
    // One POST and three list calls; none after it succeeded.
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method !== 'POST')).toHaveLength(3)
  })

  it('shows the failure copy when the image fails', async () => {
    vi.useFakeTimers()
    server(['failed'])

    const done = createDream(view, png)
    await vi.advanceTimersByTimeAsync(3_000)
    await done

    expect(useDreamStore.getState().state).toBe('failed')
    expect(useDreamStore.getState().message).toBe(FAILED)
  })

  it('gives up after 5 minutes', async () => {
    vi.useFakeTimers()
    const fetchMock = server(['running'])

    const done = createDream(view, png)
    await vi.advanceTimersByTimeAsync(5 * 60_000 + 3_000)
    await done
    const calls = fetchMock.mock.calls.length
    await vi.advanceTimersByTimeAsync(30_000)

    expect(useDreamStore.getState().state).toBe('failed')
    expect(useDreamStore.getState().message).toBe(FAILED)
    expect(fetchMock.mock.calls.length).toBe(calls)
  })

  it('the quota message comes from the server code', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ code: 'quota-reached' }), { status: 409 })))

    await createDream(view, png)

    expect(useDreamStore.getState().state).toBe('failed')
    expect(useDreamStore.getState().message).toBe('Du har skapat fem drömbilder för den här trädgården.')
  })

  it('remaining ignores failed images', () => {
    expect(remaining([image('a', 'succeeded'), image('b', 'failed'), image('c', 'queued')])).toBe(3)
    expect(remaining([])).toBe(5)
  })

  it('sendPhotos defaults to true when localStorage throws', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { status: 200 })))
    useDreamStore.setState({ sendPhotos: false })

    await loadDreams('g2')
    expect(useDreamStore.getState().sendPhotos).toBe(true)
    setSendPhotos(false)
    expect(useDreamStore.getState().sendPhotos).toBe(false)
  })

  it('remembers the choice per garden', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { status: 200 })))
    await loadDreams('g3')
    setSendPhotos(false)
    expect(localStorage.getItem('dream-send-photos:g3')).toBe('false')

    await loadDreams('g4')
    expect(useDreamStore.getState().sendPhotos).toBe(true)
    await loadDreams('g3')
    expect(useDreamStore.getState().sendPhotos).toBe(false)
  })
})
