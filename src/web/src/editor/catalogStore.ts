import { create } from 'zustand'
import { type CatalogArticle, fetchArticles } from '../api/catalog'

type CatalogState = {
  status: 'loading' | 'ready' | 'error'
  articles: CatalogArticle[]
  /** Fetches the catalog. Never throws: a failed fetch ends in status 'error' so the editor can say so. */
  load: () => Promise<void>
}

/** The catalog the editor builds from, fetched from the server. Separate from the scene: it is not part of undo. */
export const useCatalogStore = create<CatalogState>((set) => ({
  status: 'loading',
  articles: [],
  load: async () => {
    set({ status: 'loading' })
    try {
      set({ status: 'ready', articles: await fetchArticles() })
    } catch {
      set({ status: 'error', articles: [] })
    }
  },
}))

let indexed: { articles: CatalogArticle[]; byId: Map<string, CatalogArticle> } | null = null

/**
 * The article with this id, or undefined (no id, not loaded, or no longer in the catalog). Inside a useMemo, pass the
 * articles from useCatalogStore so the memo recalculates when the catalog arrives.
 */
export function findArticle(
  id: string | null | undefined,
  articles: CatalogArticle[] = useCatalogStore.getState().articles,
): CatalogArticle | undefined {
  if (!id) return undefined
  if (indexed?.articles !== articles) indexed = { articles, byId: new Map(articles.map((a) => [a.id, a])) }
  return indexed.byId.get(id)
}
