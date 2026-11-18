import { beforeEach, describe, expect, it } from 'vitest'
import type { Point2, SceneDocument, SceneElement } from '../api/scene'
import { demoScene } from './demoScene'
import { applyCommand, useSceneStore } from './sceneStore'
import { drawClick, dropShapeDrag, finishShaping, shapeProblem, startDrawing, startShaping, tryShape } from './shapeCommands'

const rect = (x0: number, y0: number, x1: number, y1: number): Point2[] => [
  { x: x0, y: y0 },
  { x: x1, y: y0 },
  { x: x1, y: y1 },
  { x: x0, y: y1 },
]

const plot: SceneElement = { kind: 'plot', id: 'plot', status: 'existing-keep', measurementSource: 'estimated', sloped: false, points: rect(0, 0, 20, 20) }
const house: SceneElement = { kind: 'building', id: 'house', status: 'existing-keep', measurementSource: 'estimated', height: 5, roof: 'gable', points: rect(2, 12, 10, 18) }
const tree = (id: string, x: number, y: number): SceneElement => ({
  kind: 'point',
  id,
  status: 'existing-keep',
  measurementSource: 'estimated',
  articleId: null,
  form: 'tree-large',
  position: { x, y },
  rotation: 0,
})

const scene = (...more: SceneElement[]): SceneDocument => ({ formatVersion: 2, elements: [plot, house, ...more] })
const pointsOf = (document: SceneDocument, id: string) => {
  const element = document.elements.find((e) => e.id === id)
  return element && 'points' in element ? element.points : []
}

describe('shape commands', () => {
  beforeEach(() => useSceneStore.getState().load(scene()))

  it('move-point moves one corner and undo restores it', () => {
    const { execute, undo } = useSceneStore.getState()
    execute({ type: 'move-point', id: 'plot', index: 2, point: { x: 22, y: 21 } })
    expect(pointsOf(useSceneStore.getState().document, 'plot')[2]).toEqual({ x: 22, y: 21 })
    undo()
    expect(pointsOf(useSceneStore.getState().document, 'plot')[2]).toEqual({ x: 20, y: 20 })
  })

  it('insert-point on a 4-corner plot gives 5 points', () => {
    const after = applyCommand(scene(), { type: 'insert-point', id: 'plot', index: 1, point: { x: 10, y: -2 } })
    expect(pointsOf(after, 'plot')).toEqual([{ x: 0, y: 0 }, { x: 10, y: -2 }, { x: 20, y: 0 }, { x: 20, y: 20 }, { x: 0, y: 20 }])
  })

  it('remove-point refuses to go below 3 points', () => {
    const triangle: SceneDocument = { formatVersion: 2, elements: [{ ...plot, points: rect(0, 0, 20, 20).slice(0, 3) } as SceneElement] }
    expect(applyCommand(triangle, { type: 'remove-point', id: 'plot', index: 0 })).toBe(triangle)
  })

  it('remove-point removes one corner', () => {
    const after = applyCommand(scene(), { type: 'remove-point', id: 'plot', index: 3 })
    expect(pointsOf(after, 'plot')).toHaveLength(3)
  })

  it('redraw replaces all points', () => {
    const points = [{ x: 0, y: 0 }, { x: 30, y: 0 }, { x: 30, y: 25 }, { x: 15, y: 30 }, { x: 0, y: 25 }]
    expect(pointsOf(applyCommand(scene(), { type: 'redraw', id: 'plot', points }), 'plot')).toEqual(points)
  })

  it('shape commands do nothing to a tree', () => {
    const document = scene(tree('t', 5, 5))
    expect(applyCommand(document, { type: 'remove-point', id: 't', index: 0 })).toBe(document)
  })
})

describe('shapeProblem', () => {
  it('a bowtie plot crosses itself', () => {
    const bowtie = [{ x: 0, y: 0 }, { x: 20, y: 20 }, { x: 20, y: 0 }, { x: 0, y: 20 }]
    const document = { ...scene(), elements: [{ ...plot, points: bowtie } as SceneElement] }
    expect(shapeProblem(document)).toBe('Tomtgränsen får inte korsa sig själv.')
  })

  it('a house corner one meter outside', () => {
    const document = applyCommand(scene(), { type: 'move-point', id: 'house', index: 2, point: { x: 10, y: 21 } })
    expect(shapeProblem(document)).toBe('Huset måste ligga inom tomten.')
  })

  it('two existing things end up outside', () => {
    const document = applyCommand(scene(tree('a', 1, 1), tree('b', 1, 3), tree('c', 15, 5)), {
      type: 'redraw',
      id: 'plot',
      points: rect(2, 0, 20, 20),
    })
    expect(shapeProblem(document)).toBe('2 saker hamnar utanför tomten. Flytta dem först.')
  })

  it('a hedge on the new boundary is fine', () => {
    expect(shapeProblem(scene(tree('a', 20.3, 5)))).toBeNull()
  })

  it('the demo scene has no problem', () => {
    expect(shapeProblem(demoScene)).toBeNull()
  })
})

describe('tryShape', () => {
  beforeEach(() => useSceneStore.getState().load(scene(tree('a', 1, 1))))

  it('leaves the document unchanged and says why', () => {
    const before = useSceneStore.getState().document
    tryShape({ type: 'redraw', id: 'plot', points: rect(2, 0, 20, 20) })
    expect(useSceneStore.getState().document).toBe(before)
    expect(useSceneStore.getState().notice).toBe('1 sak hamnar utanför tomten. Flytta den först.')
  })

  it('executes a shape that works', () => {
    tryShape({ type: 'move-point', id: 'plot', index: 1, point: { x: 25, y: 0 } })
    expect(pointsOf(useSceneStore.getState().document, 'plot')[1]).toEqual({ x: 25, y: 0 })
  })
})

describe('the shape tool', () => {
  beforeEach(() => useSceneStore.getState().load(scene()))

  it('starts on the plot in the top view and finishes back in perspective', () => {
    startShaping('plot')
    expect(useSceneStore.getState().tool).toEqual({ mode: 'shape', id: 'plot' })
    expect(useSceneStore.getState().view).toBe('top')
    finishShaping()
    expect(useSceneStore.getState().tool).toEqual({ mode: 'select' })
    expect(useSceneStore.getState().view).toBe('perspective')
  })

  it('a dragged mid-edge handle inserts a corner when dropped, as one undo step', () => {
    startShaping('plot')
    useSceneStore.getState().setShapeDrag({ id: 'plot', index: 1, insert: true, point: { x: 10, y: -1 } })
    dropShapeDrag()
    expect(pointsOf(useSceneStore.getState().document, 'plot')).toHaveLength(5)
    expect(useSceneStore.getState().shapeDrag).toBeNull()
    useSceneStore.getState().undo()
    expect(pointsOf(useSceneStore.getState().document, 'plot')).toHaveLength(4)
  })

  it('drawing anew closes on a click near the first corner', () => {
    startShaping('plot')
    startDrawing()
    for (const p of [{ x: 0, y: 0 }, { x: 25, y: 0 }, { x: 25, y: 22 }, { x: 0, y: 22 }, { x: 0.2, y: 0.1 }]) drawClick(p)
    expect(pointsOf(useSceneStore.getState().document, 'plot')).toEqual([{ x: 0, y: 0 }, { x: 25, y: 0 }, { x: 25, y: 22 }, { x: 0, y: 22 }])
    expect(useSceneStore.getState().tool).toEqual({ mode: 'shape', id: 'plot' })
  })
})
