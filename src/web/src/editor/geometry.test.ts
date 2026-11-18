import { describe, expect, it } from 'vitest'
import { distanceToPolygon, isSelfIntersecting, segmentLengths } from './geometry'

const square = [
  { x: 0, y: 0 },
  { x: 10, y: 0 },
  { x: 10, y: 10 },
  { x: 0, y: 10 },
]

describe('geometry', () => {
  it('a bowtie crosses itself, a square does not', () => {
    const bowtie = [
      { x: 0, y: 0 },
      { x: 10, y: 10 },
      { x: 10, y: 0 },
      { x: 0, y: 10 },
    ]
    expect(isSelfIntersecting(bowtie)).toBe(true)
    expect(isSelfIntersecting(square)).toBe(false)
  })

  it('segment lengths of a 3 × 4 rectangle', () => {
    const rectangle = [
      { x: 0, y: 0 },
      { x: 3, y: 0 },
      { x: 3, y: 4 },
      { x: 0, y: 4 },
    ]
    expect(segmentLengths(rectangle)).toEqual([3, 4, 3, 4])
  })

  it('distance is zero inside and to the nearest edge outside', () => {
    expect(distanceToPolygon({ x: 5, y: 5 }, square)).toBe(0)
    expect(distanceToPolygon({ x: 12, y: 5 }, square)).toBeCloseTo(2)
    expect(distanceToPolygon({ x: 13, y: 14 }, square)).toBeCloseTo(5)
  })
})
