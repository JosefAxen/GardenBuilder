import type { Tool } from '../sceneStore'

/** Shaping, redrawing and pinning a photo work only from above, so the view cannot be switched meanwhile. */
export function viewLocked(mode: Tool['mode']): boolean {
  return mode === 'shape' || mode === 'draw' || mode === 'pin'
}
