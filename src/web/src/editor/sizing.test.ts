import { describe, expect, it } from 'vitest'
import { NOMINAL_HEIGHT } from './models'
import { renderScale } from './sizing'
import { thuja } from './testArticles'

describe('renderScale', () => {
  it('draws a 0.5 m thuja 0.5 m tall', () => {
    expect(renderScale(thuja) * NOMINAL_HEIGHT.conifer).toBeCloseTo(0.5)
  })

  it('draws a bigger size of the same thuja taller', () => {
    expect(renderScale({ ...thuja, height: 1.3 })).toBeGreaterThan(renderScale(thuja))
  })

  it('uses scale for things that are not plants', () => {
    expect(renderScale({ ...thuja, typeModel: 'pot', height: null, scale: 1.2 })).toBe(1.2)
  })
})
