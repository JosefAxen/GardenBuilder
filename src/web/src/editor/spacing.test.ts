import { describe, expect, it } from 'vitest'
import { crowdingFootprints } from './spacing'

describe('crowding', () => {
  it('removed plants are left out of the crowding check', () => {
    const footprints = crowdingFootprints([
      { id: 'kept', position: { x: 0, y: 0 }, matureWidth: 2, removed: false },
      { id: 'gone', position: { x: 0, y: 0 }, matureWidth: 2, removed: true },
      { id: 'none', position: { x: 5, y: 5 }, matureWidth: null, removed: false },
    ])
    expect(footprints.map((f) => f.id)).toEqual(['kept'])
  })
})
