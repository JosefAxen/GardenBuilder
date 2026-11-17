import { describe, expect, it } from 'vitest'
import { buildModels } from './models'
import { GLB_URLS, POINT_FORMS, fallbackFor, itemsFor, nominalHeightOf, partsFor } from './modelLibrary'
import type { CatalogArticle } from '../api/catalog'
import { thuja } from './testArticles'

const maple = { ...thuja, typeModel: 'tree-small', family: 'deciduous-tree', color: '#b5452f', accentColor: null } as const
const hortensia = { ...thuja, typeModel: 'shrub-flowering', family: 'shrub', color: '#5f8f45', accentColor: '#f2f0e1' } as const

describe('partsFor', () => {
  const parts = partsFor(['Leaves_NormalTree', 'Flowers'])
  const part = (key: string) => parts.find((p) => p.key === key)!

  it('splits leaves into pack and tinted parts', () => {
    expect(part('Leaves_NormalTree').role).toBe('leaves')
    expect(part('Leaves_NormalTree').when(maple)).toBe(false)
    expect(part('Leaves_NormalTree').when(hortensia)).toBe(true)
    expect(part('Leaves_NormalTree-tinted').role).toBe('leaves-tinted')
    expect(part('Leaves_NormalTree-tinted').when(maple)).toBe(true)
    expect(part('Leaves_NormalTree-tinted').when(hortensia)).toBe(false)
  })

  it('draws flowers only when the article has an accent colour', () => {
    expect(part('Flowers').role).toBe('flowers')
    expect(part('Flowers').when(hortensia)).toBe(true)
    expect(part('Flowers').when({ ...hortensia, accentColor: null })).toBe(false)
  })

  it('keeps bark in the pack colours for every article', () => {
    const bark = partsFor(['Bark_NormalTree'])[0]
    expect(bark.role).toBe('fixed')
    expect(bark.when(maple)).toBe(true)
  })

  it('gives each part the colour it is tinted with, white for pack colours', () => {
    expect(part('Leaves_NormalTree').tint(hortensia)).toBe('#ffffff')
    expect(part('Leaves_NormalTree-tinted').tint(maple)).toBe('#b5452f')
    expect(part('Flowers').tint(hortensia)).toBe('#f2f0e1')
  })
})

describe('elements that will be removed', () => {
  const parts = partsFor(['Leaves_NormalTree', 'Flowers'])
  const part = (key: string) => parts.find((p) => p.key === key)!
  const at = (id: string, article: CatalogArticle, removed: boolean) => ({ id, position: { x: 0, y: 0 }, rotation: 0, article, removed })

  it('are drawn only by the grey removed parts, never in the pack colours', () => {
    const items = [at('kept', hortensia, false), at('gone', hortensia, true)]

    expect(itemsFor(part('Leaves_NormalTree'), items).map((i) => i.id)).toEqual(['kept'])
    expect(itemsFor(part('Leaves_NormalTree-removed'), items).map((i) => i.id)).toEqual(['gone'])
    expect(part('Leaves_NormalTree-removed').role).toBe('removed')
    expect(part('Leaves_NormalTree-removed').tint(hortensia)).toBe('#b0b0b0')
  })

  it('keep their flowers only when the article has an accent colour', () => {
    expect(itemsFor(part('Flowers-removed'), [at('gone', hortensia, true)])).toHaveLength(1)
    expect(itemsFor(part('Flowers-removed'), [at('gone', { ...hortensia, accentColor: null }, true)])).toHaveLength(0)
  })
})

describe('the library', () => {
  const models = buildModels('low')

  it('every point form has a fallback', () => {
    for (const key of POINT_FORMS) expect(fallbackFor(key, models).length, key).toBeGreaterThan(0)
  })

  it('every pack form has a GLB and a nominal height', () => {
    for (const key of POINT_FORMS.filter((k) => k !== 'pot' && k !== 'furniture')) {
      expect(GLB_URLS[key], key).toBeDefined()
      expect(nominalHeightOf(key), key).toBeGreaterThan(0)
    }
    expect(GLB_URLS.pot).toBeUndefined()
  })
})
