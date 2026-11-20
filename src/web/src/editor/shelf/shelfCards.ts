import type { CatalogArticle } from '../../api/catalog'
import type { PanelCategory } from '../panelGroups'

/** One card on the catalogue shelf: an article, with the variety as its title when the variety has several sizes. */
export type ShelfCard = { article: CatalogArticle; title: string; detail: string | null }

const lower = (text: string) => text.toLocaleLowerCase('sv-SE')

/**
 * The shelf's cards in catalogue order: one category (null for all) and those whose name or variety contains the
 * search text, ignoring case. A blank query matches everything.
 */
export function shelfCards(categories: PanelCategory[], category: string | null, query: string): ShelfCard[] {
  const needle = lower(query.trim())
  return categories
    .filter((c) => category === null || c.category === category)
    .flatMap(({ varieties }) =>
      varieties.flatMap(({ name, articles }) =>
        articles.map((article): ShelfCard => {
          const grouped = articles.length > 1
          return {
            article,
            title: grouped ? name : article.name,
            detail: grouped ? article.name.replace(name, '').trim() || null : null,
          }
        }),
      ),
    )
    .filter(({ article }) => !needle || lower(article.name).includes(needle) || (article.variety !== null && lower(article.variety).includes(needle)))
}

const kronor = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 0 })
const UNIT_SUFFIX: Record<CatalogArticle['unit'], string> = { piece: '', meter: '/m', 'square-meter': '/m²' }

/** "3 450 kr", "420 kr/m" or "640 kr/m²": an article's price including VAT, as the server sends it. */
export function formatUnitPrice(price: number, unit: CatalogArticle['unit']): string {
  return `${kronor.format(price)} kr${UNIT_SUFFIX[unit]}`
}
