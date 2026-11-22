import { type Camera, type Object3D, PerspectiveCamera, type Scene, Vector2, type Vector3, type WebGLRenderer } from 'three'
import { newId } from '../../api/ids'
import type { SceneView } from '../../api/scene'

/** The same limits as SceneValidator on the server. */
const MIN_EYE_HEIGHT = 0.5
const MAX_HEIGHT = 30
const MIN_FIELD_OF_VIEW = 20
const MAX_FIELD_OF_VIEW = 90

export const RENDER_WIDTH = 1536
export const RENDER_HEIGHT = 1024

type Triple = [number, number, number]

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value))
const round = (value: number) => Math.round(value * 1000) / 1000

/**
 * A three.js camera as a scene view. three.js has y up and -z north; the scene has x east, y north and a height.
 * Heights and field of view are kept within what the server accepts, so the orbit camera can always be saved.
 */
export function cameraToView(position: Triple, target: Triple, fov: number, name: string): SceneView {
  return {
    id: newId(),
    name,
    eye: { x: round(position[0]), y: round(-position[2]), height: round(clamp(position[1], MIN_EYE_HEIGHT, MAX_HEIGHT)) },
    target: { x: round(target[0]), y: round(-target[2]), height: round(clamp(target[1], 0, MAX_HEIGHT)) },
    fieldOfView: round(clamp(fov, MIN_FIELD_OF_VIEW, MAX_FIELD_OF_VIEW)),
  }
}

/** A scene view as a three.js camera position, target and field of view. */
export function viewToCamera(view: SceneView): { position: Triple; target: Triple; fov: number } {
  return {
    position: [view.eye.x, view.eye.height, -view.eye.y],
    target: [view.target.x, view.target.height, -view.target.y],
    fov: view.fieldOfView,
  }
}

/** The orbit controls as far as this file needs them. */
export type CaptureControls = { target: Vector3; update: () => void }

type Bridge = { gl: WebGLRenderer; scene: Scene; camera: Camera; controls: CaptureControls | null; invalidate: () => void }

let bridge: Bridge | null = null

/** Called by CaptureBridge inside the Canvas; null when the scene unmounts. */
export function registerBridge(next: Bridge | null): void {
  bridge = next
}

const isPerspective = (camera: Camera): camera is PerspectiveCamera => (camera as PerspectiveCamera).isPerspectiveCamera === true

/** What the 3D window shows right now, as a view to save. Null without a scene or in the top view. */
export function currentView(name: string): SceneView | null {
  if (!bridge?.controls || !isPerspective(bridge.camera)) return null
  const { position } = bridge.camera
  const { target } = bridge.controls
  return cameraToView([position.x, position.y, position.z], [target.x, target.y, target.z], bridge.camera.fov, name)
}

/** Moves the orbit camera to a saved view. */
export function showView(view: SceneView): void {
  if (!bridge?.controls || !isPerspective(bridge.camera)) return
  const { position, target, fov } = viewToCamera(view)
  bridge.camera.position.set(...position)
  bridge.camera.fov = fov
  bridge.camera.updateProjectionMatrix()
  bridge.controls.target.set(...target)
  bridge.controls.update()
  bridge.invalidate()
}

/** Objects that help the customer edit (selection, rings, pins) and must not reach the image model. */
function helpers(scene: Scene): Object3D[] {
  const found: Object3D[] = []
  scene.traverse((o) => {
    if (o.userData.helper === true && o.visible) found.push(o)
  })
  return found
}

/**
 * The view drawn as a PNG of 1536 × 1024, without editing helpers. It draws into the editor's own canvas for one
 * moment and puts it back before the browser shows a frame, so the image has exactly the colours the customer sees.
 */
export function renderView(view: SceneView, width = RENDER_WIDTH, height = RENDER_HEIGHT): Promise<Blob> {
  if (!bridge) return Promise.reject(new Error('No scene to render.'))
  const { gl, scene, invalidate } = bridge
  const { position, target, fov } = viewToCamera(view)
  const camera = new PerspectiveCamera(fov, width / height, 0.1, 500)
  camera.position.set(...position)
  camera.lookAt(...target)
  camera.updateMatrixWorld()

  const hidden = helpers(scene)
  const ratio = gl.getPixelRatio()
  const size = gl.getSize(new Vector2())
  return new Promise<Blob>((resolve, reject) => {
    try {
      hidden.forEach((o) => (o.visible = false))
      gl.setPixelRatio(1)
      gl.setSize(width, height, false)
      gl.render(scene, camera)
      // toBlob takes its snapshot now, before the canvas is put back.
      gl.domElement.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('The view could not be drawn.'))), 'image/png')
    } catch (error) {
      reject(error instanceof Error ? error : new Error(String(error)))
    } finally {
      hidden.forEach((o) => (o.visible = true))
      gl.setPixelRatio(ratio)
      gl.setSize(size.x, size.y, false)
      invalidate()
    }
  })
}
