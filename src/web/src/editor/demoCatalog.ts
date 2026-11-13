/**
 * PROTOTYPE: a hard-coded catalog so the editor can be tried before the Catalog module serves articles.
 * Each article maps to a type model (here generated in code) with scale and colour, as in docs/design.md
 * ("Modellbibliotek"). No prices here: prices are calculated only on the server.
 */
export type ModelKind = 'deciduous' | 'conifer' | 'bush' | 'perennial' | 'pot' | 'bench'

export type PointArticle = {
  id: string
  name: string
  category: string
  geometry: 'point'
  model: ModelKind
  scale: number
  color: string
  /** Second colour: blooms, or the plant in a pot. */
  accent?: string
  /** Mature width in meters. Only plants have it; it drives the ring on the ground and the spacing warning. */
  matureWidth?: number
}

export type LineArticle = {
  id: string
  name: string
  category: string
  geometry: 'line'
  style: 'hedge' | 'fence'
  height: number
  width: number
  color: string
}

export type Article = PointArticle | LineArticle

export const articles: Article[] = [
  { id: 'tree-apple', name: 'Äppelträd Rubinola', category: 'Träd', geometry: 'point', model: 'deciduous', scale: 1, color: '#5f9e3f', matureWidth: 4 },
  { id: 'tree-birch', name: 'Hängbjörk', category: 'Träd', geometry: 'point', model: 'deciduous', scale: 1.4, color: '#8dbb4c', matureWidth: 6 },
  { id: 'tree-maple', name: 'Japansk lönn', category: 'Träd', geometry: 'point', model: 'deciduous', scale: 0.7, color: '#b5452f', matureWidth: 3 },
  { id: 'conifer-spruce', name: 'Serbisk gran', category: 'Träd', geometry: 'point', model: 'conifer', scale: 1.3, color: '#2f5d3a', matureWidth: 3 },
  { id: 'conifer-thuja', name: 'Tuja Smaragd', category: 'Träd', geometry: 'point', model: 'conifer', scale: 0.8, color: '#3f7a3a', matureWidth: 1.2 },
  { id: 'bush-lilac', name: 'Syren', category: 'Buskar', geometry: 'point', model: 'bush', scale: 1.2, color: '#6a9a4a', accent: '#a77bca', matureWidth: 2.5 },
  { id: 'bush-hydrangea', name: 'Hortensia', category: 'Buskar', geometry: 'point', model: 'bush', scale: 0.8, color: '#5f8f45', accent: '#9fb8e6', matureWidth: 1.5 },
  { id: 'bush-boxwood', name: 'Buxbom, klot', category: 'Buskar', geometry: 'point', model: 'bush', scale: 0.5, color: '#3d6b35', matureWidth: 0.6 },
  { id: 'perennial-lavender', name: 'Lavendel', category: 'Perenner', geometry: 'point', model: 'perennial', scale: 1, color: '#7c9a6a', accent: '#8e7cc3', matureWidth: 0.5 },
  { id: 'perennial-hosta', name: 'Funkia', category: 'Perenner', geometry: 'point', model: 'perennial', scale: 1.2, color: '#4d8a4a', accent: '#e8e3c8', matureWidth: 0.7 },
  { id: 'perennial-rudbeckia', name: 'Rudbeckia', category: 'Perenner', geometry: 'point', model: 'perennial', scale: 1, color: '#55803d', accent: '#f2b632', matureWidth: 0.5 },
  { id: 'pot-terracotta', name: 'Terrakottakruka', category: 'Krukor och möbler', geometry: 'point', model: 'pot', scale: 1, color: '#c06a3f', accent: '#5b8f45' },
  { id: 'bench-wood', name: 'Trädgårdsbänk', category: 'Krukor och möbler', geometry: 'point', model: 'bench', scale: 1, color: '#8a6a4a' },
  { id: 'hedge-privet', name: 'Ligusterhäck', category: 'Häckar och staket', geometry: 'line', style: 'hedge', height: 1.2, width: 0.6, color: '#3f7a3a' },
  { id: 'hedge-beech', name: 'Bokhäck', category: 'Häckar och staket', geometry: 'line', style: 'hedge', height: 1.6, width: 0.5, color: '#6f8f3a' },
  { id: 'fence-wood', name: 'Spjälstaket', category: 'Häckar och staket', geometry: 'line', style: 'fence', height: 1.0, width: 0.08, color: '#d9cdb6' },
]

const byId = new Map(articles.map((a) => [a.id, a]))

export function findArticle(id: string | undefined): Article | undefined {
  return id === undefined ? undefined : byId.get(id)
}

export const pointArticles = articles.filter((a): a is PointArticle => a.geometry === 'point')
