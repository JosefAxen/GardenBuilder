import type { CatalogArticle } from '../api/catalog'

/** One plant (or product) in the panel, with a row per size. */
export type PanelVariety = { name: string; articles: CatalogArticle[] }

export type PanelCategory = { category: string; varieties: PanelVariety[] }

const byHeight = (a: CatalogArticle, b: CatalogArticle) => (a.height ?? 0) - (b.height ?? 0)

/**
 * The catalog as the panel shows it: categories in catalog order, and within each, the sizes of one variety
 * together, smallest first. An article without a variety is a group of its own, named after the article.
 */
export function groupForPanel(articles: CatalogArticle[]): PanelCategory[] {
  const categories = new Map<string, Map<string, CatalogArticle[]>>()
  for (const article of articles) {
    const varieties = categories.get(article.category) ?? new Map<string, CatalogArticle[]>()
    categories.set(article.category, varieties)
    const name = article.variety ?? article.name
    varieties.set(name, [...(varieties.get(name) ?? []), article])
  }

  return [...categories].map(([category, varieties]) => ({
    category,
    varieties: [...varieties].map(([name, sizes]) => ({ name, articles: sizes.toSorted(byHeight) })),
  }))
}

/**
 * What the selected article can be swapped for: same geometry (a tree for a tree, a hedge for a hedge), in stock,
 * other sizes of the same variety first so changing size is one click.
 */
export function replaceOptions(selected: CatalogArticle, all: CatalogArticle[]): CatalogArticle[] {
  const candidates = all.filter((a) => a.id !== selected.id && a.geometry === selected.geometry && a.stockStatus === 'in-stock')
  const sameVariety = selected.variety ? candidates.filter((a) => a.variety === selected.variety).toSorted(byHeight) : []
  return [...sameVariety, ...candidates.filter((a) => !sameVariety.includes(a))]
}
