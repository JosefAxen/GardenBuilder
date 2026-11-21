import { sceneLook } from './sceneLook'
import type { ThreeEvent } from '@react-three/fiber'
import { useMemo } from 'react'
import type { Point2 } from '../api/scene'
import type { CatalogArticle } from '../api/catalog'

type Segment = { mid: Point2; length: number; angle: number; from: Point2 }

function segments(points: Point2[]): Segment[] {
  return points.slice(1).map((to, i) => {
    const from = points[i]
    const dx = to.x - from.x
    const dy = to.y - from.y
    return { from, mid: { x: (from.x + to.x) / 2, y: (from.y + to.y) / 2 }, length: Math.hypot(dx, dy), angle: Math.atan2(dy, dx) }
  })
}

/** Fence posts at most two meters apart, centred on the segment. */
function postOffsets(length: number): number[] {
  const gaps = Math.max(1, Math.ceil(length / 2))
  return Array.from({ length: gaps + 1 }, (_, k) => -length / 2 + (k * length) / gaps)
}

/** Hedges and fences are generated in code from the line's points; they need no model files. */
export function LineView({
  points,
  article,
  selected,
  ghost,
  onPointerDown,
}: {
  points: Point2[]
  article: CatalogArticle
  selected?: boolean
  /** Half transparent: the preview while drawing, or an existing line that will be removed. */
  ghost?: boolean
  onPointerDown?: (event: ThreeEvent<PointerEvent>) => void
}) {
  const parts = useMemo(() => segments(points), [points])
  // The catalog requires both for hedges and fences; the fallbacks only guard a broken article.
  const height = article.height ?? 1
  const width = article.width ?? 0.3
  const emissive = selected ? sceneLook.selectedEmissive : '#000000'
  // Keyed on ghost: three.js does not rebuild a material when its transparency changes.
  const material = <meshStandardMaterial key={ghost ? 'ghost' : 'solid'} color={article.color} emissive={emissive} flatShading transparent={ghost} opacity={ghost ? 0.5 : 1} />

  return (
    <group onPointerDown={onPointerDown}>
      {parts.map((s, i) =>
        article.typeModel === 'hedge' ? (
          <mesh key={i} position={[s.mid.x, height / 2, -s.mid.y]} rotation={[0, s.angle, 0]} castShadow receiveShadow>
            <boxGeometry args={[s.length, height, width]} />
            {material}
          </mesh>
        ) : (
          <group key={i} position={[s.mid.x, 0, -s.mid.y]} rotation={[0, s.angle, 0]}>
            {[0.3, 0.8].map((h) => (
              <mesh key={h} position={[0, height * h, 0]} castShadow>
                <boxGeometry args={[s.length, 0.08, 0.04]} />
                {material}
              </mesh>
            ))}
            <mesh position={[0, height * 0.5, 0.03]} castShadow>
              <boxGeometry args={[s.length, height * 0.85, 0.02]} />
              {material}
            </mesh>
            {postOffsets(s.length).map((x) => (
              <mesh key={x} position={[x, height / 2, 0]} castShadow>
                <boxGeometry args={[0.1, height, 0.1]} />
                {material}
              </mesh>
            ))}
          </group>
        ),
      )}
    </group>
  )
}
