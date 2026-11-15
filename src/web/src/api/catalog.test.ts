import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchArticles } from './catalog'

describe('fetchArticles', () => {
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

    await expect(fetchArticles({ timeoutMs: 50 })).rejects.toThrow()
  })
})
