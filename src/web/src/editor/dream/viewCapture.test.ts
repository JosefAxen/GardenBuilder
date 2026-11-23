import { describe, expect, it } from 'vitest'
import { cameraToView, currentView, viewToCamera } from './viewCapture'

describe('view capture', () => {
  it('turns a three.js camera into a scene view', () => {
    const view = cameraToView([10, 1.7, 9], [10, 1, -8], 50, 'Från altanen')

    expect(view.name).toBe('Från altanen')
    expect(view.eye).toEqual({ x: 10, y: -9, height: 1.7 })
    expect(view.target).toEqual({ x: 10, y: 8, height: 1 })
    expect(view.fieldOfView).toBe(50)
    expect(view.id).toMatch(/^[0-9a-f-]{36}$/)
  })

  it('round-trips through viewToCamera', () => {
    const view = cameraToView([3, 12.5, -4], [8, 0, -12], 45, 'Uppifrån')

    const camera = viewToCamera(view)

    expect(camera.position).toEqual([3, 12.5, -4])
    expect(camera.target).toEqual([8, 0, -12])
    expect(camera.fov).toBe(45)
  })

  it('a camera looking towards -Z looks north', () => {
    const view = cameraToView([0, 2, 0], [0, 2, -10], 50, 'Norrut')

    expect(view.target.y).toBeGreaterThan(view.eye.y)
    expect(view.target.x).toBe(view.eye.x)
  })

  it('keeps the eye height and field of view within what the server accepts', () => {
    const view = cameraToView([0, 0.1, 0], [0, -1, -10], 120, 'Marken')

    expect(view.eye.height).toBe(0.5)
    expect(view.target.height).toBe(0)
    expect(view.fieldOfView).toBe(90)
    expect(cameraToView([0, 80, 0], [0, 0, -1], 10, 'Högt').eye.height).toBe(30)
    expect(cameraToView([0, 80, 0], [0, 0, -1], 10, 'Högt').fieldOfView).toBe(20)
  })

  it('has no current view while no scene is mounted', () => {
    expect(currentView('Vy')).toBeNull()
  })
})
