import type { CatalogArticle, Light } from '../../api/catalog'

export type LabelFact = { label: string; value: string }

const ROMAN = ['', 'I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII']
const LIGHT: Record<Light, string> = { sun: 'Sol', 'part-shade': 'Halvskugga', shade: 'Skugga' }
const meters = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 1 })

/**
 * The facts on a plant label, in the order a customer reads them: hardiness zone, planting height, mature height and
 * width, light. Only what the article has; an article with none gives an empty list.
 */
export function labelFacts(article: CatalogArticle): LabelFact[] {
  const facts: LabelFact[] = []
  if (article.hardinessZone !== null) facts.push({ label: 'Växtzon', value: `I–${ROMAN[article.hardinessZone] ?? article.hardinessZone}` })
  if (article.height !== null) facts.push({ label: 'Höjd vid plantering', value: `${Math.round(article.height * 100)} cm` })
  if (article.matureHeight !== null) facts.push({ label: 'Fullvuxen höjd', value: `${meters.format(article.matureHeight)} m` })
  if (article.matureWidth !== null) facts.push({ label: 'Fullvuxen bredd', value: `${meters.format(article.matureWidth)} m` })
  if (article.light.length > 0) facts.push({ label: 'Läge', value: article.light.map((l) => LIGHT[l]).join('–') })
  return facts
}
