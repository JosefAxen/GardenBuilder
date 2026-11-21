import { useEffect } from 'react'
import { useEditorUiStore } from './editorUiStore'
import { useSceneStore } from './sceneStore'
import { removeSelected, rotateSelected } from './selectionCommands'
import { finishShaping } from './shapeCommands'

/**
 * The one Esc path. Open price details close first and nothing else happens; otherwise shaping or pinning ends (which
 * also leaves the top view), any other tool goes back to select, and the selection is cleared.
 */
export function handleEscape(): void {
  const ui = useEditorUiStore.getState()
  if (ui.priceOpen) {
    ui.setPriceOpen(false)
    return
  }
  const { tool, setTool, select } = useSceneStore.getState()
  if (tool.mode === 'shape' || tool.mode === 'draw' || tool.mode === 'pin') finishShaping()
  else setTool({ mode: 'select' })
  select(null)
}

const typing = (target: EventTarget | null) =>
  target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement

/** Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z, Delete, Q and E turn, Esc. Ignored while typing in a field. */
export function useKeyboardShortcuts() {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (typing(event.target)) return
      const { undo, redo } = useSceneStore.getState()
      const key = event.key.toLowerCase()
      if ((event.ctrlKey || event.metaKey) && key === 'z') {
        event.preventDefault()
        if (event.shiftKey) redo()
        else undo()
      } else if ((event.ctrlKey || event.metaKey) && key === 'y') {
        event.preventDefault()
        redo()
      } else if (key === 'delete' || key === 'backspace') removeSelected()
      else if (key === 'escape') handleEscape()
      else if (key === 'q') rotateSelected(1)
      else if (key === 'e') rotateSelected(-1)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])
}
