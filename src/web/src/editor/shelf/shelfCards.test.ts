import { describe, expect, it } from 'vitest'
import type { CatalogArticle } from '../../api/catalog'
import { groupForPanel } from '../panelGroups'
import { thuja } from '../testArticles'
import { formatUnitPrice, shelfCards } from './shelfCards'

const tree = (id: string, name: string, extra: Partial<CatalogArticle> = {}): CatalogArticle => ({
  ...thuja,
  id,
  name,
  variety: null,
  category: 'Träd',
  typeModel: 'tree-large',
  ...extra,
})

const maple = tree('maple', "Skogslönn 'Crimson King'")
const rowan = tree('rowan', 'Rönn')
const small = { ...thuja, id: 'sm-40', name: 'Tuja Smaragd 40–60 cm', height: 0.5 }
const large = { ...thuja, id: 'sm-80', name: 'Tuja Smaragd 80–100 cm', height: 0.9 }
const categories = groupForPanel([small, maple, large, rowan])

describe('shelfCards', () => {
  it('finds a plant whatever the case of the query', () => {
    expect(shelfCards(categories, null, 'LÖNN').map((c) => c.article.id)).toEqual(['maple'])
  })

  it('a query matching only the variety returns all its sizes, titled by the variety', () => {
    const cards = shelfCards(categories, null, 'smaragd')
    expect(cards.map((c) => c.article.id)).toEqual(['sm-40', 'sm-80'])
    expect(cards.map((c) => [c.title, c.detail])).toEqual([
      ['Tuja Smaragd', '40–60 cm'],
      ['Tuja Smaragd', '80–100 cm'],
    ])
  })

  it('a category with no query shows only that category, in catalogue order', () => {
    expect(shelfCards(categories, 'Träd', '').map((c) => c.article.id)).toEqual(['maple', 'rowan'])
  })

  it('a blank query shows everything', () => {
    expect(shelfCards(categories, null, '   ')).toHaveLength(4)
  })

  it('a single article keeps its own name and has no detail line', () => {
    expect(shelfCards(categories, 'Träd', 'rönn')).toEqual([{ article: rowan, title: 'Rönn', detail: null }])
  })

  it('category and query together', () => {
    expect(shelfCards(categories, 'Barrträd', 'lönn')).toEqual([])
  })
})

describe('sizes on single articles', () => {
  it('a trailing size moves to the detail line', () => {
    const spruce = tree('spruce', 'Serbisk gran 100–125 cm')
    expect(shelfCards(groupForPanel([spruce]), null, '')).toEqual([{ article: spruce, title: 'Serbisk gran', detail: '100–125 cm' }])
  })

  it('a single size like "30 cm" also moves', () => {
    const box = tree('box', 'Buxbom klot 30 cm')
    expect(shelfCards(groupForPanel([box]), null, '')[0]).toMatchObject({ title: 'Buxbom klot', detail: '30 cm' })
  })

  it('a name without a size stays whole', () => {
    expect(shelfCards(groupForPanel([rowan]), null, '')[0]).toMatchObject({ title: 'Rönn', detail: null })
  })
})

describe('formatUnitPrice', () => {
  it.each([
    [3450, 'piece', `${new Intl.NumberFormat('sv-SE').format(3450)} kr`],
    [420, 'meter', '420 kr/m'],
    [640, 'square-meter', '640 kr/m²'],
  ] as const)('%d per %s', (price, unit, text) => {
    expect(formatUnitPrice(price, unit)).toBe(text)
  })
})
