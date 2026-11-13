import { OrbitControls } from '@react-three/drei'
import { Canvas, type ThreeEvent, useFrame } from '@react-three/fiber'
import { useEffect, useMemo, useRef } from 'react'
import { Plane, Shape, Vector2, Vector3 } from 'three'
import type { BuildingElement, SceneElement, Vec2 } from '../api/scene'
import { findArticle, type LineArticle, type ModelKind } from './demoCatalog'
import { LineView } from './LineView'
import { buildModels } from './models'
import { GrowthRings, PartInstances, type PlacedPoint } from './PointInstances'
import { useSceneStore } from './sceneStore'
import { findCrowdedPlants } from './spacing'

// Scene coordinates are (x = east, y = north). In three.js the ground is the XZ plane with -Z as north.
const toShape = (points: Vec2[]) => new Shape(points.map((p) => new Vector2(p.x, p.y)))
const fromGround = (point: Vector3): Vec2 => ({ x: point.x, y: -point.z })
const GROUND = new Plane(new Vector3(0, 1, 0), 0)

const SURFACE_COLORS: Record<string, string> = {
  grass: '#7fb069',
  paving: '#c9c2b5',
  gravel: '#d8d0bf',
  decking: '#a77b52',
  bed: '#6b4f3a',
}

function FlatPolygon({ points, color, elevation }: { points: Vec2[]; color: string; elevation: number }) {
  const shape = useMemo(() => toShape(points), [points])
  return (
    <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, elevation, 0]} receiveShadow>
      <shapeGeometry args={[shape]} />
      <meshStandardMaterial color={color} />
    </mesh>
  )
}

function Building({ element }: { element: BuildingElement }) {
  const shape = useMemo(() => toShape(element.points), [element.points])
  return (
    <mesh rotation={[-Math.PI / 2, 0, 0]} castShadow receiveShadow>
      <extrudeGeometry args={[shape, { depth: element.height, bevelEnabled: false }]} />
      <meshStandardMaterial color="#e8e2d6" />
    </mesh>
  )
}

/** Polygons and buildings: few, rarely changed, drawn as plain meshes. */
function StaticElement({ element, index }: { element: SceneElement; index: number }) {
  switch (element.kind) {
    case 'plot':
      return <FlatPolygon points={element.points} color="#b9c9a3" elevation={0} />
    case 'surface':
      // Later surfaces lie a little higher so overlapping polygons do not flicker.
      return <FlatPolygon points={element.points} color={SURFACE_COLORS[element.material] ?? '#999999'} elevation={0.01 + index * 0.002} />
    case 'building':
      return <Building element={element} />
    default:
      return null
  }
}

/** Measures frame rate, draw calls and triangles about once a second. */
function PerfMeter() {
  const setPerf = useSceneStore((s) => s.setPerf)
  const window = useRef({ frames: 0, start: 0 })
  useFrame(({ gl }) => {
    const w = window.current
    const now = performance.now()
    if (w.start === 0) w.start = now
    w.frames++
    if (now - w.start >= 1000) {
      // gl.info still holds the previous frame here, which is what we want.
      setPerf({ fps: Math.round((w.frames * 1000) / (now - w.start)), drawCalls: gl.info.render.calls, triangles: gl.info.render.triangles })
      w.frames = 0
      w.start = now
    }
  })
  return null
}

function SelectionMarker({ point }: { point: PlacedPoint }) {
  const radius = Math.max(0.5, (point.article.matureWidth ?? 1) / 2 + 0.15)
  return (
    <mesh position={[point.position.x, 0.04, -point.position.y]} rotation={[-Math.PI / 2, 0, 0]} raycast={() => null}>
      <ringGeometry args={[radius - 0.08, radius, 48]} />
      <meshBasicMaterial color="#ffd23f" />
    </mesh>
  )
}

