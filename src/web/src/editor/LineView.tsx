import type { ThreeEvent } from '@react-three/fiber'
import { useMemo } from 'react'
import type { Point2 } from '../api/scene'
import type { LineArticle } from './demoCatalog'

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
  article: LineArticle
  selected?: boolean
  ghost?: boolean
  onPointerDown?: (event: ThreeEvent<PointerEvent>) => void
}) {
  const parts = useMemo(() => segments(points), [points])
  const emissive = selected ? '#665500' : '#000000'
  const material = <meshStandardMaterial color={article.color} emissive={emissive} flatShading transparent={ghost} opacity={ghost ? 0.5 : 1} />

  return (
    <group onPointerDown={onPointerDown}>
      {parts.map((s, i) =>
        article.style === 'hedge' ? (
          <mesh key={i} position={[s.mid.x, article.height / 2, -s.mid.y]} rotation={[0, s.angle, 0]} castShadow receiveShadow>
            <boxGeometry args={[s.length, article.height, article.width]} />
            {material}
          </mesh>
        ) : (
          <group key={i} position={[s.mid.x, 0, -s.mid.y]} rotation={[0, s.angle, 0]}>
            {[0.3, 0.8].map((h) => (
              <mesh key={h} position={[0, article.height * h, 0]} castShadow>
                <boxGeometry args={[s.length, 0.08, 0.04]} />
                {material}
              </mesh>
            ))}
            <mesh position={[0, article.height * 0.5, 0.03]} castShadow>
              <boxGeometry args={[s.length, article.height * 0.85, 0.02]} />
              {material}
            </mesh>
            {postOffsets(s.length).map((x) => (
              <mesh key={x} position={[x, article.height / 2, 0]} castShadow>
                <boxGeometry args={[0.1, article.height, 0.1]} />
                {material}
              </mesh>
            ))}
          </group>
        ),
      )}
    </group>
  )
}
