import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { findArticle, useCatalogStore } from './catalogStore'
import { thuja } from './testArticles'

function respondWith(status: number, body: unknown) {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(body), { status })))
}

describe('the catalog store', () => {
  beforeEach(() => useCatalogStore.setState({ status: 'loading', articles: [] }))
  afterEach(() => vi.unstubAllGlobals())

  it('loads articles and finds them by id', async () => {
    respondWith(200, [thuja])

    await useCatalogStore.getState().load()

    expect(useCatalogStore.getState().status).toBe('ready')
    expect(findArticle('thuja')?.name).toBe('Tuja Smaragd 40–60 cm')
  })

  it('ends in error, not a crash, when the API fails', async () => {
    respondWith(500, { title: 'Internal Server Error' })

    await useCatalogStore.getState().load()

    expect(useCatalogStore.getState().status).toBe('error')
    expect(useCatalogStore.getState().articles).toEqual([])
  })

  it('finds nothing for an unknown article id', () => {
    expect(findArticle('nope')).toBeUndefined()
    expect(findArticle(null)).toBeUndefined()
  })
})
