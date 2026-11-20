import { sceneLook } from './sceneLook'
import { Html, Line, OrthographicCamera } from '@react-three/drei'
import { type ThreeEvent, useThree } from '@react-three/fiber'
import { useState } from 'react'
import type { Point2 } from '../api/scene'
import { segmentLengths } from './geometry'
import { type ShapeDrag, useSceneStore } from './sceneStore'
import { tryShape } from './shapeCommands'

const HANDLE_Y = 0.06
const toVector = (p: Point2): [number, number, number] => [p.x, HANDLE_Y, -p.y]
const meters = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 1, minimumFractionDigits: 1 })

/** The corners as they look while a handle is dragged, before the drop makes it a command. */
function preview(points: Point2[], drag: ShapeDrag | null, id: string): Point2[] {
  if (!drag || drag.id !== id) return points
  return drag.insert ? points.toSpliced(drag.index, 0, drag.point) : points.with(drag.index, drag.point)
}

function Handle({ point, radius, color, onPointerDown, onDoubleClick }: {
  point: Point2
  radius: number
  color: string
  onPointerDown?: (event: ThreeEvent<PointerEvent>) => void
  onDoubleClick?: (event: ThreeEvent<MouseEvent>) => void
}) {
  return (
    // Drawn on top of everything, so the house never hides its own corners in the top view.
    <mesh position={toVector(point)} rotation={[-Math.PI / 2, 0, 0]} renderOrder={10} onPointerDown={onPointerDown} onDoubleClick={onDoubleClick}>
      <circleGeometry args={[radius, 24]} />
      <meshBasicMaterial color={color} depthTest={false} />
    </mesh>
  )
}

/** Corner and mid-edge handles, and the length of every edge, for the plot or building being shaped. */
export function ShapeHandles() {
  const tool = useSceneStore((s) => s.tool)
  const elements = useSceneStore((s) => s.document.elements)
  const shapeDrag = useSceneStore((s) => s.shapeDrag)
  const hover = useSceneStore((s) => s.hover)
  // The top view's camera is orthographic, where zoom is pixels per meter: handles keep the same size on screen.
  const pixelsPerMeter = useThree((s) => s.camera.zoom)
  const px = (pixels: number) => pixels / Math.max(pixelsPerMeter, 1)

  if (tool.mode === 'draw') {
    const drawn = hover ? [...tool.points, hover] : tool.points
    return (
      <>
        {drawn.length >= 2 && <Line points={drawn.map(toVector)} color={sceneLook.handle} lineWidth={2} depthTest={false} renderOrder={9} />}
        {tool.points.map((p, i) => (
          <Handle key={i} point={p} radius={px(i === 0 ? 9 : 6)} color={i === 0 ? sceneLook.firstHandle : sceneLook.handle} />
        ))}
      </>
    )
  }

  if (tool.mode !== 'shape') return null
  const element = elements.find((e) => e.id === tool.id)
  if (!element || (element.kind !== 'plot' && element.kind !== 'building')) return null
  const points = preview(element.points, shapeDrag, element.id)
  const lengths = segmentLengths(points)
  const startDrag = (index: number, insert: boolean, point: Point2) => (event: ThreeEvent<PointerEvent>) => {
    if (event.button !== 0) return
    event.stopPropagation()
    useSceneStore.getState().setShapeDrag({ id: element.id, index, insert, point })
  }

  return (
    <>
      <Line points={[...points, points[0]].map(toVector)} color={sceneLook.handle} lineWidth={2} depthTest={false} renderOrder={9} />
      {points.map((p, i) => {
        const next = points[(i + 1) % points.length]
        const middle = { x: (p.x + next.x) / 2, y: (p.y + next.y) / 2 }
        // A third of the way along, so the label never hides the mid-edge handle.
        const labelAt = { x: p.x + (next.x - p.x) / 3, y: p.y + (next.y - p.y) / 3 }
        return (
          <group key={i}>
            <Handle
              point={p}
              radius={px(9)}
              color={sceneLook.handle}
              onPointerDown={startDrag(i, false, p)}
              onDoubleClick={(event) => {
                event.stopPropagation()
                tryShape({ type: 'remove-point', id: element.id, index: i })
              }}
            />
            {!shapeDrag && <Handle point={middle} radius={px(7)} color="#ffffff" onPointerDown={startDrag(i + 1, true, middle)} />}
            <Html position={toVector(labelAt)} center style={{ pointerEvents: 'none' }}>
              <span className="edge-length">{meters.format(lengths[i])} m</span>
            </Html>
          </group>
        )
      })}
    </>
  )
}

/**
 * A camera straight above the plot, north up, zoomed so the whole plot fits with a margin. The framing is taken once
 * when the top view opens, so the view does not jump while the customer drags corners.
 */
export function TopCamera() {
  const size = useThree((s) => s.size)
  const [frame] = useState(() => {
    const plot = useSceneStore.getState().document.elements.find((e) => e.kind === 'plot')
    const points = plot?.kind === 'plot' ? plot.points : [{ x: -10, y: -10 }, { x: 10, y: 10 }]
    const xs = points.map((p) => p.x)
    const ys = points.map((p) => p.y)
    return { minX: Math.min(...xs), maxX: Math.max(...xs), minY: Math.min(...ys), maxY: Math.max(...ys) }
  })
  const zoom = Math.min(size.width / ((frame.maxX - frame.minX) * 1.25 || 1), size.height / ((frame.maxY - frame.minY) * 1.25 || 1))
  return (
    <OrthographicCamera
      makeDefault
      position={[(frame.minX + frame.maxX) / 2, 100, -(frame.minY + frame.maxY) / 2]}
      zoom={zoom}
      up={[0, 0, -1]}
      near={0.1}
      far={500}
    />
  )
}
