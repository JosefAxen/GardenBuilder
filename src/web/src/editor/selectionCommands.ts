import { useSceneStore } from './sceneStore'

const ROTATE_STEP = Math.PI / 12

/** Turns the selected tree or pot 15 degrees, as one command. */
export function rotateSelected(direction: 1 | -1) {
  const { document, selectedId, execute } = useSceneStore.getState()
  const element = document.elements.find((e) => e.id === selectedId)
  if (element?.kind !== 'point') return
  execute({ type: 'move', id: element.id, position: element.position, rotation: element.rotation + direction * ROTATE_STEP })
}

/** Removes the selected point or line. The plot, the house and surfaces are not removed here. */
export function removeSelected() {
  const { selectedId, execute, select, document } = useSceneStore.getState()
  const element = document.elements.find((e) => e.id === selectedId)
  if (!element || (element.kind !== 'point' && element.kind !== 'line')) return
  execute({ type: 'remove', id: element.id })
  select(null)
}
