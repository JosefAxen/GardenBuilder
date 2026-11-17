import type { SurfaceMaterial } from './scene'

/** The form keys, the same list as TypeModels in the Catalog module and assets/models/manifest.json. */
export type TypeModelKey =
  | 'tree-large'
  | 'tree-fruit'
  | 'tree-small'
  | 'pine'
  | 'spruce'
  | 'conifer-column'
  | 'shrub-round'
  | 'shrub-flowering'
  | 'perennial-tall'
  | 'perennial-low'
  | 'foliage-plant'
  | 'ornamental-grass'
  | 'rock'
  | 'pot'
  | 'furniture'
  | 'hedge'
  | 'fence'
  | 'surface-grass'
  | 'surface-paving'
  | 'surface-gravel'
  | 'surface-decking'
  | 'surface-bed'

export type Light = 'sun' | 'part-shade' | 'shade'

/**
 * One article as GET /api/catalog/articles returns it (ListArticles.ArticleResponse in the Catalog module).
 * Written by hand: only the scene format is generated from C#. Lengths are meters; prices SEK including VAT,
 * calculated and shown as the server sends them.
 */
export type CatalogArticle = {
  id: string
  articleNumber: string
  name: string
  /** Articles with the same variety are one plant in different sizes, e.g. "Tuja Smaragd". */
  variety: string | null
  category: string
  unit: 'piece' | 'meter' | 'square-meter'
  price: number
  currency: string
  stockStatus: 'in-stock' | 'out-of-stock'
  /** The form the article is drawn with. Only how it looks: the article is what the element is. */
  typeModel: TypeModelKey
  /** What the form stands for, e.g. 'conifer' for 'conifer-column'. Plants are the families of trees, shrubs and perennials. */
  family: string
  geometry: 'point' | 'line' | 'surface'
  material: SurfaceMaterial | null
  scale: number
  color: string
  accentColor: string | null
  hardinessZone: number | null
  /** Plants and hedges: planting height (as delivered). Fences: fence height. */
  height: number | null
  /** Hedges and fences: the line's width. */
  width: number | null
  matureHeight: number | null
  matureWidth: number | null
  clipHeight: number | null
  growthCmPerYear: number | null
  light: Light[]
}

/**
 * Fetches the catalog. Throws when the server does not answer 200, or does not answer at all within the time limit:
 * a hanging server must end in an error the editor can show, not in "loading" forever.
 */
export async function fetchArticles({ timeoutMs = 10_000 }: { timeoutMs?: number } = {}): Promise<CatalogArticle[]> {
  const response = await fetch('/api/catalog/articles', { signal: AbortSignal.timeout(timeoutMs) })
  if (!response.ok) throw new Error(`The catalog could not be fetched: HTTP ${response.status}`)
  return (await response.json()) as CatalogArticle[]
}
