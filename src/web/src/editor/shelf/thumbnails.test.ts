import { describe, expect, it, vi } from 'vitest'
import { thuja } from '../testArticles'
import { createThumbnailCache, thumbnailKey } from './thumbnails'

describe('thumbnail keys', () => {
  it('articles with the same model and colours share a picture', () => {
    expect(thumbnailKey({ ...thuja, id: 'a' })).toBe(thumbnailKey({ ...thuja, id: 'b', name: 'Annan' }))
  })

  it('a different accent colour is a different picture', () => {
    expect(thumbnailKey(thuja)).not.toBe(thumbnailKey({ ...thuja, accentColor: '#ff0000' }))
  })
})

describe('the thumbnail cache', () => {
  it('renders each picture once, even when asked three times at once', async () => {
    const render = vi.fn(async () => 'data:image/png;base64,AAA')
    const cache = createThumbnailCache(render)

    const results = await Promise.all([cache.get(thuja), cache.get({ ...thuja, id: 'x' }), cache.get(thuja)])

    expect(render).toHaveBeenCalledTimes(1)
    expect(results).toEqual(['data:image/png;base64,AAA', 'data:image/png;base64,AAA', 'data:image/png;base64,AAA'])
  })

  it('a failed render is tried again next time', async () => {
    const render = vi.fn().mockRejectedValueOnce(new Error('no WebGL')).mockResolvedValueOnce('data:image/png;base64,BBB')
    const cache = createThumbnailCache(render)

    await expect(cache.get(thuja)).rejects.toThrow('no WebGL')
    await expect(cache.get(thuja)).resolves.toBe('data:image/png;base64,BBB')
    expect(render).toHaveBeenCalledTimes(2)
  })
})
