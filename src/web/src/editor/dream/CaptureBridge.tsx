import { useThree } from '@react-three/fiber'
import { useEffect } from 'react'
import { type CaptureControls, registerBridge } from './viewCapture'

/**
 * Lets the dream panel, which lives outside the Canvas, read the camera and draw a view. Registers the renderer, scene,
 * camera and orbit controls on mount and clears them on unmount.
 */
export function CaptureBridge() {
  const gl = useThree((s) => s.gl)
  const scene = useThree((s) => s.scene)
  const camera = useThree((s) => s.camera)
  const controls = useThree((s) => s.controls) as unknown as CaptureControls | null
  const invalidate = useThree((s) => s.invalidate)

  useEffect(() => {
    registerBridge({ gl, scene, camera, controls: controls ?? null, invalidate })
    return () => registerBridge(null)
  }, [gl, scene, camera, controls, invalidate])

  return null
}
