/**
 * PROTOTYPE: a hard-coded catalog so the editor can be tried before the Catalog module serves articles.
 * Each article maps to a type model (here generated in code) with scale and colour, as in docs/design.md
 * ("Modellbibliotek"). Ids are fixed UUIDs so saved scenes keep pointing at the same article.
 * No prices here: prices are calculated only on the server.
 */
export type ModelKind = 'deciduous' | 'conifer' | 'bush' | 'perennial' | 'pot' | 'bench'

type ArticleBase = { id: string; name: string; category: string; color: string }

export type PointArticle = ArticleBase & {
  geometry: 'point'
  model: ModelKind
  scale: number
  /** Second colour: blooms, or the plant in a pot. */
  accent?: string
  /** Mature width in meters. Only plants have it; it drives the ring on the ground and the spacing warning. */
  matureWidth?: number
}

export type LineArticle = ArticleBase & {
  geometry: 'line'
  style: 'hedge' | 'fence'
  height: number
  width: number
}

export type SurfaceArticle = ArticleBase & { geometry: 'surface' }

export type Article = PointArticle | LineArticle | SurfaceArticle

const id = (n: number) => `019a7d3c-a000-7000-8000-${n.toString().padStart(12, '0')}`

export const articles: Article[] = [
  { id: id(1), name: 'Äppelträd Rubinola', category: 'Träd', geometry: 'point', model: 'deciduous', scale: 1, color: '#5f9e3f', matureWidth: 4 },
  { id: id(2), name: 'Hängbjörk', category: 'Träd', geometry: 'point', model: 'deciduous', scale: 1.4, color: '#8dbb4c', matureWidth: 6 },
  { id: id(3), name: 'Japansk lönn', category: 'Träd', geometry: 'point', model: 'deciduous', scale: 0.7, color: '#b5452f', matureWidth: 3 },
  { id: id(4), name: 'Serbisk gran', category: 'Träd', geometry: 'point', model: 'conifer', scale: 1.3, color: '#2f5d3a', matureWidth: 3 },
  { id: id(5), name: 'Tuja Smaragd', category: 'Träd', geometry: 'point', model: 'conifer', scale: 0.8, color: '#3f7a3a', matureWidth: 1.2 },
  { id: id(6), name: 'Syren', category: 'Buskar', geometry: 'point', model: 'bush', scale: 1.2, color: '#6a9a4a', accent: '#a77bca', matureWidth: 2.5 },
  { id: id(7), name: 'Hortensia', category: 'Buskar', geometry: 'point', model: 'bush', scale: 0.8, color: '#5f8f45', accent: '#9fb8e6', matureWidth: 1.5 },
  { id: id(8), name: 'Buxbom, klot', category: 'Buskar', geometry: 'point', model: 'bush', scale: 0.5, color: '#3d6b35', matureWidth: 0.6 },
  { id: id(9), name: 'Lavendel', category: 'Perenner', geometry: 'point', model: 'perennial', scale: 1, color: '#7c9a6a', accent: '#8e7cc3', matureWidth: 0.5 },
  { id: id(10), name: 'Funkia', category: 'Perenner', geometry: 'point', model: 'perennial', scale: 1.2, color: '#4d8a4a', accent: '#e8e3c8', matureWidth: 0.7 },
  { id: id(11), name: 'Rudbeckia', category: 'Perenner', geometry: 'point', model: 'perennial', scale: 1, color: '#55803d', accent: '#f2b632', matureWidth: 0.5 },
  { id: id(12), name: 'Terrakottakruka', category: 'Krukor och möbler', geometry: 'point', model: 'pot', scale: 1, color: '#c06a3f', accent: '#5b8f45' },
  { id: id(13), name: 'Trädgårdsbänk', category: 'Krukor och möbler', geometry: 'point', model: 'bench', scale: 1, color: '#8a6a4a' },
  { id: id(14), name: 'Ligusterhäck', category: 'Häckar och staket', geometry: 'line', style: 'hedge', height: 1.2, width: 0.6, color: '#3f7a3a' },
  { id: id(15), name: 'Bokhäck', category: 'Häckar och staket', geometry: 'line', style: 'hedge', height: 1.6, width: 0.5, color: '#6f8f3a' },
  { id: id(16), name: 'Spjälstaket', category: 'Häckar och staket', geometry: 'line', style: 'fence', height: 1.0, width: 0.08, color: '#d9cdb6' },
  { id: id(17), name: 'Gräsmatta, rullgräs', category: 'Ytor', geometry: 'surface', color: '#7fb069' },
  { id: id(18), name: 'Trädäck, tryckimpregnerat', category: 'Ytor', geometry: 'surface', color: '#a77b52' },
  { id: id(19), name: 'Rabatt, planteringsjord', category: 'Ytor', geometry: 'surface', color: '#6b4f3a' },
]

const byId = new Map(articles.map((a) => [a.id, a]))

export function findArticle(articleId: string | null | undefined): Article | undefined {
  return articleId ? byId.get(articleId) : undefined
}

export const pointArticles = articles.filter((a): a is PointArticle => a.geometry === 'point')
