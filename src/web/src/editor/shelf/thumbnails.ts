import { create } from 'zustand'
import type { CatalogArticle } from '../../api/catalog'
import { isPointForm } from '../modelLibrary'

/** Articles drawn with the same model and colours look the same, so they share one picture. */
export function thumbnailKey(article: CatalogArticle): string {
  return `${article.typeModel}|${article.color}|${article.accentColor ?? ''}`
}

/** Only point articles (plants, stones, pots, furniture) have a model to draw; lines and surfaces keep their swatch. */
export const hasThumbnail = (article: CatalogArticle) => article.geometry === 'point' && isPointForm(article.typeModel)

/**
 * Pictures by key: `render` is called once per key, also when several cards ask at the same time. A failed render is
 * forgotten, so the next request tries again.
 */
export function createThumbnailCache(render: (article: CatalogArticle) => Promise<string>) {
  const pictures = new Map<string, Promise<string>>()
  return {
    get(article: CatalogArticle): Promise<string> {
      const key = thumbnailKey(article)
      let picture = pictures.get(key)
      if (!picture) {
        picture = render(article)
        pictures.set(key, picture)
        picture.catch(() => pictures.delete(key))
      }
      return picture
    },
  }
}

type Request = { id: number; article: CatalogArticle; resolve: (url: string) => void; reject: (error: unknown) => void }

let requests = 0

/** Pictures waiting to be drawn, one at a time, by the ThumbnailStudio's own small renderer. */
export const useThumbnailQueue = create<{ queue: Request[] }>(() => ({ queue: [] }))

/** Asks the studio for a picture of the article: a PNG data URL, or a rejection when WebGL cannot draw it. */
export function renderThumbnail(article: CatalogArticle): Promise<string> {
  return new Promise((resolve, reject) => {
    useThumbnailQueue.setState((s) => ({ queue: [...s.queue, { id: ++requests, article, resolve, reject }] }))
  })
}

/** Called by the studio when the first picture in the queue is done (or failed). */
export function finishThumbnail(result: { url: string } | { error: unknown }) {
  const [first, ...rest] = useThumbnailQueue.getState().queue
  if (!first) return
  useThumbnailQueue.setState({ queue: rest })
  if ('url' in result) first.resolve(result.url)
  else first.reject(result.error)
}

export const thumbnails = createThumbnailCache(renderThumbnail)
