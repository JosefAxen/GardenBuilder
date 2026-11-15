import type { CatalogArticle, TypeModelKey } from '../api/catalog'
import { NOMINAL_HEIGHT, type PointModelKey } from './models'

const PLANTS: TypeModelKey[] = ['deciduous-tree', 'conifer', 'shrub', 'perennial']

export const isPointModel = (key: TypeModelKey): key is PointModelKey => key in NOMINAL_HEIGHT

/**
 * How much to scale a point model. A plant is drawn at its planting height (what the customer buys), so a 0.5 m thuja
 * is 0.5 m tall; anything else uses the article's scale.
 */
export function renderScale(article: CatalogArticle): number {
  if (PLANTS.includes(article.typeModel) && article.height && isPointModel(article.typeModel)) {
    return article.height / NOMINAL_HEIGHT[article.typeModel]
  }
  return article.scale
}
