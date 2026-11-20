import { useGLTF } from '@react-three/drei'
import { Canvas, useThree } from '@react-three/fiber'
import { Suspense, useEffect, useMemo, type ReactNode } from 'react'
import { Box3, InstancedMesh, PerspectiveCamera, Vector3 } from 'three'
import type { CatalogArticle } from '../../api/catalog'
import { FormInstances } from '../GlbInstances'
import { GLB_URLS, type PointFormKey } from '../modelLibrary'
import { buildModels } from '../models'
import type { PlacedPoint } from '../PointInstances'
import { finishThumbnail, useThumbnailQueue } from './thumbnails'

/** The picture's size in pixels: twice the card's 150 × 68 slot, for sharp screens. */
const WIDTH = 300
const HEIGHT = 136
const ignore = () => {}

/** Waits (by suspending) until the GLB is loaded, so the capture never catches the stand-in shape. */
function Loaded({ url, children }: { url: string; children: ReactNode }) {
  useGLTF(url)
  return children
}

/** Frames whatever was drawn, renders one frame and hands the picture back. */
function Capture({ article }: { article: CatalogArticle }) {
  const { gl, scene, camera } = useThree()

  useEffect(() => {
    // One frame later, so the instance matrices written in layout effects are in place.
    const frame = requestAnimationFrame(() => {
      try {
        const box = new Box3()
        scene.traverse((o) => {
          if (o instanceof InstancedMesh && o.count > 0) {
            o.computeBoundingBox()
            if (o.boundingBox) box.union(o.boundingBox)
          }
        })
        if (box.isEmpty()) throw new Error('Nothing was drawn for the thumbnail.')
        const size = box.getSize(new Vector3())
        const centre = box.getCenter(new Vector3())
        const perspective = camera as PerspectiveCamera
        // Far enough back that both the height and the width fit, seen slightly from above.
        const fitHeight = size.y / 2 / Math.tan((perspective.fov * Math.PI) / 360)
        const fitWidth = Math.max(size.x, size.z) / 2 / Math.tan((perspective.fov * Math.PI) / 360) / perspective.aspect
        const distance = Math.max(fitHeight, fitWidth) * 1.02 + Math.max(size.x, size.z) / 2
        perspective.position.set(centre.x + distance * 0.55, centre.y + distance * 0.35, centre.z + distance * 0.75)
        perspective.lookAt(centre)
        perspective.updateProjectionMatrix()
        gl.render(scene, camera)
        finishThumbnail({ url: gl.domElement.toDataURL('image/png') })
      } catch (error) {
        finishThumbnail({ error })
      }
    })
    return () => cancelAnimationFrame(frame)
  }, [article, gl, scene, camera])

  return null
}

function Subject({ article }: { article: CatalogArticle }) {
  const models = useMemo(() => buildModels('high'), [])
  useEffect(() => () => Object.values(models).flat().forEach((p) => p.geometry.dispose()), [models])
  const form = article.typeModel as PointFormKey
  const item: PlacedPoint = { id: 'thumbnail', position: { x: 0, y: 0 }, rotation: 0.6, article, removed: false }
  const url = GLB_URLS[form]
  const subject = (
    <>
      <FormInstances form={form} items={[item]} models={models} detail="high" onPointerDown={ignore} />
      <Capture article={article} />
    </>
  )

  return (
    <Suspense fallback={null}>
      {url ? (
        <Loaded url={url}>{subject}</Loaded>
      ) : (
        subject
      )}
    </Suspense>
  )
}

/**
 * A small hidden renderer of its own (not the scene's) that draws catalogue pictures one at a time, in Plantskolan's
 * warm light on a transparent ground. Mounted once in the editor; idle while the queue is empty.
 */
export function ThumbnailStudio() {
  const current = useThumbnailQueue((s) => s.queue[0])
  if (!current) return null

  return (
    <div className="thumbnail-studio" aria-hidden>
      <Canvas
        frameloop="never"
        dpr={1}
        gl={{ preserveDrawingBuffer: true, alpha: true, antialias: true }}
        camera={{ fov: 30, near: 0.05, far: 200 }}
        style={{ width: WIDTH, height: HEIGHT }}
      >
        <hemisphereLight args={['#fff3e0', '#8a7a5c', 1.6]} />
        <directionalLight position={[-4, 8, 6]} intensity={2.2} color="#ffe2b8" />
        <Subject key={current.article.id} article={current.article} />
      </Canvas>
    </div>
  )
}
