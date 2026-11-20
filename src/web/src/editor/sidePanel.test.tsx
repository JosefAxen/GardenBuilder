// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import type { SceneDocument, SceneElement } from '../api/scene'
import { useCatalogStore } from './catalogStore'
import { useEditorUiStore } from './editorUiStore'
import { PlantLabel } from './label/PlantLabel'
import { useSaveStore } from './saveStore'
import { useSceneStore } from './sceneStore'
import { Shelf } from './shelf/Shelf'
import { SidePanel } from './shell/SidePanel'
import { thuja } from './testArticles'

const tree = {
  kind: 'point',
  id: 'tree',
  status: 'new',
  measurementSource: 'estimated',
  articleId: 'thuja',
  position: { x: 5, y: 5 },
  rotation: 0,
} as SceneElement

function Editor() {
  return (
    <>
      <SidePanel ready />
      <PlantLabel />
      <Shelf ready />
    </>
  )
}

afterEach(cleanup)

beforeEach(() => {
  useCatalogStore.setState({ status: 'ready', articles: [thuja] })
  useEditorUiStore.setState({ panel: 'catalog', query: '', category: null })
  useSaveStore.setState({ gardenId: null, designId: null, status: 'idle' })
  useSceneStore.getState().load({ formatVersion: 2, elements: [tree] } as SceneDocument)
})

describe('the side panel', () => {
  it('opens Förslag from the shelf tab and Stäng goes back to the catalogue', async () => {
    render(<Editor />)
    await userEvent.click(screen.getByRole('tab', { name: 'Förslag' }))

    expect(screen.getByRole('heading', { name: 'Förslag', level: 2 })).toBeTruthy()

    await userEvent.click(screen.getByRole('button', { name: 'Stäng' }))

    expect(useEditorUiStore.getState().panel).toBe('catalog')
    expect(screen.queryByRole('heading', { name: 'Förslag', level: 2 })).toBeNull()
    expect(screen.getByRole('button', { name: /Lägg till Tuja Smaragd/ })).toBeTruthy()
  })

  it('the plant label stays while the side panel is open', async () => {
    useSceneStore.getState().select('tree')
    render(<Editor />)
    await userEvent.click(screen.getByRole('tab', { name: 'Foton' }))

    expect(screen.getByRole('heading', { name: 'Foton', level: 2 })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Tuja Smaragd 40–60 cm' })).toBeTruthy()
  })
})
