import type { CatalogArticle } from '../../api/catalog'
import type { PanelCategory } from '../panelGroups'

/** One card on the catalogue shelf: an article, with the variety as its title when the variety has several sizes. */
export type ShelfCard = { article: CatalogArticle; title: string; detail: string | null }

const lower = (text: string) => text.toLocaleLowerCase('sv-SE')

/** A size at the end of a name: "30 cm", "100–125 cm", "1,5 m". */
const SIZE = /\s(\d+(?:,\d+)?(?:–\d+(?:,\d+)?)?\s?(?:cm|m))$/

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
          if (articles.length > 1) return { article, title: name, detail: article.name.replace(name, '').trim() || null }
          // A single article's own size goes on the detail line too, so every card reads name, size, price.
          const size = SIZE.exec(article.name)
          return size ? { article, title: article.name.slice(0, size.index).trim(), detail: size[1] } : { article, title: article.name, detail: null }
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
