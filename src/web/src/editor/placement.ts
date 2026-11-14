import type { Point2, SceneDocument } from '../api/scene'

/** Lines shorter than this are a double click on the same spot, not a hedge. */
export const MIN_LINE_LENGTH = 0.1

const OUTSIDE_PLOT = 'Det går inte att placera utanför tomten.'
const LINE_TOO_SHORT = 'Linjen är för kort.'

/** Ray casting point-in-polygon test, the same rule as Polygon.Contains on the server. */
export function isInsidePolygon(point: Point2, polygon: Point2[]): boolean {
  let inside = false
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const a = polygon[i]
    const b = polygon[j]
    if (a.y > point.y !== b.y > point.y && point.x < ((b.x - a.x) * (point.y - a.y)) / (b.y - a.y) + a.x) inside = !inside
  }
  return inside
}

/**
 * Why the points cannot be placed, in Swedish for the customer, or null when they can. One point is a tree or a pot;
 * two or more are a line. Checking here keeps the editor from building scenes the server would reject.
 */
export function placementProblem(document: SceneDocument, points: Point2[]): string | null {
  const plot = document.elements.find((e) => e.kind === 'plot')
  if (plot && !points.every((p) => isInsidePolygon(p, plot.points))) return OUTSIDE_PLOT

  if (points.length >= 2) {
    const length = points.slice(1).reduce((sum, p, i) => sum + Math.hypot(p.x - points[i].x, p.y - points[i].y), 0)
    if (length < MIN_LINE_LENGTH) return LINE_TOO_SHORT
  }

  return null
}
