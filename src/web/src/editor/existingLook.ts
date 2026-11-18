import type { CatalogArticle } from '../api/catalog'
import type { SceneElement } from '../api/scene'
import { findArticle } from './catalogStore'

const FAMILY: Record<string, string> = {
  'tree-large': 'deciduous-tree',
  'tree-fruit': 'deciduous-tree',
  'tree-small': 'deciduous-tree',
  pine: 'conifer',
  spruce: 'conifer',
  'conifer-column': 'conifer',
  'shrub-round': 'shrub',
  'shrub-flowering': 'shrub',
  'perennial-tall': 'perennial',
  'perennial-low': 'perennial',
  'foliage-plant': 'perennial',
  'ornamental-grass': 'perennial',
  hedge: 'hedge',
  fence: 'fence',
}

/** Colours for existing things, which have no article to take a colour from. */
const COLOR: Record<string, string> = {
  'deciduous-tree': '#5b8a3c',
  conifer: '#2f5d3a',
  shrub: '#5a8f4a',
  perennial: '#6f9a4f',
  hedge: '#3d6b35',
  fence: '#8a6a4a',
}

/**
 * What an element looks like: its catalog article, or for an existing thing without one (a tree from a photo), a
 * stand-in built from the element's own form and size, so the editor draws both the same way.
 */
export function lookOf(element: SceneElement, articles: CatalogArticle[]): CatalogArticle | undefined {
  if (element.kind !== 'point' && element.kind !== 'line') return undefined
  const article = findArticle(element.articleId, articles)
  if (article || !element.form) return article
  const family = FAMILY[element.form] ?? 'furniture'
  const width = element.kind === 'point' ? (element.width ?? null) : null
  return {
    id: `existing:${element.id}`,
    articleNumber: '',
    name: 'Befintlig',
    variety: null,
    category: 'Befintligt',
    unit: element.kind === 'line' ? 'meter' : 'piece',
    price: 0,
    currency: 'SEK',
    stockStatus: 'in-stock',
    typeModel: element.form as CatalogArticle['typeModel'],
    family,
    geometry: element.kind,
    material: null,
    scale: 1,
    color: COLOR[family] ?? '#8a8a8a',
    accentColor: null,
    hardinessZone: null,
    height: element.height ?? null,
    width: element.kind === 'line' ? (family === 'fence' ? 0.1 : 0.8) : null,
    // An existing plant is already grown: its width now is its mature width.
    matureHeight: element.height ?? null,
    matureWidth: width,
    clipHeight: null,
    growthCmPerYear: null,
    light: [],
  }
}
