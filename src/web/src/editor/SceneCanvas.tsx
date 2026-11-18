import { OrbitControls } from '@react-three/drei'
import { Canvas, type ThreeEvent, useFrame } from '@react-three/fiber'
import { useEffect, useMemo, useRef } from 'react'
import { Plane, Shape, Vector2, Vector3 } from 'three'
import { newId } from '../api/ids'
import type { BuildingElement, Point2, SceneElement, SurfaceMaterial } from '../api/scene'
import { findArticle, useCatalogStore } from './catalogStore'
import { LineView } from './LineView'
import { FormInstances } from './GlbInstances'
import { buildModels } from './models'
import { isPointForm, type PointFormKey } from './modelLibrary'
import { placementProblem } from './placement'
import { GrowthRings, type PlacedPoint } from './PointInstances'
import { useSceneStore } from './sceneStore'
import { drawClick, dropShapeDrag } from './shapeCommands'
import { ShapeHandles, TopCamera } from './ShapeHandles'
import { boxCenter } from './geometry'
import { findCrowdedPlants } from './spacing'

// Scene coordinates are (x = east, y = north). In three.js the ground is the XZ plane with -Z as north.
const toShape = (points: Point2[]) => new Shape(points.map((p) => new Vector2(p.x, p.y)))
const fromGround = (point: Vector3): Point2 => ({ x: point.x, y: -point.z })
const GROUND = new Plane(new Vector3(0, 1, 0), 0)
const ignore = () => {}

/** Where the top view's controls look: the middle of the plot. */
function topTargetOf(elements: SceneElement[]): [number, number, number] {
  const plot = elements.find((e) => e.kind === 'plot')
  if (plot?.kind !== 'plot') return [0, 0, 0]
  const center = boxCenter(plot.points)
  return [center.x, 0, -center.y]
}

const SURFACE_COLORS: Record<SurfaceMaterial, string> = {
  grass: '#7fb069',
  paving: '#c9c2b5',
  gravel: '#d8d0bf',
  decking: '#a77b52',
  bed: '#6b4f3a',
}

