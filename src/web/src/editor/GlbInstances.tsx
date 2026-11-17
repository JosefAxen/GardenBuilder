import { useGLTF } from '@react-three/drei'
import type { ThreeEvent } from '@react-three/fiber'
import { Component, type ReactNode, Suspense, useMemo } from 'react'
import { CanvasTexture, type Material, type Mesh, type MeshStandardMaterial, ShaderChunk, type Texture } from 'three'
import type { CatalogArticle } from '../api/catalog'
import { greyPixels } from './modelColors'
import { GLB_URLS, fallbackFor, fallbackNominalHeight, itemsFor, nominalHeightOf, partsFor, type PointFormKey } from './modelLibrary'
import type { ModelPart, PointModelKey } from './models'
import { PartInstances, type PlacedPoint } from './PointInstances'

type Props = {
  form: PointFormKey
  items: PlacedPoint[]
  models: Record<PointModelKey, ModelPart[]>
  detail: 'low' | 'high'
  onPointerDown: (id: string, event: ThreeEvent<PointerEvent>) => void
}

/**
 * Draws every element of one form: the GLB model when "Detaljerade modeller" is on and the file has loaded, otherwise
 * the generated shape. A file that fails to load also falls back, so an element is never left undrawn.
 */
export function FormInstances(props: Props) {
  const fallback = <FallbackInstances {...props} />
  const url = GLB_URLS[props.form]
  if (props.detail === 'low' || !url) return fallback
  return (
    <ModelErrorBoundary fallback={fallback}>
      <Suspense fallback={fallback}>
        <GlbInstances {...props} url={url} />
      </Suspense>
    </ModelErrorBoundary>
  )
}

function FallbackInstances({ form, items, models, onPointerDown }: Props) {
  const nominalHeight = fallbackNominalHeight(form)
  return fallbackFor(form, models).map((part) => (
    <PartInstances
      key={part.key}
      geometry={part.geometry}
      colorOf={colorOfPart(part)}
      nominalHeight={nominalHeight}
      items={part.when ? items.filter((i) => part.when!(i.article)) : items}
      onPointerDown={onPointerDown}
    />
  ))
}

const partColors = new WeakMap<ModelPart, (a: CatalogArticle) => string | null>()
/** One function per part, so PartInstances does not rewrite its matrices on every render. */
function colorOfPart(part: ModelPart) {
  let colorOf = partColors.get(part)
  if (!colorOf) {
    colorOf = (a) => (part.color === 'primary' ? a.color : part.color === 'accent' ? a.accentColor : part.color)
    partColors.set(part, colorOf)
  }
  return colorOf
}

function GlbInstances({ form, items, onPointerDown, url }: Props & { url: string }) {
  const { scene } = useGLTF(url)

  const parts = useMemo(() => {
    const meshes: Mesh[] = []
    scene.traverse((o) => {
      if ((o as Mesh).isMesh) meshes.push(o as Mesh)
    })
    const byMaterial = new Map(meshes.map((m) => [(m.material as Material).name, m]))
    return partsFor([...byMaterial.keys()]).map((part) => {
      const mesh = byMaterial.get(part.material)!
      const source = mesh.material as MeshStandardMaterial
      const material = source.clone()
      // The instance colour multiplies the texture, so a tinted part needs a grey texture to take the tint's colour.
      if (part.role !== 'leaves' && part.role !== 'fixed' && source.map) material.map = greyTexture(source.map)
      if (part.role !== 'fixed') keepCardNormals(material)
      return { part, geometry: mesh.geometry, material }
    })
  }, [scene])

  const nominalHeight = nominalHeightOf(form)
  return parts.map(({ part, geometry, material }) => (
    <PartInstances
      key={part.key}
      geometry={geometry}
      material={material}
      colorOf={part.tint}
      nominalHeight={nominalHeight}
      receiveShadow={part.role === 'fixed'}
      items={itemsFor(part, items)}
      onPointerDown={onPointerDown}
    />
  ))
}

/** The fragment shader's normal setup without the flip for back faces. */
const NORMALS_WITHOUT_FLIP = ShaderChunk.normal_fragment_begin.replace('normal *= faceDirection;', '')

/**
 * The pack's leaf and flower cards have normals that point up and out of the crown, whichever way a card faces. three.js
 * flips the normal when it draws a card's back, which turns the cards seen from above almost black, so that flip is
 * left out for these parts.
 */
function keepCardNormals(material: Material) {
  material.onBeforeCompile = (shader) => {
    shader.fragmentShader = shader.fragmentShader.replace('#include <normal_fragment_begin>', NORMALS_WITHOUT_FLIP)
  }
  material.customProgramCacheKey = () => 'keep-card-normals'
}

const greyTextures = new WeakMap<Texture, Texture>()

/** A grey copy of the texture, made once per texture. Falls back to the original if the image cannot be read. */
function greyTexture(texture: Texture): Texture {
  const cached = greyTextures.get(texture)
  if (cached) return cached
  const image = texture.image as CanvasImageSource & { width: number; height: number }
  const canvas = document.createElement('canvas')
  canvas.width = image.width
  canvas.height = image.height
  const context = canvas.getContext('2d')
  if (!context) return texture
  context.drawImage(image, 0, 0)
  const data = context.getImageData(0, 0, canvas.width, canvas.height)
  greyPixels(data.data)
  context.putImageData(data, 0, 0)
  const grey = new CanvasTexture(canvas)
  grey.flipY = texture.flipY
  grey.colorSpace = texture.colorSpace
  grey.wrapS = texture.wrapS
  grey.wrapT = texture.wrapT
  grey.channel = texture.channel
  greyTextures.set(texture, grey)
  return grey
}

class ModelErrorBoundary extends Component<{ fallback: ReactNode; children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  componentDidCatch(error: unknown) {
    console.warn('A 3D model could not be loaded; the simple shape is shown instead.', error)
  }

  render() {
    return this.state.failed ? this.props.fallback : this.props.children
  }
}
