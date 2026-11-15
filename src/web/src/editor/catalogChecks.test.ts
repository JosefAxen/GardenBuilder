import { describe, expect, it } from 'vitest'
import type { SceneElement } from '../api/scene'
import { countMissingArticles } from './catalogChecks'
import { thuja } from './testArticles'

const tree = (articleId: string | null): SceneElement => ({
  kind: 'point',
  id: `tree-${articleId}`,
  status: 'new',
  measurementSource: 'estimated',
  articleId,
  position: { x: 1, y: 1 },
  rotation: 0,
})

const plot: SceneElement = {
  kind: 'plot',
  id: 'plot',
  status: 'existing-keep',
  measurementSource: 'estimated',
  sloped: false,
  points: [{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 10 }],
}

describe('countMissingArticles', () => {
  it('counts elements whose article is not in the catalog', () => {
    expect(countMissingArticles([tree('thuja'), tree('discontinued'), plot], [thuja])).toBe(1)
  })

  it('does not count elements without an article, like the plot or an existing lawn', () => {
    expect(countMissingArticles([plot, tree(null)], [thuja])).toBe(0)
  })
})