function FlatPolygon({
  points,
  color,
  elevation,
  selected,
  removed,
  onPointerDown,
}: {
  points: Point2[]
  color: string
  elevation: number
  selected?: boolean
  /** An existing surface that will be removed: drawn faded, like removed plants. */
  removed?: boolean
  onPointerDown?: (event: ThreeEvent<PointerEvent>) => void
}) {
  const shape = useMemo(() => toShape(points), [points])
  return (
    <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, elevation, 0]} receiveShadow onPointerDown={onPointerDown}>
      <shapeGeometry args={[shape]} />
      {/* A new material when the surface is removed or kept: three.js does not rebuild one when transparency changes. */}
      <meshStandardMaterial
        key={removed ? 'removed' : 'kept'}
        color={color}
        emissive={selected ? '#665500' : '#000000'}
        transparent={removed}
        opacity={removed ? 0.35 : 1}
        depthWrite={!removed}
      />
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
  const selected = useSceneStore((s) => s.selectedId === element.id)
  const selectSurface = (event: ThreeEvent<PointerEvent>) => {
    const { tool, select } = useSceneStore.getState()
    if (tool.mode !== 'select' || event.button !== 0) return
    event.stopPropagation()
    select(element.id)
  }

  switch (element.kind) {
    case 'plot':
      return <FlatPolygon points={element.points} color="#b9c9a3" elevation={0} />
    case 'surface':
      // Later surfaces lie a little higher so overlapping polygons do not flicker.
      // A surface takes its article's colour; an existing surface without an article falls back to its material.
      return (
        <FlatPolygon
          points={element.points}
          color={findArticle(element.articleId)?.color ?? SURFACE_COLORS[element.material]}
          elevation={0.01 + index * 0.002}
          selected={selected}
          removed={element.status === 'existing-remove'}
          onPointerDown={selectSurface}
        />
      )
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
  const measuring = useSceneStore((s) => s.measuring)
  const drag = useSceneStore((s) => s.drag)
  const tool = useSceneStore((s) => s.tool)
  const hover = useSceneStore((s) => s.hover)
  const selectedId = useSceneStore((s) => s.selectedId)
  const detail = useSceneStore((s) => s.detail)
  const shapeDrag = useSceneStore((s) => s.shapeDrag)
  const view = useSceneStore((s) => s.view)
  // Taken when the top view opens, like the camera, so the view does not move while corners are dragged.
  const topTarget = useMemo(() => (view === 'top' ? topTargetOf(useSceneStore.getState().document.elements) : undefined), [view])
  // Re-render when the catalog arrives, so elements whose article just loaded are drawn.
  const articles = useCatalogStore((s) => s.articles)
  const { execute, select, setTool, setDrag, setHover, setNotice } = useSceneStore.getState()

  const models = useMemo(() => buildModels(detail), [detail])
  useEffect(() => () => Object.values(models).flat().forEach((p) => p.geometry.dispose()), [models])

  const placed = useMemo(
    () =>
      elements.flatMap((e): PlacedPoint[] => {
        if (e.kind !== 'point') return []
        const article = findArticle(e.articleId, articles)
        // Elements whose article is not in the catalog (not loaded, or discontinued) are skipped, not drawn wrong.
        if (article?.geometry !== 'point' || !isPointForm(article.typeModel)) return []
        const position = drag?.id === e.id ? drag.position : e.position
        return [{ id: e.id, position, rotation: e.rotation, article, removed: e.status === 'existing-remove' }]
      }),
    [elements, drag, articles],
  )

  const crowded = useMemo(
    () => findCrowdedPlants(placed.flatMap((p) => (p.article.matureWidth ? [{ id: p.id, position: p.position, matureWidth: p.article.matureWidth }] : []))),
    [placed],
  )

  const byForm = useMemo(() => {
    const groups = new Map<PointFormKey, PlacedPoint[]>()
    for (const p of placed) {
      const form = p.article.typeModel as PointFormKey
      groups.set(form, [...(groups.get(form) ?? []), p])
    }
    return [...groups]
  }, [placed])

  // The drop is one 'move' command, so a whole drag undoes in one step.
  // A release anywhere drops the element. If the pointer is lost instead (the window loses focus, or the browser
  // cancels the pointer), the drag is cancelled so it can never get stuck with the camera locked.
  useEffect(() => {
    const { dropDrag, cancelDrag, setShapeDrag } = useSceneStore.getState()
    const drop = () => {
      dropDrag()
      dropShapeDrag()
    }
    const cancel = () => {
      cancelDrag()
      setShapeDrag(null)
    }
    window.addEventListener('pointerup', drop)
    window.addEventListener('pointercancel', cancel)
    window.addEventListener('blur', cancel)
    return () => {
      window.removeEventListener('pointerup', drop)
      window.removeEventListener('pointercancel', cancel)
      window.removeEventListener('blur', cancel)
    }
  }, [])

  const startDrag = (id: string, event: ThreeEvent<PointerEvent>) => {
    if (useSceneStore.getState().tool.mode !== 'select' || event.button !== 0) return
    event.stopPropagation()
    select(id)
    const point = placed.find((p) => p.id === id)
    const hit = event.ray.intersectPlane(GROUND, new Vector3())
    // A removed element can be selected (to keep it again) but not moved.
    if (!point || point.removed || !hit) return
    const ground = fromGround(hit)
    setDrag({ id, position: point.position, offset: { x: ground.x - point.position.x, y: ground.y - point.position.y }, moved: false })
  }

  const onGroundMove = (event: ThreeEvent<PointerEvent>) => {
    const ground = fromGround(event.point)
    const { drag: current, shapeDrag: shaping, setShapeDrag } = useSceneStore.getState()
    if (shaping) setShapeDrag({ ...shaping, point: ground })
    else if (current) setDrag({ ...current, position: { x: ground.x - current.offset.x, y: ground.y - current.offset.y }, moved: true })
    else if (tool.mode !== 'select') setHover(ground)
  }

  const onGroundClick = (event: ThreeEvent<MouseEvent>) => {
    if (event.delta > 4) return // The camera was dragged, not clicked.
    const ground = fromGround(event.point)
    switch (tool.mode) {
      case 'place': {
        const problem = placementProblem(useSceneStore.getState().document, [ground])
        if (problem) setNotice(problem)
        else
          execute({
            type: 'add',
            element: { id: newId(), kind: 'point', status: 'new', measurementSource: 'estimated', articleId: tool.articleId, position: ground, rotation: 0 },
          })
        break
      }
      case 'line': {
        const document = useSceneStore.getState().document
        if (!tool.start) {
          const problem = placementProblem(document, [ground])
          if (problem) setNotice(problem)
          else setTool({ ...tool, start: ground })
          break
        }
        const problem = placementProblem(document, [tool.start, ground])
        if (problem) setNotice(problem)
        else {
          execute({
            type: 'add',
            element: { id: newId(), kind: 'line', status: 'new', measurementSource: 'estimated', articleId: tool.articleId, points: [tool.start, ground] },
          })
          setTool({ mode: 'line', articleId: tool.articleId })
        }
        break
      }
      case 'draw':
        drawClick(ground)
        break
      case 'shape':
        break
      case 'select':
        // Only the nearest hit counts: a click on a plant must not also deselect it through the ground.
        if (event.intersections[0]?.eventObject === event.eventObject) select(null)
        break
    }
  }

  const selectedPoint = placed.find((p) => p.id === selectedId)
  const placeArticle = tool.mode === 'place' || tool.mode === 'line' ? findArticle(tool.articleId) : undefined
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
            ghost={element.status === 'existing-remove'}
            onPointerDown={(event) => {
              if (useSceneStore.getState().tool.mode !== 'select') return
              event.stopPropagation()
              select(element.id)
            }}
          />
        )
      })}

      {byForm.map(([form, items]) => (
        <FormInstances key={form} form={form} items={items} models={models} detail={detail} onPointerDown={startDrag} />
      ))}
      <GrowthRings items={placed} crowded={crowded} />
      {selectedPoint && <SelectionMarker point={selectedPoint} />}

      {ghost && <GrowthRings items={[ghost]} crowded={new Set()} />}
      {ghost && isPointForm(ghost.article.typeModel) && (
        <FormInstances form={ghost.article.typeModel} items={[ghost]} models={models} detail={detail} onPointerDown={ignore} />
      )}
      {tool.mode === 'line' && tool.start && hover && placeArticle?.geometry === 'line' && (
        <LineView points={[tool.start, hover]} article={placeArticle} ghost />
      )}

      <ShapeHandles />
      {view === 'top' && <TopCamera />}
      <OrbitControls
        key={view}
        makeDefault
        enabled={drag === null && shapeDrag === null}
        enableRotate={view !== 'top'}
        target={view === 'top' ? topTarget : undefined}
        maxPolarAngle={Math.PI / 2.1}
      />
      {measuring && <PerfMeter />}
    </>
  )
}

export function SceneCanvas() {
  const measuring = useSceneStore((s) => s.measuring)
  return (
    // Draw only when something changes (saves battery on laptops), except while measuring performance.
    <Canvas shadows dpr={[1, 2]} frameloop={measuring ? 'always' : 'demand'} camera={{ position: [18, 16, 18], fov: 45 }}>
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