function Scene() {
  const elements = useSceneStore((s) => s.document.elements)
  const drag = useSceneStore((s) => s.drag)
  const tool = useSceneStore((s) => s.tool)
  const hover = useSceneStore((s) => s.hover)
  const selectedId = useSceneStore((s) => s.selectedId)
  const detail = useSceneStore((s) => s.detail)
  const { execute, select, setTool, setDrag, setHover } = useSceneStore.getState()

  const models = useMemo(() => buildModels(detail), [detail])
  useEffect(() => () => Object.values(models).flat().forEach((p) => p.geometry.dispose()), [models])

  const placed = useMemo(
    () =>
      elements.flatMap((e): PlacedPoint[] => {
        if (e.kind !== 'point') return []
        const article = findArticle(e.articleId)
        if (article?.geometry !== 'point') return []
        const position = drag?.id === e.id ? drag.position : e.position
        return [{ id: e.id, position, rotation: e.rotation, article, removed: e.status === 'existing-remove' }]
      }),
    [elements, drag],
  )

  const crowded = useMemo(
    () => findCrowdedPlants(placed.flatMap((p) => (p.article.matureWidth ? [{ id: p.id, position: p.position, matureWidth: p.article.matureWidth }] : []))),
    [placed],
  )

  const parts = useMemo(
    () =>
      (Object.entries(models) as [ModelKind, (typeof models)[ModelKind]][]).flatMap(([kind, modelParts]) =>
        modelParts.map((part) => ({
          key: `${kind}-${part.key}`,
          part,
          items: placed.filter((p) => p.article.model === kind && (part.when?.(p.article) ?? true)),
        })),
      ),
    [models, placed],
  )

  // The drop is one 'move' command, so a whole drag undoes in one step.
  useEffect(() => {
    const onPointerUp = () => {
      const current = useSceneStore.getState().drag
      if (!current) return
      if (current.moved) execute({ type: 'move', id: current.id, position: current.position })
      setDrag(null)
    }
    window.addEventListener('pointerup', onPointerUp)
    return () => window.removeEventListener('pointerup', onPointerUp)
  }, [execute, setDrag])

  const startDrag = (id: string, event: ThreeEvent<PointerEvent>) => {
    if (useSceneStore.getState().tool.mode !== 'select' || event.button !== 0) return
    event.stopPropagation()
    select(id)
    const point = placed.find((p) => p.id === id)
    const hit = event.ray.intersectPlane(GROUND, new Vector3())
    if (!point || !hit) return
    const ground = fromGround(hit)
    setDrag({ id, position: point.position, offset: { x: ground.x - point.position.x, y: ground.y - point.position.y }, moved: false })
  }

  const onGroundMove = (event: ThreeEvent<PointerEvent>) => {
    const ground = fromGround(event.point)
    const current = useSceneStore.getState().drag
    if (current) setDrag({ ...current, position: { x: ground.x - current.offset.x, y: ground.y - current.offset.y }, moved: true })
    else if (tool.mode !== 'select') setHover(ground)
  }

  const onGroundClick = (event: ThreeEvent<MouseEvent>) => {
    if (event.delta > 4) return // The camera was dragged, not clicked.
    const ground = fromGround(event.point)
    switch (tool.mode) {
      case 'place':
        execute({ type: 'add', element: { id: crypto.randomUUID(), kind: 'point', status: 'new', articleId: tool.articleId, position: ground, rotation: 0 } })
        break
      case 'line':
        if (!tool.start) setTool({ ...tool, start: ground })
        else {
          execute({ type: 'add', element: { id: crypto.randomUUID(), kind: 'line', status: 'new', articleId: tool.articleId, points: [tool.start, ground] } })
          setTool({ mode: 'line', articleId: tool.articleId })
        }
        break
      case 'select':
        // Only the nearest hit counts: a click on a plant must not also deselect it through the ground.
        if (event.intersections[0]?.eventObject === event.eventObject) select(null)
        break
    }
  }

  const selectedPoint = placed.find((p) => p.id === selectedId)
  const placeArticle = tool.mode !== 'select' ? findArticle(tool.articleId) : undefined
  const ghost: PlacedPoint | null =
    tool.mode === 'place' && hover && placeArticle?.geometry === 'point'
      ? { id: 'ghost', position: hover, rotation: 0, article: placeArticle, removed: false }
      : null

  return (
    <>
      <mesh rotation={[-Math.PI / 2, 0, 0]} onPointerMove={onGroundMove} onClick={onGroundClick}>
        <planeGeometry args={[400, 400]} />
        <meshBasicMaterial visible={false} />
      </mesh>

      {elements.map((element, index) => (
        <StaticElement key={element.id} element={element} index={index} />
      ))}

      {elements.map((element) => {
        if (element.kind !== 'line') return null
        const article = findArticle(element.articleId)
        if (article?.geometry !== 'line') return null
        return (
          <LineView
            key={element.id}
            points={element.points}
            article={article}
            selected={element.id === selectedId}
            onPointerDown={(event) => {
              if (useSceneStore.getState().tool.mode !== 'select') return
              event.stopPropagation()
              select(element.id)
            }}
          />
        )
      })}

      {parts.map(({ key, part, items }) => (
        <PartInstances key={key} part={part} items={items} onPointerDown={startDrag} />
      ))}
      <GrowthRings items={placed} crowded={crowded} />
      {selectedPoint && <SelectionMarker point={selectedPoint} />}

      {ghost && <GrowthRings items={[ghost]} crowded={new Set()} />}
      {ghost &&
        models[ghost.article.model].map((part) =>
          part.when?.(ghost.article) === false ? null : (
            <PartInstances key={`ghost-${part.key}`} part={part} items={[ghost]} onPointerDown={() => {}} />
          ),
        )}
      {tool.mode === 'line' && tool.start && hover && placeArticle?.geometry === 'line' && (
        <LineView points={[tool.start, hover]} article={placeArticle as LineArticle} ghost />
      )}

      <OrbitControls makeDefault enabled={drag === null} maxPolarAngle={Math.PI / 2.1} />
      <PerfMeter />
    </>
  )
}

export function SceneCanvas() {
  return (
    <Canvas shadows dpr={[1, 2]} camera={{ position: [18, 16, 18], fov: 45 }}>
      <color attach="background" args={['#dfe9f2']} />
      <hemisphereLight args={['#eaf3ff', '#6b7a55', 0.8]} />
      <directionalLight
        position={[12, 25, 8]}
        intensity={1.6}
        castShadow
        shadow-mapSize={[2048, 2048]}
        shadow-camera-left={-20}
        shadow-camera-right={20}
        shadow-camera-top={20}
        shadow-camera-bottom={-20}
        shadow-camera-far={80}
      />
      <Scene />
    </Canvas>
  )
}
