import { describe, expect, it } from 'vitest'
import type { CatalogArticle } from '../api/catalog'
import { groupForPanel, replaceOptions } from './panelGroups'
import { thuja } from './testArticles'

const smaragd = (id: string, height: number): CatalogArticle => ({ ...thuja, id, name: `Tuja Smaragd ${id}`, height })
const big = smaragd('120', 1.3)
const small = smaragd('40', 0.5)
const medium = smaragd('80', 0.9)
const brabant: CatalogArticle = { ...thuja, id: 'brabant', name: 'Tuja Brabant 60–80 cm', variety: 'Tuja Brabant', height: 0.7 }
const pot: CatalogArticle = { ...thuja, id: 'pot', name: 'Terrakottakruka 40 cm', variety: null, category: 'Krukor och möbler', typeModel: 'pot', height: null }
const hedge: CatalogArticle = { ...thuja, id: 'hedge', name: 'Thujahäck', geometry: 'line', typeModel: 'hedge' }

describe('groupForPanel', () => {
  it('groups sizes of one variety under the variety name', () => {
    const [conifers] = groupForPanel([big, small, medium])

    expect(conifers.category).toBe('Barrträd')
    const [variety] = conifers.varieties
    expect(variety.name).toBe('Tuja Smaragd')
    expect(variety.articles.map((a) => a.id)).toEqual(['40', '80', '120'])
  })

  it('gives an article without a variety a group of its own, named after the article', () => {
    const [category] = groupForPanel([pot])

    expect(category.varieties).toEqual([{ name: 'Terrakottakruka 40 cm', articles: [pot] }])
  })
})

describe('replaceOptions', () => {
  it('lists other sizes of the same variety first when replacing', () => {
    const options = replaceOptions(small, [brabant, big, small, medium])

    expect(options.map((a) => a.id)).toEqual(['80', '120', 'brabant'])
  })

  it('never offers out-of-stock articles as replacements', () => {
    const options = replaceOptions(small, [{ ...big, stockStatus: 'out-of-stock' }, medium])

    expect(options.map((a) => a.id)).toEqual(['80'])
  })

  it('only offers articles of the same geometry', () => {
    expect(replaceOptions(small, [hedge, medium]).map((a) => a.id)).toEqual(['80'])
  })
})
