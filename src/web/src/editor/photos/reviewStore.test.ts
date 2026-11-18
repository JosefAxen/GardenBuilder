import { beforeEach, describe, expect, it } from 'vitest'
import type { SceneDocument, SceneElement } from '../../api/scene'
import { useSceneStore } from '../sceneStore'
import { confidenceText, useReviewStore } from './reviewStore'

const plot: SceneElement = {
  kind: 'plot',
  id: 'plot',
  status: 'existing-keep',
  measurementSource: 'customer',
  sloped: false,
  points: [{ x: 0, y: 0 }, { x: 20, y: 0 }, { x: 20, y: 20 }, { x: 0, y: 20 }],
}
const house: SceneElement = {
  kind: 'building',
  id: 'house',
  status: 'existing-keep',
  measurementSource: 'customer',
  height: 5,
  roof: 'gable',
  points: [{ x: 14, y: 16 }, { x: 18, y: 16 }, { x: 18, y: 19 }, { x: 14, y: 19 }],
}
const scene: SceneDocument = { formatVersion: 2, elements: [plot, house] }

const tree = (id: string, x: number): SceneElement => ({
  kind: 'point',
  id,
  status: 'existing-keep',
  measurementSource: 'estimated',
  articleId: null,
  form: 'tree-large',
  width: 5,
  height: 9,
  position: { x, y: 5 },
  rotation: 0,
})

const ids = () => useSceneStore.getState().document.elements.map((e) => e.id)

describe('review of a photo interpretation', () => {
  beforeEach(() => {
    useSceneStore.getState().load(scene)
    useReviewStore.getState().show({
      elements: [
        { label: 'Björk', confidence: 0.9, element: tree('a', 5) },
        { label: 'Lönn', confidence: 0.5, element: tree('b', 10) },
      ],
      house: { wallColor: '#E3B23C', roofColor: '#2B2B2B', roof: 'gable' },
      dropped: [],
      skippedPhotos: 0,
    })
  })

  it('take adds the element and undo removes it', () => {
    useReviewStore.getState().take('a')
    expect(ids()).toContain('a')
    expect(useReviewStore.getState().proposals.map((p) => p.element.id)).toEqual(['b'])
    useSceneStore.getState().undo()
    expect(ids()).not.toContain('a')
  })

  it('takeAll is one undo step', () => {
    useReviewStore.getState().takeAll()
    expect(ids()).toEqual(['plot', 'house', 'a', 'b'])
    expect(useReviewStore.getState().proposals).toEqual([])
    useSceneStore.getState().undo()
    expect(ids()).toEqual(['plot', 'house'])
  })

  it('drop removes it from the list only', () => {
    useReviewStore.getState().drop('a')
    expect(useReviewStore.getState().proposals.map((p) => p.element.id)).toEqual(['b'])
    expect(ids()).toEqual(['plot', 'house'])
  })

  it('takeHouse sets wall and roof colour on the building', () => {
    useReviewStore.getState().takeHouse()
    const building = useSceneStore.getState().document.elements.find((e) => e.id === 'house')
    expect(building).toMatchObject({ wallColor: '#E3B23C', roofColor: '#2B2B2B', roof: 'gable' })
    expect(useReviewStore.getState().house).toBeNull()
    useSceneStore.getState().undo()
    expect(useSceneStore.getState().document.elements.find((e) => e.id === 'house')).not.toHaveProperty('wallColor')
  })

  it('a proposal that no longer fits the plot is removed with a notice', () => {
    useSceneStore.getState().execute({ type: 'redraw', id: 'plot', points: [{ x: 8, y: 0 }, { x: 20, y: 0 }, { x: 20, y: 20 }, { x: 8, y: 20 }] })
    useReviewStore.getState().take('a')
    expect(ids()).not.toContain('a')
    expect(useReviewStore.getState().proposals.map((p) => p.element.id)).toEqual(['b'])
    expect(useSceneStore.getState().notice).toBe('Passar inte längre på tomten.')
  })

  it('confidence in words', () => {
    expect([confidenceText(0.9), confidenceText(0.5), confidenceText(0.3)]).toEqual(['säker', 'ganska säker', 'osäker'])
  })
})
