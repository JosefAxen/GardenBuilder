import { beforeEach, describe, expect, it } from 'vitest'
import type { SceneDocument } from '../api/scene'
import { applyCommand, useSceneStore } from './sceneStore'

const tree = {
  kind: 'point' as const,
  id: 'tree',
  status: 'new' as const,
  measurementSource: 'estimated' as const,
  articleId: 'apple',
  position: { x: 2, y: 2 },
  rotation: 0,
}

const scene: SceneDocument = {
  formatVersion: 1,
  elements: [
    {
      kind: 'plot',
      id: 'plot',
      status: 'existing-keep',
      measurementSource: 'estimated',
      sloped: false,
      points: [{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 10 }, { x: 0, y: 10 }],
    },
    tree,
  ],
}

describe('applyCommand returns the same document when nothing changes', () => {
  it('move of an element that does not exist', () => {
    expect(applyCommand(scene, { type: 'move', id: 'gone', position: { x: 1, y: 1 } })).toBe(scene)
  })

  it('move to where the element already is', () => {
    expect(applyCommand(scene, { type: 'move', id: 'tree', position: { x: 2, y: 2 }, rotation: 0 })).toBe(scene)
  })

  it('replace with the article it already has', () => {
    expect(applyCommand(scene, { type: 'replace', id: 'tree', articleId: 'apple' })).toBe(scene)
  })

  it('replace on the plot, which has no article', () => {
    expect(applyCommand(scene, { type: 'replace', id: 'plot', articleId: 'apple' })).toBe(scene)
  })

  it('remove of an element that does not exist', () => {
    expect(applyCommand(scene, { type: 'remove', id: 'gone' })).toBe(scene)
  })

  it('an empty batch', () => {
    expect(applyCommand(scene, { type: 'batch', commands: [] })).toBe(scene)
  })
})

describe('the store', () => {
  beforeEach(() => {
    useSceneStore.setState({ document: scene, past: [], future: [], drag: null, notice: null, selectedId: null })
  })

  it('adds no undo step for a command that changes nothing', () => {
    useSceneStore.getState().execute({ type: 'remove', id: 'gone' })

    expect(useSceneStore.getState().past).toHaveLength(0)
  })

  it('adds one undo step for a command that changes something', () => {
    useSceneStore.getState().execute({ type: 'remove', id: 'tree' })

    expect(useSceneStore.getState().past).toHaveLength(1)
  })

  it('drops a dragged element as one move', () => {
    const { setDrag, dropDrag } = useSceneStore.getState()
    setDrag({ id: 'tree', position: { x: 5, y: 5 }, offset: { x: 0, y: 0 }, moved: true })

    dropDrag()

    const state = useSceneStore.getState()
    expect(state.document.elements[1]).toMatchObject({ position: { x: 5, y: 5 } })
    expect(state.past).toHaveLength(1)
    expect(state.drag).toBeNull()
  })

  it('refuses to drop outside the plot and says why', () => {
    const { setDrag, dropDrag } = useSceneStore.getState()
    setDrag({ id: 'tree', position: { x: 50, y: 5 }, offset: { x: 0, y: 0 }, moved: true })

    dropDrag()

    const state = useSceneStore.getState()
    expect(state.document).toBe(scene)
    expect(state.notice).toBe('Det går inte att placera utanför tomten.')
    expect(state.drag).toBeNull()
  })

  it('cancels a drag without moving anything', () => {
    const { setDrag, cancelDrag } = useSceneStore.getState()
    setDrag({ id: 'tree', position: { x: 5, y: 5 }, offset: { x: 0, y: 0 }, moved: true })

    cancelDrag()

    const state = useSceneStore.getState()
    expect(state.document).toBe(scene)
    expect(state.drag).toBeNull()
  })
})
