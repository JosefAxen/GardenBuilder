import { type ThreeEvent, useThree } from '@react-three/fiber'
import { useLayoutEffect, useMemo, useRef } from 'react'
import { Color, type InstancedMesh, Matrix4, Quaternion, Vector3 } from 'three'
import type { Point2 } from '../api/scene'
import type { CatalogArticle } from '../api/catalog'
import { renderScale } from './sizing'
import { NOMINAL_HEIGHT, type ModelPart, type PointModelKey } from './models'

/** A point element joined with its article, ready to draw. */
export type PlacedPoint = { id: string; position: Point2; rotation: number; article: CatalogArticle; removed: boolean }

const UP = new Vector3(0, 1, 0)
const matrix = new Matrix4()
const quaternion = new Quaternion()
const position = new Vector3()
const scale = new Vector3()
const color = new Color()
const REMOVED = new Color('#b0b0b0')

/** Capacity grows in powers of two, so the InstancedMesh is rebuilt only now and then as elements are added. */
const capacityFor = (count: number) => Math.max(16, 2 ** Math.ceil(Math.log2(Math.max(1, count))))

/**
 * Draws one part of one model kind for every element that uses it, in a single draw call.
 * The matrices are rewritten from the store whenever the elements change.
 */
export function PartInstances({
  part,
  items,
  onPointerDown,
}: {
  part: ModelPart
  items: PlacedPoint[]
  onPointerDown: (id: string, event: ThreeEvent<PointerEvent>) => void
}) {
  const ref = useRef<InstancedMesh>(null)
  const invalidate = useThree((s) => s.invalidate)
  const capacity = capacityFor(items.length)

  useLayoutEffect(() => {
    const mesh = ref.current
    if (!mesh) return
    items.forEach((item, i) => {
      // Scene (x east, y north) to three.js (x, z = -north).
      position.set(item.position.x, 0, -item.position.y)
      quaternion.setFromAxisAngle(UP, item.rotation)
      scale.setScalar(renderScale(item.article, NOMINAL_HEIGHT[item.article.family as PointModelKey]))
      mesh.setMatrixAt(i, matrix.compose(position, quaternion, scale))
      const source = part.color === 'primary' ? item.article.color : part.color === 'accent' ? item.article.accentColor : part.color
      color.set(source ?? '#ffffff')
      // Existing elements that will be removed are drawn faded, so the customer sees what goes away.
      if (item.removed) color.lerp(REMOVED, 0.7)
      mesh.setColorAt(i, color)
    })
    mesh.count = items.length
    mesh.instanceMatrix.needsUpdate = true
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true
    mesh.computeBoundingSphere()
    // The matrices are written straight to the GPU, past React, so ask for a new frame (the scene renders on demand).
    invalidate()
  }, [items, part, capacity, invalidate])

  return (
    <instancedMesh
      key={capacity}
      ref={ref}
      args={[part.geometry, undefined, capacity]}
      castShadow
      receiveShadow
      onPointerDown={(event) => {
        if (event.instanceId === undefined) return
        const item = items[event.instanceId]
        if (item) onPointerDown(item.id, event)
      }}
    >
      <meshStandardMaterial flatShading roughness={0.9} />
    </instancedMesh>
  )
}

/** Rings on the ground that show each plant's mature width; red where plants stand too close. */
export function GrowthRings({ items, crowded }: { items: PlacedPoint[]; crowded: Set<string> }) {
  const ref = useRef<InstancedMesh>(null)
  const invalidate = useThree((s) => s.invalidate)
  const plants = useMemo(() => items.filter((i) => i.article.matureWidth !== null), [items])
  const capacity = capacityFor(plants.length)

  useLayoutEffect(() => {
    const mesh = ref.current
    if (!mesh) return
    plants.forEach((plant, i) => {
      position.set(plant.position.x, 0.03, -plant.position.y)
      quaternion.setFromAxisAngle(new Vector3(1, 0, 0), -Math.PI / 2)
      scale.setScalar((plant.article.matureWidth ?? 0) / 2)
      mesh.setMatrixAt(i, matrix.compose(position, quaternion, scale))
      mesh.setColorAt(i, color.set(crowded.has(plant.id) ? '#d64545' : '#ffffff'))
    })
    mesh.count = plants.length
    mesh.instanceMatrix.needsUpdate = true
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true
    invalidate()
  }, [plants, crowded, capacity, invalidate])

  return (
    <instancedMesh key={capacity} ref={ref} args={[undefined, undefined, capacity]} raycast={() => null}>
      <ringGeometry args={[0.94, 1, 48]} />
      <meshBasicMaterial transparent opacity={0.7} depthWrite={false} />
    </instancedMesh>
  )
}
