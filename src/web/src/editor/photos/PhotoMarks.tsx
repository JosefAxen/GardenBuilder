import { Line } from '@react-three/drei'
import { useMemo } from 'react'
import { Shape, Vector2 } from 'three'
import type { Point2, SceneElement } from '../../api/scene'
import { useSceneStore } from '../sceneStore'
import { usePhotoStore } from './photoStore'
import { useReviewStore } from './reviewStore'

const at = (p: Point2, y = 0.08): [number, number, number] => [p.x, y, -p.y]

/** A point `length` meters from `from` in the heading's direction (degrees from north, clockwise). */
function ahead(from: Point2, heading: number, length: number): Point2 {
  const radians = (heading * Math.PI) / 180
  return { x: from.x + Math.sin(radians) * length, y: from.y + Math.cos(radians) * length }
}

function CameraMark({ position, heading, color }: { position: Point2; heading: number; color: string }) {
  return (
    <>
      <mesh position={at(position)} rotation={[-Math.PI / 2, 0, 0]} renderOrder={10}>
        <circleGeometry args={[0.4, 20]} />
        <meshBasicMaterial color={color} depthTest={false} />
      </mesh>
      <Line points={[at(position), at(ahead(position, heading, 3))]} color={color} lineWidth={3} depthTest={false} renderOrder={10} />
    </>
  )
}

/** Where each photo was taken, as a dot with a line in the camera's direction, and the camera being placed now. */
export function CameraPins() {
  const photos = usePhotoStore((s) => s.photos)
  const tool = useSceneStore((s) => s.tool)
  const hover = useSceneStore((s) => s.hover)

  return (
    <>
      {photos.map((p) =>
        p.camera && !(tool.mode === 'pin' && tool.photoId === p.id) ? (
          <CameraMark key={p.id} position={{ x: p.camera.x, y: p.camera.y }} heading={p.camera.heading} color="#c2410c" />
        ) : null,
      )}
      {tool.mode === 'pin' && tool.position && (
        <>
          <CameraMark position={tool.position} heading={0} color="#f59e0b" />
          {hover && <Line points={[at(tool.position), at(hover)]} color="#f59e0b" lineWidth={3} depthTest={false} renderOrder={10} />}
        </>
      )}
    </>
  )
}

function Ghost({ element }: { element: SceneElement }) {
  const shape = useMemo(
    () => (element.kind === 'surface' ? new Shape(element.points.map((p) => new Vector2(p.x, p.y))) : null),
    [element],
  )
  switch (element.kind) {
    case 'surface':
      return (
        <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.05, 0]} raycast={() => null}>
          <shapeGeometry args={[shape!]} />
          <meshBasicMaterial color="#4f9d4f" transparent opacity={0.4} depthWrite={false} />
        </mesh>
      )
    case 'line':
      return <Line points={element.points.map((p) => at(p, 0.6))} color="#2f6b35" lineWidth={6} transparent opacity={0.5} />
    case 'point': {
      const width = element.width ?? 1
      const height = element.height ?? 2
      return (
        <mesh position={[element.position.x, height / 2, -element.position.y]} raycast={() => null}>
          <cylinderGeometry args={[width / 2, width / 2, height, 16]} />
          <meshBasicMaterial color="#3d8b3d" transparent opacity={0.4} depthWrite={false} />
        </mesh>
      )
    }
    default:
      return null
  }
}

/** The proposals not yet taken, drawn see-through so the customer can compare them with what is in the scene. */
export function ProposalGhosts() {
  const proposals = useReviewStore((s) => s.proposals)
  return (
    <>
      {proposals.map((p) => (
        <Ghost key={p.element.id} element={p.element} />
      ))}
    </>
  )
}
