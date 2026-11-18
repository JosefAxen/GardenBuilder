import { newId } from '../api/ids'
import type { SceneElement, SurfaceMaterial } from '../api/scene'
import { useSceneStore } from './sceneStore'

const ROTATE_STEP = Math.PI / 12

function selectedElement(): SceneElement | undefined {
  const { document, selectedId } = useSceneStore.getState()
  return document.elements.find((e) => e.id === selectedId)
}

/** Turns the selected tree or pot 15 degrees, as one command. */
export function rotateSelected(direction: 1 | -1) {
  const element = selectedElement()
  if (element?.kind !== 'point') return
  useSceneStore.getState().execute({ type: 'move', id: element.id, position: element.position, rotation: element.rotation + direction * ROTATE_STEP })
}

/**
 * Removes the selected point, line or surface. A new one disappears. An existing one is only marked for removal and
 * stays selected, so the panel can offer to keep it. The plot and the house are not removed here.
 */
export function removeSelected() {
  const element = selectedElement()
  if (!element || (element.kind !== 'point' && element.kind !== 'line' && element.kind !== 'surface')) return
  const { execute, select } = useSceneStore.getState()
  execute({ type: 'remove', id: element.id })
  if (element.status === 'new') select(null)
}

/** Takes back the removal of the selected existing element. */
export function keepSelected() {
  const element = selectedElement()
  if (element) useSceneStore.getState().execute({ type: 'keep', id: element.id })
}

/**
 * Swaps the selected element for another article. A new element just gets the new article. An existing one stays in
 * the scene marked for removal (taking it away costs work), and a new copy in the same place takes the new article.
 * Both happen in one batch, so one undo brings the old element back.
 */
export function replaceSelected(articleId: string, material?: SurfaceMaterial) {
  const element = selectedElement()
  if (!element || !('articleId' in element) || element.status === 'existing-remove') return
  const { execute, select } = useSceneStore.getState()
  if (element.status === 'new') {
    execute({ type: 'replace', id: element.id, articleId, material })
    return
  }

  const copy = { ...element, id: newId(), status: 'new' as const, articleId }
  const added = copy.kind === 'surface' && material ? { ...copy, material } : copy
  execute({ type: 'batch', commands: [{ type: 'remove', id: element.id }, { type: 'add', element: added }] })
  select(added.id)
}

/** How close two elements must be to count as standing in the same place. */
const SAME_PLACE = 0.3

const anchorOf = (element: SceneElement) =>
  element.kind === 'point' ? element.position : 'points' in element ? element.points[0] : undefined

/**
 * What a click on this element should select. After "Byt" the removed original stays exactly under its replacement;
 * a click there means the replacement, so a removed element gives way to a kept one of the same kind in the same place.
 */
export function pickTarget(clickedId: string, elements: SceneElement[]): string {
  const clicked = elements.find((e) => e.id === clickedId)
  const at = clicked && anchorOf(clicked)
  if (!clicked || !at || clicked.status !== 'existing-remove') return clickedId
  const onTop = elements.find((e) => {
    const other = anchorOf(e)
    return e.kind === clicked.kind && e.status !== 'existing-remove' && other && Math.hypot(other.x - at.x, other.y - at.y) < SAME_PLACE
  })
  return onTop?.id ?? clickedId
}
