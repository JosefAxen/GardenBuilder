import { describe, expect, it } from 'vitest'
import { isPlant, renderScale } from './sizing'
import { thuja } from './testArticles'

describe('renderScale', () => {
  it('draws a 1.3 m plant 1.3 m tall from a 1 m model', () => {
    expect(renderScale({ ...thuja, height: 1.3 }, 1)).toBeCloseTo(1.3)
  })

  it('draws a 0.5 m thuja 0.5 m tall from a 3.3 m model', () => {
    expect(renderScale(thuja, 3.3) * 3.3).toBeCloseTo(0.5)
  })

  it('uses scale for things that are not plants, whatever the model height', () => {
    const pot = { ...thuja, typeModel: 'pot', family: 'pot', height: null, scale: 1.2 } as const
    expect(renderScale(pot, 1)).toBe(1.2)
    expect(renderScale(pot, 0.4)).toBe(1.2)
  })
})

describe('isPlant', () => {
  it('follows the family', () => {
    expect(isPlant(thuja)).toBe(true)
    expect(isPlant({ ...thuja, typeModel: 'rock', family: 'furniture' })).toBe(false)
    expect(isPlant({ ...thuja, typeModel: 'ornamental-grass', family: 'perennial' })).toBe(true)
  })
})
