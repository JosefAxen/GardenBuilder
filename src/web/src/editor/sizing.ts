import type { CatalogArticle } from '../api/catalog'

const PLANT_FAMILIES = new Set(['deciduous-tree', 'conifer', 'shrub', 'perennial', 'hedge'])

/** Plants are drawn at their planting height; everything else at the article's scale. */
export const isPlant = (article: CatalogArticle) => PLANT_FAMILIES.has(article.family)

/**
 * How much to scale a point model that is `nominalHeight` meters tall at scale 1. A plant is drawn at its planting
 * height (what the customer buys), so a 0.5 m thuja is 0.5 m tall; anything else uses the article's scale.
 */
export function renderScale(article: CatalogArticle, nominalHeight: number): number {
  if (isPlant(article) && article.height) return article.height / nominalHeight
  return article.scale
}
