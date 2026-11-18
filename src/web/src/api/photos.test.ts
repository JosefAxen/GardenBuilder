import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchInterpretation, listPhotos, PhotoError, setCamera, startInterpretation, uploadPhoto } from './photos'

function answer(status: number, body: unknown) {
  const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => new Response(body === undefined ? null : JSON.stringify(body), { status }))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('photos api', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('uploads the file as multipart field "file"', async () => {
    const fetchMock = answer(201, { id: 'p1', width: 800, height: 600, url: 'https://x/p1.jpg', camera: null })

    const photo = await uploadPhoto('g1', new File(['jpeg'], 'tradgard.jpg', { type: 'image/jpeg' }))

    expect(photo.id).toBe('p1')
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/gardens/g1/photos')
    expect(init?.method).toBe('POST')
    expect((init!.body as FormData).get('file')).toBeInstanceOf(File)
  })

  it('too-many-photos becomes a PhotoError with that code and a Swedish message', async () => {
    answer(409, { code: 'too-many-photos' })

    const error = await uploadPhoto('g1', new File(['x'], 'a.jpg')).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(PhotoError)
    expect((error as PhotoError).code).toBe('too-many-photos')
    expect((error as PhotoError).message).toBe('Du kan ladda upp högst 6 foton.')
  })

  it('lists photos and saves a camera', async () => {
    const fetchMock = answer(200, [{ id: 'p1', width: 800, height: 600, url: 'u', camera: { x: 1, y: 2, heading: 90 } }])
    expect((await listPhotos('g1'))[0].camera).toEqual({ x: 1, y: 2, heading: 90 })

    answer(204, undefined)
    await setCamera('g1', 'p1', { x: 3, y: 4, heading: 180 })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/gardens/g1/photos')
  })

  it('starts an interpretation and reads it back', async () => {
    const start = answer(202, { id: 'job-1' })
    expect(await startInterpretation('g1', 'd1')).toBe('job-1')
    expect(JSON.parse(start.mock.calls[0][1]?.body as string)).toEqual({ designId: 'd1' })

    answer(200, { status: 'running', result: null, failure: null })
    expect((await fetchInterpretation('g1', 'job-1')).status).toBe('running')
  })

  it('a missing camera says so in Swedish', async () => {
    answer(409, { code: 'camera-missing' })

    await expect(startInterpretation('g1', 'd1')).rejects.toThrow('Visa var du stod för varje foto först.')
  })
})
