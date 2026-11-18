import type { Point2, SceneDocument, SceneElement } from '../api/scene'
import { distanceToPolygon, isSelfIntersecting } from './geometry'
import { applyCommand, type SceneCommand, useSceneStore } from './sceneStore'

/** How far outside the plot a point may lie, the same tolerance as SceneValidator.PlotTolerance on the server. */
export const PLOT_TOLERANCE = 0.5

const pointsOf = (element: SceneElement): Point2[] => {
  switch (element.kind) {
    case 'point':
      return [element.position]
    case 'plot':
      return []
    default:
      return element.points
  }
}

/**
 * Why this shape of the plot and the house cannot be kept, in Swedish, or null when it can. Checked before every shape
 * change, so the editor never holds a scene the server would refuse to save.
 */
export function shapeProblem(document: SceneDocument): string | null {
  const plot = document.elements.find((e) => e.kind === 'plot')
  if (!plot) return null
  if (isSelfIntersecting(plot.points)) return 'Tomtgränsen får inte korsa sig själv.'

  const buildings = document.elements.filter((e) => e.kind === 'building')
  if (buildings.some((b) => isSelfIntersecting(b.points))) return 'Huset får inte korsa sig självt.'
  const outside = (e: SceneElement) => pointsOf(e).some((p) => distanceToPolygon(p, plot.points) > PLOT_TOLERANCE)
  if (buildings.some(outside)) return 'Huset måste ligga inom tomten.'

  const things = document.elements.filter((e) => e.kind !== 'plot' && e.kind !== 'building' && outside(e)).length
  if (things === 1) return '1 sak hamnar utanför tomten. Flytta den först.'
  if (things > 1) return `${things} saker hamnar utanför tomten. Flytta dem först.`
  return null
}

/** Runs a shape command when the result is a shape that can be kept; otherwise leaves the scene and says why. */
export function tryShape(command: SceneCommand): void {
  const { document, execute, setNotice } = useSceneStore.getState()
  const problem = shapeProblem(applyCommand(document, command))
  if (problem) setNotice(problem)
  else execute(command)
}

/** A click this close to the first corner closes the drawing. */
export const CLOSE_DISTANCE = 0.75

/** Starts the shape tool on the plot or the house (the first building), seen from above. */
export function startShaping(kind: 'plot' | 'building'): void {
  const { document, setTool, setView, select } = useSceneStore.getState()
  const element = document.elements.find((e) => e.kind === kind)
  if (!element) return
  select(null)
  setTool({ mode: 'shape', id: element.id })
  setView('top')
}

export function finishShaping(): void {
  const { setTool, setView, setShapeDrag } = useSceneStore.getState()
  setShapeDrag(null)
  setTool({ mode: 'select' })
  setView('perspective')
}

/** Ends a corner drag: one shape command, kept only when the shape is allowed. */
export function dropShapeDrag(): void {
  const { shapeDrag, setShapeDrag } = useSceneStore.getState()
  if (!shapeDrag) return
  setShapeDrag(null)
  const { id, index, point } = shapeDrag
  tryShape(shapeDrag.insert ? { type: 'insert-point', id, index, point } : { type: 'move-point', id, index, point })
}

/** Switches the shape tool to drawing the same element anew. */
export function startDrawing(): void {
  const { tool, setTool } = useSceneStore.getState()
  if (tool.mode === 'shape') setTool({ mode: 'draw', id: tool.id, points: [] })
}

/** A click while drawing: adds a corner, or closes the shape on a click near the first corner. */
export function drawClick(point: Point2): void {
  const { tool, setTool } = useSceneStore.getState()
  if (tool.mode !== 'draw') return
  const [first] = tool.points
  if (first && tool.points.length >= 3 && Math.hypot(point.x - first.x, point.y - first.y) <= CLOSE_DISTANCE) {
    setTool({ mode: 'shape', id: tool.id })
    tryShape({ type: 'redraw', id: tool.id, points: tool.points })
    return
  }
  setTool({ ...tool, points: [...tool.points, point] })
}
