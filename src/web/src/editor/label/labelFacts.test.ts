import { describe, expect, it } from 'vitest'
import { thuja } from '../testArticles'
import { labelFacts } from './labelFacts'

describe('labelFacts', () => {
  it('gives zone, heights, width and light in that order, in Swedish', () => {
    const facts = labelFacts({ ...thuja, hardinessZone: 5, height: 2.0, matureHeight: 12, matureWidth: 1.5, light: ['sun', 'part-shade'] })

    expect(facts).toEqual([
      { label: 'Växtzon', value: 'I–V' },
      { label: 'Höjd vid plantering', value: '200 cm' },
      { label: 'Fullvuxen höjd', value: '12 m' },
      { label: 'Fullvuxen bredd', value: '1,5 m' },
      { label: 'Läge', value: 'Sol–Halvskugga' },
    ])
  })

  it('leaves out what the article does not have', () => {
    expect(labelFacts({ ...thuja, hardinessZone: null, height: null, matureHeight: null, matureWidth: null, light: [] })).toEqual([])
  })

  it('zone 8 is I–VIII and shade alone is Skugga', () => {
    const facts = labelFacts({ ...thuja, hardinessZone: 8, height: null, matureHeight: null, matureWidth: null, light: ['shade'] })
    expect(facts).toEqual([
      { label: 'Växtzon', value: 'I–VIII' },
      { label: 'Läge', value: 'Skugga' },
    ])
  })
})
