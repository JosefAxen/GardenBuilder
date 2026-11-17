import { afterEach, describe, expect, it, vi } from 'vitest'
import type { SceneDocument } from './scene'
import { createDesign, DesignError, fetchLatestDesign, saveWorkingCopy } from './designs'

const scene: SceneDocument = { formatVersion: 1, elements: [] }

function answer(response: () => Response) {
  const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => response())
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('designs api', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('gives null when there is no latest design', async () => {
    answer(() => new Response(null, { status: 404 }))

    expect(await fetchLatestDesign()).toBeNull()
  })

  it('reports a 412 as a conflict with the server revision', async () => {
    answer(() => new Response(JSON.stringify({ code: 'conflict', revision: 5 }), { status: 412 }))

    const error = await saveWorkingCopy('d1', 3, scene).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(DesignError)
    expect((error as DesignError).reason).toBe('conflict')
    expect((error as DesignError).revision).toBe(5)
  })

  it('reports a 409 on create as a conflict with the existing design', async () => {
    answer(() => new Response(JSON.stringify({ code: 'design-exists', id: 'd9', revision: 3 }), { status: 409 }))

    const error = await createDesign(scene).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(DesignError)
    expect((error as DesignError).reason).toBe('conflict')
    expect((error as DesignError).designId).toBe('d9')
    expect((error as DesignError).revision).toBe(3)
  })

  it('sends the revision in If-Match and reads the new one from ETag', async () => {
    const fetchMock = answer(
      () => new Response(JSON.stringify({ id: 'd1', revision: 7, scene }), { status: 200, headers: { ETag: '"7"' } }),
    )

    const revision = await saveWorkingCopy('d1', 3, scene)

    expect(revision).toBe(7)
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/designs/d1/working-copy')
    expect(init?.method).toBe('PUT')
    expect(new Headers(init?.headers).get('If-Match')).toBe('"3"')
    expect(init?.credentials).toBe('same-origin')
  })

  it('reports a network error as unavailable', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new TypeError('Failed to fetch'))))

    const error = await saveWorkingCopy('d1', 1, scene).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(DesignError)
    expect((error as DesignError).reason).toBe('unavailable')
  })

  it('reports a 400 as invalid and a 500 as unavailable', async () => {
    answer(() => new Response(JSON.stringify({ code: 'invalid-scene', problems: [] }), { status: 400 }))
    expect(((await saveWorkingCopy('d1', 1, scene).catch((e: unknown) => e)) as DesignError).reason).toBe('invalid')

    answer(() => new Response(null, { status: 500 }))
    expect(((await saveWorkingCopy('d1', 1, scene).catch((e: unknown) => e)) as DesignError).reason).toBe('unavailable')
  })
})
