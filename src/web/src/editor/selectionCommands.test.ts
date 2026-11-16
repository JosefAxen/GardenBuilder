import { beforeEach, describe, expect, it } from 'vitest'
import type { SceneDocument } from '../api/scene'
import { useSceneStore } from './sceneStore'
import { keepSelected, removeSelected, replaceSelected } from './selectionCommands'

const plot = {
  kind: 'plot' as const,
  id: 'plot',
  status: 'existing-keep' as const,
  measurementSource: 'estimated' as const,
  sloped: false,
  points: [{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 10 }, { x: 0, y: 10 }],
}

const oldTree = {
  kind: 'point' as const,
  id: 'old-tree',
  status: 'existing-keep' as const,
  measurementSource: 'customer' as const,
  articleId: 'apple',
  position: { x: 2, y: 3 },
  rotation: 0.5,
}

const newTree = { ...oldTree, id: 'new-tree', status: 'new' as const, measurementSource: 'estimated' as const, position: { x: 6, y: 6 } }

const lawn = {
  kind: 'surface' as const,
  id: 'lawn',
  status: 'existing-keep' as const,
  measurementSource: 'customer' as const,
  articleId: null,
  material: 'grass' as const,
  points: [{ x: 1, y: 1 }, { x: 4, y: 1 }, { x: 4, y: 4 }],
}

const scene: SceneDocument = { formatVersion: 1, elements: [plot, oldTree, newTree, lawn] }

function start(selectedId: string) {
  useSceneStore.setState({ document: scene, past: [], future: [], selectedId })
}

describe('replacing the selected element', () => {
  beforeEach(() => start('old-tree'))

  it('replacing an existing element adds a new one and marks the old as removed', () => {
    replaceSelected('pear')

    const { document, selectedId } = useSceneStore.getState()
    expect(document.elements).toHaveLength(5)
    expect(document.elements[1]).toEqual({ ...oldTree, status: 'existing-remove' })
    const added = document.elements[4]
    expect(added).toMatchObject({
      kind: 'point',
      status: 'new',
      articleId: 'pear',
      position: { x: 2, y: 3 },
      rotation: 0.5,
      measurementSource: 'customer',
    })
    expect(added.id).not.toBe('old-tree')
    expect(selectedId).toBe(added.id)
  })

  it('one undo after replacing an existing element restores it', () => {
    replaceSelected('pear')

    useSceneStore.getState().undo()

    expect(useSceneStore.getState().document).toBe(scene)
  })

  it('replacing a new element only swaps the article', () => {
    start('new-tree')

    replaceSelected('pear')

    const { document, selectedId } = useSceneStore.getState()
    expect(document.elements).toHaveLength(4)
    expect(document.elements[2]).toEqual({ ...newTree, articleId: 'pear' })
    expect(selectedId).toBe('new-tree')
  })

  it('replacing an existing surface copies its points and takes the new material', () => {
    start('lawn')

    replaceSelected('paving-article', 'paving')

    const { document } = useSceneStore.getState()
    expect(document.elements[3]).toMatchObject({ id: 'lawn', status: 'existing-remove' })
    expect(document.elements[4]).toMatchObject({
      kind: 'surface',
      status: 'new',
      articleId: 'paving-article',
      material: 'paving',
      points: lawn.points,
      measurementSource: 'customer',
    })
  })
})

describe('removing and keeping the selected element', () => {
  it('removing an existing surface marks it and keeps it selected', () => {
    start('lawn')

    removeSelected()

    const { document, selectedId } = useSceneStore.getState()
    expect(document.elements[3]).toMatchObject({ id: 'lawn', status: 'existing-remove' })
    expect(selectedId).toBe('lawn')
  })

  it('keep brings a removed element back', () => {
    start('old-tree')
    removeSelected()

    keepSelected()

    expect(useSceneStore.getState().document.elements[1]).toEqual(oldTree)
  })

  it('removing a new element deletes it and clears the selection', () => {
    start('new-tree')

    removeSelected()

    const { document, selectedId } = useSceneStore.getState()
    expect(document.elements.map((e) => e.id)).toEqual(['plot', 'old-tree', 'lawn'])
    expect(selectedId).toBeNull()
  })
})
