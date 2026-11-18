import type { Point2 } from '../api/scene'
import { isInsidePolygon } from './placement'

/** Which side of the line p→q the point r is on. */
const cross = (p: Point2, q: Point2, r: Point2) => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x)

const onSegment = (p: Point2, q: Point2, r: Point2) =>
  Math.min(p.x, q.x) <= r.x && r.x <= Math.max(p.x, q.x) && Math.min(p.y, q.y) <= r.y && r.y <= Math.max(p.y, q.y)

function segmentsTouch(a: Point2, b: Point2, c: Point2, d: Point2): boolean {
  const d1 = cross(c, d, a)
  const d2 = cross(c, d, b)
  const d3 = cross(a, b, c)
  const d4 = cross(a, b, d)
  if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true
  return (d1 === 0 && onSegment(c, d, a)) || (d2 === 0 && onSegment(c, d, b)) || (d3 === 0 && onSegment(a, b, c)) || (d4 === 0 && onSegment(a, b, d))
}

/** True when two edges that are not neighbours cross or touch, the same rule as Polygon.IsSelfIntersecting on the server. */
export function isSelfIntersecting(points: Point2[]): boolean {
  const n = points.length
  for (let i = 0; i < n; i++) {
    for (let j = i + 1; j < n; j++) {
      const neighbours = j === i + 1 || (i === 0 && j === n - 1)
      if (neighbours) continue
      if (segmentsTouch(points[i], points[(i + 1) % n], points[j], points[(j + 1) % n])) return true
    }
  }
  return false
}

/** Length of each edge, the last one closing the polygon. */
export function segmentLengths(points: Point2[]): number[] {
  return points.map((p, i) => {
    const q = points[(i + 1) % points.length]
    return Math.hypot(q.x - p.x, q.y - p.y)
  })
}

function distanceToSegment(p: Point2, a: Point2, b: Point2): number {
  const dx = b.x - a.x
  const dy = b.y - a.y
  const lengthSquared = dx * dx + dy * dy
  const t = lengthSquared === 0 ? 0 : Math.min(1, Math.max(0, ((p.x - a.x) * dx + (p.y - a.y) * dy) / lengthSquared))
  return Math.hypot(p.x - (a.x + t * dx), p.y - (a.y + t * dy))
}

/** Zero inside the polygon, otherwise the distance to the nearest edge. Same as Polygon.DistanceTo on the server. */
export function distanceToPolygon(point: Point2, polygon: Point2[]): number {
  if (isInsidePolygon(point, polygon)) return 0
  return Math.min(...polygon.map((a, i) => distanceToSegment(point, a, polygon[(i + 1) % polygon.length])))
}

/** The middle of the points' bounding box. */
export function boxCenter(points: Point2[]): Point2 {
  const xs = points.map((p) => p.x)
  const ys = points.map((p) => p.y)
  return { x: (Math.min(...xs) + Math.max(...xs)) / 2, y: (Math.min(...ys) + Math.max(...ys)) / 2 }
}
