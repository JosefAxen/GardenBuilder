import { create } from 'zustand'

export type EditorPanel = 'catalog' | 'photos' | 'proposals'

/**
 * What the editor's chrome shows: which panel, whether the price details are open, and the catalogue search.
 * Nothing here is part of the design, so none of it is undoable; the scene lives in sceneStore.
 */
type EditorUiState = {
  panel: EditorPanel
  priceOpen: boolean
  query: string
  /** The catalogue category chip, or null for "Alla". */
  category: string | null
  setPanel: (panel: EditorPanel) => void
  setPriceOpen: (open: boolean) => void
  setQuery: (query: string) => void
  setCategory: (category: string | null) => void
}

export const useEditorUiStore = create<EditorUiState>((set) => ({
  panel: 'catalog',
  priceOpen: false,
  query: '',
  category: null,
  setPanel: (panel) => set({ panel }),
  setPriceOpen: (priceOpen) => set({ priceOpen }),
  setQuery: (query) => set({ query }),
  setCategory: (category) => set({ category }),
}))
