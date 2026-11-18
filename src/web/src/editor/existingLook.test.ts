import { describe, expect, it } from 'vitest'
import type { SceneElement } from '../api/scene'
import { lookOf } from './existingLook'
import { thuja } from './testArticles'

const base = { status: 'existing-keep' as const, measurementSource: 'estimated' as const }

describe('lookOf', () => {
  it('an existing tree without article is drawn as its form at its own size', () => {
    const tree: SceneElement = { ...base, kind: 'point', id: 't', articleId: null, form: 'tree-large', width: 6, height: 9, position: { x: 0, y: 0 }, rotation: 0 }
    const look = lookOf(tree, [])
    expect(look).toMatchObject({ typeModel: 'tree-large', family: 'deciduous-tree', geometry: 'point', height: 9, matureWidth: 6 })
  })

  it('an existing spruce is a conifer', () => {
    const spruce: SceneElement = { ...base, kind: 'point', id: 's', articleId: null, form: 'pine', height: 12, position: { x: 0, y: 0 }, rotation: 0 }
    expect(lookOf(spruce, [])?.family).toBe('conifer')
  })

  it('an existing hedge is drawn as a hedge of its height', () => {
    const hedge: SceneElement = { ...base, kind: 'line', id: 'h', articleId: null, form: 'hedge', height: 1.5, points: [{ x: 0, y: 0 }, { x: 5, y: 0 }] }
    expect(lookOf(hedge, [])).toMatchObject({ typeModel: 'hedge', geometry: 'line', height: 1.5 })
  })

  it('the article wins when there is one', () => {
    const planted: SceneElement = { ...base, kind: 'point', id: 'p', articleId: thuja.id, form: 'tree-large', position: { x: 0, y: 0 }, rotation: 0 }
    expect(lookOf(planted, [thuja])).toBe(thuja)
  })

  it('nothing to draw without article or form', () => {
    const unknown: SceneElement = { ...base, kind: 'point', id: 'u', articleId: null, position: { x: 0, y: 0 }, rotation: 0 }
    expect(lookOf(unknown, [])).toBeUndefined()
  })
})
