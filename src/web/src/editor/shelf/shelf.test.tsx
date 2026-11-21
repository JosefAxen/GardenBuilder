// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import type { CatalogArticle } from '../../api/catalog'
import { useCatalogStore } from '../catalogStore'
import { useEditorUiStore } from '../editorUiStore'
import { useSceneStore } from '../sceneStore'
import { thuja } from '../testArticles'
import { Shelf } from './Shelf'

const rowan: CatalogArticle = { ...thuja, id: 'rowan', name: 'Rönn', variety: null, category: 'Träd', typeModel: 'tree-small' }
const birch: CatalogArticle = { ...rowan, id: 'birch', name: 'Björk', stockStatus: 'out-of-stock' }
const paving: CatalogArticle = {
  ...thuja,
  id: 'paving',
  name: 'Marksten grå',
  variety: null,
  category: 'Markmaterial',
  unit: 'square-meter',
  price: 640,
  geometry: 'surface',
  material: 'paving',
  typeModel: 'surface-paving',
}

afterEach(cleanup)

beforeEach(() => {
  useCatalogStore.setState({ status: 'ready', articles: [rowan, birch, paving] })
  useEditorUiStore.setState({ panel: 'catalog', query: '', category: null })
  useSceneStore.setState({ tool: { mode: 'select' }, selectedId: null })
})

describe('the catalogue shelf', () => {
  it('says so when nothing matches the search', async () => {
    render(<Shelf ready />)
    await userEvent.type(screen.getByRole('searchbox', { name: 'Sök i katalogen' }), 'xyz')
    expect(screen.getByText('Inga artiklar matchar ”xyz”')).toBeTruthy()
  })

  it('Lägg till starts placing that article, and Klar stops', async () => {
    render(<Shelf ready />)
    await userEvent.click(screen.getByRole('button', { name: 'Lägg till Rönn' }))
    expect(useSceneStore.getState().tool).toEqual({ mode: 'place', articleId: 'rowan' })

    await userEvent.click(screen.getByRole('button', { name: 'Klar med Rönn' }))
    expect(useSceneStore.getState().tool).toEqual({ mode: 'select' })
  })

  it('an article out of stock says so and cannot be added', () => {
    render(<Shelf ready />)
    expect(screen.getByText('Slut i lager')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Lägg till Björk' })).toBeNull()
  })

  it('a surface is swapped from the garden, not added', () => {
    render(<Shelf ready />)
    expect(screen.queryByRole('button', { name: 'Lägg till Marksten grå' })).toBeNull()
    expect(screen.getByText('Välj en yta i trädgården för att byta')).toBeTruthy()
  })

  it('a category chip narrows the shelf', async () => {
    render(<Shelf ready />)
    await userEvent.click(screen.getByRole('button', { name: 'Markmaterial' }))
    expect(screen.queryByText('Rönn')).toBeNull()
    expect(screen.getByText('Marksten grå')).toBeTruthy()
  })

  it('keeps the catalogue on the shelf while Foton or Förslag is open beside the rail', () => {
    useEditorUiStore.setState({ panel: 'photos' })
    render(<Shelf ready />)
    expect(screen.getByRole('radio', { name: 'Foton' }).getAttribute('aria-checked')).toBe('true')
    expect(screen.queryByRole('tab')).toBeNull()
    expect(screen.getByRole('searchbox', { name: 'Sök i katalogen' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Lägg till Rönn' })).toBeTruthy()
  })
})
