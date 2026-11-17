import { describe, expect, it } from 'vitest'
import { buildModels } from './models'
import { GLB_URLS, POINT_FORMS, fallbackFor, nominalHeightOf, partsFor } from './modelLibrary'
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
