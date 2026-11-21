// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import type { CatalogArticle } from '../../api/catalog'
import type { SceneDocument, SceneElement } from '../../api/scene'
import { useCatalogStore } from '../catalogStore'
import { useSceneStore } from '../sceneStore'
import { thuja } from '../testArticles'
import { PlantLabel } from './PlantLabel'

const plot: SceneElement = {
  kind: 'plot',
  id: 'plot',
  status: 'existing-keep',
  measurementSource: 'customer',
  sloped: false,
  points: [{ x: 0, y: 0 }, { x: 20, y: 0 }, { x: 20, y: 20 }, { x: 0, y: 20 }],
} as SceneElement
const lawn = {
  kind: 'surface',
  id: 'lawn',
  status: 'existing-keep',
  measurementSource: 'customer',
  articleId: null,
  material: 'grass',
  points: [{ x: 1, y: 1 }, { x: 4, y: 1 }, { x: 4, y: 4 }],
} as SceneElement
const tree = {
  kind: 'point',
  id: 'tree',
  status: 'new',
  measurementSource: 'estimated',
  articleId: 'thuja',
  position: { x: 5, y: 5 },
  rotation: 0,
} as SceneElement
const paving: CatalogArticle = {
  ...thuja,
  id: 'paving',
  name: 'Marksten grå',
  variety: null,
  geometry: 'surface',
  material: 'paving',
  typeModel: 'surface-paving',
  unit: 'square-meter',
  price: 640,
}
const otherThuja: CatalogArticle = { ...thuja, id: 'thuja-2', name: 'Tuja Smaragd 80–100 cm', height: 0.9 }
const newPaving = { ...lawn, id: 'patio', status: 'new', articleId: 'paving', material: 'paving' } as SceneElement

function show(element: SceneElement) {
  const document: SceneDocument = { formatVersion: 2, elements: [plot, element] } as SceneDocument
  useSceneStore.getState().load(document)
  useSceneStore.getState().select(element.id)
  return render(<PlantLabel />)
}

afterEach(cleanup)

beforeEach(() => {
  useCatalogStore.setState({ status: 'ready', articles: [thuja, otherThuja, paving] })
})

describe('the plant label', () => {
  it('is absent with nothing selected', () => {
    useSceneStore.getState().select(null)
    render(<PlantLabel />)
    expect(screen.queryByRole('region', { name: /Valt/ })).toBeNull()
  })

  it('shows a new plant with its facts, price and turn buttons', () => {
    show(tree)
    expect(screen.getByText('Ny i designen')).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Tuja Smaragd 40–60 cm' })).toBeTruthy()
    expect(screen.getByText('Växtzon')).toBeTruthy()
    expect(screen.getByText('189 kr')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Vrid åt vänster (Q)' })).toBeTruthy()
  })

  it('an existing lawn without an article has a name and no price row', () => {
    show(lawn)
    expect(screen.getByRole('heading', { name: 'Befintlig yta (gräs)' })).toBeTruthy()
    expect(screen.getByText('Befintlig')).toBeTruthy()
    expect(screen.queryByText(/ kr/)).toBeNull()
  })

  it('an element marked for removal can only be kept', async () => {
    show({ ...tree, status: 'existing-remove' } as SceneElement)
    expect(screen.getByText('Tas bort')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Byt' })).toBeNull()

    await userEvent.click(screen.getByRole('button', { name: 'Behåll' }))

    expect(useSceneStore.getState().document.elements.find((e) => e.id === 'tree')?.status).toBe('existing-keep')
  })

  it('a surface article can be swapped but not turned', () => {
    show(newPaving)
    expect(screen.getByText('640 kr/m²')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Vrid åt vänster (Q)' })).toBeNull()
  })

  it('"Byt mot" stays folded until Byt is clicked', async () => {
    show(tree)
    expect(screen.queryByText('Byt mot')).toBeNull()

    await userEvent.click(screen.getByRole('button', { name: 'Byt' }))

    expect(screen.getByText('Byt mot')).toBeTruthy()
    expect(screen.getByText('Tuja Smaragd 80–100 cm')).toBeTruthy()
  })
})
