import type { CatalogArticle } from '../api/catalog'
import type { SceneElement } from '../api/scene'
import { findArticle } from './catalogStore'

/**
 * How many elements point at an article that is not in the catalog (discontinued, or removed). They are not drawn,
 * so the status bar says so instead of letting them disappear silently. Elements without an article (the plot, an
 * existing lawn) are not counted.
 */
export function countMissingArticles(elements: SceneElement[], articles: CatalogArticle[]): number {
  return elements.filter((e) => 'articleId' in e && e.articleId !== null && !findArticle(e.articleId, articles)).length
}
