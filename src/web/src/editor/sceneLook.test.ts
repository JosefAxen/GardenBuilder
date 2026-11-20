import { PCFShadowMap } from 'three'
import { describe, expect, it } from 'vitest'
import { palette } from '../ui/palette'
import { sceneLook } from './sceneLook'

describe('the Plantskolan scene look', () => {
  it('uses the warm ground and terracotta selection from the palette', () => {
    expect(sceneLook.background).toBe(palette.sceneTop)
    expect(sceneLook.selection).toBe(palette.terra)
  })

  it('the sun is up in the south-west (three.js: west is -x, south is +z)', () => {
    const [x, y, z] = sceneLook.sun.position
    expect(y).toBeGreaterThan(0)
    expect(x).toBeLessThan(0)
    expect(z).toBeGreaterThan(0)
  })

  it('uses a shadow type this three.js still has', () => {
    expect(sceneLook.shadowType).toBe(PCFShadowMap)
  })
})
