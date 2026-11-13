import { OrbitControls } from '@react-three/drei'
import { Canvas } from '@react-three/fiber'
import { useMemo } from 'react'
import { Shape, Vector2 } from 'three'
import type { BuildingElement, SceneElement, Vec2 } from '../api/scene'
import { useSceneStore } from './sceneStore'

// Scene coordinates are (x = east, y = north). In three.js the ground is the XZ plane with -Z as north.
const toShape = (points: Vec2[]) => new Shape(points.map((p) => new Vector2(p.x, p.y)))

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
    <mesh rotation={[-Math.PI / 2, 0, 0]} castShadow>
      <extrudeGeometry args={[shape, { depth: element.height, bevelEnabled: false }]} />
      <meshStandardMaterial color="#e8e2d6" />
    </mesh>
  )
}

function Tree({ position }: { position: Vec2 }) {
  return (
    <group position={[position.x, 0, -position.y]}>
      <mesh position={[0, 0.75, 0]} castShadow>
        <cylinderGeometry args={[0.15, 0.2, 1.5]} />
        <meshStandardMaterial color="#7a5a3a" />
      </mesh>
      <mesh position={[0, 2.4, 0]} castShadow>
        <icosahedronGeometry args={[1.2, 0]} />
        <meshStandardMaterial color="#4f8a3c" flatShading />
      </mesh>
    </group>
  )
}

function SceneElementView({ element }: { element: SceneElement }) {
  switch (element.kind) {
    case 'plot':
      return <FlatPolygon points={element.points} color="#b9c9a3" elevation={0} />
    case 'surface':
      return <FlatPolygon points={element.points} color="#7fb069" elevation={0.01} />
    case 'building':
      return <Building element={element} />
    case 'point':
      return <Tree position={element.position} />
    case 'line':
      return null // Hedges, fences and edging are generated in code later.
  }
}

export function SceneCanvas() {
  const elements = useSceneStore((s) => s.document.elements)
  return (
    <Canvas shadows camera={{ position: [18, 16, 18], fov: 45 }}>
      <color attach="background" args={['#dfe9f2']} />
      <ambientLight intensity={0.6} />
      <directionalLight position={[10, 20, 5]} intensity={1.2} castShadow />
      {elements.map((element) => (
        <SceneElementView key={element.id} element={element} />
      ))}
      <OrbitControls makeDefault maxPolarAngle={Math.PI / 2.1} />
    </Canvas>
  )
}
