import { beforeEach, describe, expect, it } from 'vitest'
import type { SceneDocument, SceneView } from '../../api/scene'
import { applyCommand, useSceneStore } from '../sceneStore'
import { viewProblem } from './viewRules'

const plotScene: SceneDocument = {
  formatVersion: 3,
  elements: [
    {
      kind: 'plot',
      id: 'plot',
      status: 'existing-keep',
      measurementSource: 'estimated',
      sloped: false,
      points: [{ x: 0, y: 0 }, { x: 20, y: 0 }, { x: 20, y: 20 }, { x: 0, y: 20 }],
    },
  ],
  views: [],
}

function view(id: string, overrides: Partial<SceneView> = {}): SceneView {
  return {
    id,
    name: 'Från altanen',
    eye: { x: 5, y: -3, height: 1.7 },
    target: { x: 10, y: 10, height: 1 },
    fieldOfView: 50,
    ...overrides,
  }
}

describe('view commands', () => {
  beforeEach(() => useSceneStore.getState().load(plotScene))

  it('add-view then undo gives back the exact previous document', () => {
    const { execute, undo } = useSceneStore.getState()
    execute({ type: 'add-view', view: view('a') })
    expect(useSceneStore.getState().document.views).toEqual([view('a')])
    undo()
    expect(useSceneStore.getState().document).toBe(plotScene)
  })

  it('add-view works on a document without views', () => {
    const { views: _, ...withoutViews } = plotScene
    expect(applyCommand(withoutViews, { type: 'add-view', view: view('a') }).views).toEqual([view('a')])
  })

  it('remove-view removes only that id', () => {
    const scene = { ...plotScene, views: [view('a'), view('b'), view('c')] }
    expect(applyCommand(scene, { type: 'remove-view', id: 'b' }).views?.map((v) => v.id)).toEqual(['a', 'c'])
  })

  it('remove-view of an unknown id changes nothing', () => {
    const scene = { ...plotScene, views: [view('a')] }
    expect(applyCommand(scene, { type: 'remove-view', id: 'x' })).toBe(scene)
  })
})

describe('viewProblem', () => {
  it('accepts a valid view', () => {
    expect(viewProblem(view('a'), plotScene)).toBeNull()
  })

  it('asks for a name when it is blank', () => {
    expect(viewProblem(view('a', { name: '   ' }), plotScene)).toBe('Ge vyn ett namn.')
  })

  it('accepts a camera 9 m outside the plot', () => {
    expect(viewProblem(view('a', { eye: { x: 5, y: -9, height: 1.7 } }), plotScene)).toBeNull()
  })

  it('refuses a camera 11 m outside the plot', () => {
    expect(viewProblem(view('a', { eye: { x: 5, y: -11, height: 1.7 } }), plotScene)).toBe('Ställ kameran närmare trädgården.')
  })

  it('refuses a ninth view', () => {
    const full = { ...plotScene, views: Array.from({ length: 8 }, (_, i) => view(`v${i}`)) }
    expect(viewProblem(view('new'), full)).toBe('Du kan spara högst 8 vyer.')
  })
})
