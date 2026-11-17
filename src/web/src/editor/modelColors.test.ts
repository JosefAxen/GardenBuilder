import { describe, expect, it } from 'vitest'
import { greyPixels, leafTintNeeded } from './modelColors'

describe('leafTintNeeded', () => {
  it.each(['#b5452f', '#9fbf45'])('tints leaves that are clearly not green: %s', (color) => {
    expect(leafTintNeeded(color)).toBe(true)
  })

  it.each(['#5f9e3f', '#8dbb4c', '#3f7a3a', '#4c8a3e'])('keeps the pack colours for ordinary green: %s', (color) => {
    expect(leafTintNeeded(color)).toBe(false)
  })

  it('keeps the pack colours when the colour cannot be read', () => {
    expect(leafTintNeeded('grön')).toBe(false)
  })
})

describe('greyPixels', () => {
  const luminance = (px: Uint8ClampedArray, i: number) => 0.299 * px[i] + 0.587 * px[i + 1] + 0.114 * px[i + 2]

  it('turns colours grey', () => {
    const px = new Uint8ClampedArray([255, 0, 0, 255, 0, 255, 0, 255])
    greyPixels(px)
    expect(px[0]).toBe(px[1])
    expect(px[1]).toBe(px[2])
    expect(px[4]).toBe(px[5])
    expect(px[5]).toBe(px[6])
  })

  it('makes the opaque pixels average 210, so a tint gives about its own colour', () => {
    const px = new Uint8ClampedArray([40, 90, 30, 255, 50, 100, 40, 255, 30, 80, 25, 255])
    greyPixels(px)
    const average = (luminance(px, 0) + luminance(px, 4) + luminance(px, 8)) / 3
    expect(average).toBeGreaterThan(209)
    expect(average).toBeLessThan(211)
  })

  it('leaves transparent pixels out of the average', () => {
    const px = new Uint8ClampedArray([100, 100, 100, 255, 255, 255, 255, 0])
    greyPixels(px)
    expect(px[0]).toBeGreaterThanOrEqual(209)
    expect(px[0]).toBeLessThanOrEqual(211)
  })
})
