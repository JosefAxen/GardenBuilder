import { afterEach, describe, expect, it, vi } from 'vitest'
import { DreamError, deleteDream, listDreams, startDream } from './dreams'

function answer(status: number, body: unknown) {
  const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => new Response(body === undefined ? null : JSON.stringify(body), { status }))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const png = new Blob([new Uint8Array([137, 80, 78, 71])], { type: 'image/png' })

describe('dreams api', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('sends the four fields as a multipart form', async () => {
    const fetchMock = answer(202, { id: 'job-1' })

    expect(await startDream('g1', 'd1', 'v1', false, png)).toBe('job-1')

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/gardens/g1/dream-images')
    expect(init!.method).toBe('POST')
    const form = init!.body as FormData
    expect(form.get('designId')).toBe('d1')
    expect(form.get('viewId')).toBe('v1')
    expect(form.get('sendPhotos')).toBe('false')
    expect(form.get('render')).toBeInstanceOf(Blob)
  })

  it('quota-reached says so in Swedish', async () => {
    answer(409, { code: 'quota-reached' })

    const error = await startDream('g1', 'd1', 'v1', true, png).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(DreamError)
    expect((error as DreamError).message).toBe('Du har skapat fem drömbilder för den här trädgården.')
  })

  it.each(['invalid-image', 'no-such-view', 'budget-reached', 'step-off'])('%s has its own Swedish message', async (code) => {
    answer(400, { code })

    const error = (await startDream('g1', 'd1', 'v1', true, png).catch((e: unknown) => e)) as DreamError

    expect(error.code).toBe(code)
    expect(error.message).not.toBe('Något gick fel. Försök igen om en stund.')
  })

  it('lists the gallery and deletes one image', async () => {
    const fetchMock = answer(200, [{ id: 'job-1', status: 'succeeded' }])
    expect(await listDreams('g1')).toEqual([{ id: 'job-1', status: 'succeeded' }])
    expect(fetchMock.mock.calls[0][0]).toBe('/api/gardens/g1/dream-images')

    const deleteMock = answer(204, undefined)
    await deleteDream('g1', 'job-1')
    expect(deleteMock.mock.calls[0][0]).toBe('/api/gardens/g1/dream-images/job-1')
    expect(deleteMock.mock.calls[0][1]!.method).toBe('DELETE')
  })
})
