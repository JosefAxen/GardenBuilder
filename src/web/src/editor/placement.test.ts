import { describe, expect, it } from 'vitest'
import type { SceneDocument } from '../api/scene'
import { isInsidePolygon, placementProblem } from './placement'

const square = [
  { x: 0, y: 0 },
  { x: 10, y: 0 },
  { x: 10, y: 10 },
  { x: 0, y: 10 },
]

const scene: SceneDocument = {
  formatVersion: 1,
  elements: [{ kind: 'plot', id: 'plot', status: 'existing-keep', measurementSource: 'estimated', sloped: false, points: square }],
}

describe('isInsidePolygon', () => {
  it('finds a point inside', () => expect(isInsidePolygon({ x: 5, y: 5 }, square)).toBe(true))
  it('finds a point outside', () => expect(isInsidePolygon({ x: 11, y: 5 }, square)).toBe(false))
})

describe('placementProblem', () => {
  it('allows a point inside the plot', () => {
    expect(placementProblem(scene, [{ x: 2, y: 3 }])).toBeNull()
  })

  it('refuses a point outside the plot, in Swedish', () => {
    expect(placementProblem(scene, [{ x: 20, y: 3 }])).toBe('Det går inte att placera utanför tomten.')
  })

  it('refuses a line with one end outside the plot', () => {
    expect(placementProblem(scene, [{ x: 2, y: 2 }, { x: 2, y: 20 }])).toBe('Det går inte att placera utanför tomten.')
  })

  it('refuses a line shorter than ten centimeters', () => {
    expect(placementProblem(scene, [{ x: 2, y: 2 }, { x: 2.05, y: 2 }])).toBe('Linjen är för kort.')
  })

  it('allows anything when the scene has no plot yet', () => {
    expect(placementProblem({ formatVersion: 1, elements: [] }, [{ x: 500, y: 500 }])).toBeNull()
  })
})
