import type { CatalogArticle } from '../api/catalog'

/** A catalog article for tests: Tuja Smaragd 40–60 cm. */
export const thuja: CatalogArticle = {
  id: 'thuja',
  articleNumber: 'BA-SM-040',
  name: 'Tuja Smaragd 40–60 cm',
  variety: 'Tuja Smaragd',
  category: 'Barrträd',
  unit: 'piece',
  price: 189,
  currency: 'SEK',
  stockStatus: 'in-stock',
  typeModel: 'conifer',
  geometry: 'point',
  material: null,
  scale: 1,
  color: '#3f7a3a',
  accentColor: null,
  hardinessZone: 5,
  height: 0.5,
  width: null,
  matureHeight: 5,
  matureWidth: 1.2,
  clipHeight: null,
  growthCmPerYear: 20,
  light: ['sun', 'part-shade'],
}
