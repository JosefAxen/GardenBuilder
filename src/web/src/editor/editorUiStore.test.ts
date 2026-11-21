import { beforeEach, describe, expect, it } from 'vitest'
import { useEditorUiStore } from './editorUiStore'
import { handleEscape } from './keyboard'
import { useSceneStore } from './sceneStore'
import { viewLocked } from './shell/viewLock'

describe('the editor UI state', () => {
  beforeEach(() => {
    useEditorUiStore.setState({ panel: 'catalog', priceOpen: false, query: '', category: null })
    useSceneStore.setState({ selectedId: null, tool: { mode: 'select' } })
  })

  it('starts on the catalogue with nothing open', () => {
    expect(useEditorUiStore.getState()).toMatchObject({ panel: 'catalog', priceOpen: false, query: '', category: null })
  })

  it('Esc closes the price details first and keeps the selection; the next Esc clears it', () => {
    useSceneStore.getState().select('tree-1')
    useEditorUiStore.getState().setPriceOpen(true)

    handleEscape()
    expect(useEditorUiStore.getState().priceOpen).toBe(false)
    expect(useSceneStore.getState().selectedId).toBe('tree-1')

    handleEscape()
    expect(useSceneStore.getState().selectedId).toBeNull()
  })

  it('Esc leaves the placing tool', () => {
    useSceneStore.getState().setTool({ mode: 'place', articleId: 'maple' })
    handleEscape()
    expect(useSceneStore.getState().tool).toEqual({ mode: 'select' })
  })
})

describe('the view lock', () => {
  it.each(['shape', 'draw', 'pin'] as const)('is on while %s', (mode) => {
    expect(viewLocked(mode)).toBe(true)
  })

  it.each(['select', 'place', 'line'] as const)('is off while %s', (mode) => {
    expect(viewLocked(mode)).toBe(false)
  })
})
