import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { contrastRatio } from './contrast'
import { palette } from './palette'

const AA = 4.5

describe('the Plantskolan palette', () => {
  it('measures contrast like WCAG', () => {
    expect(contrastRatio('#ffffff', '#000000')).toBeCloseTo(21, 1)
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 1)
  })

  it.each([
    ['ink', 'linen'],
    ['ink2', 'linen'],
    ['ink3', 'linen'],
    ['ink2', 'linen2'],
    ['ink3', 'linen2'],
    ['onFir', 'fir'],
    ['onFir2', 'fir'],
    ['onFir3', 'fir'],
    ['white', 'terra'],
    ['terraInk', 'terraSoft'],
    ['onFir', 'cut'],
    ['warn', 'linen'],
    ['error', 'linen'],
    ['warn', 'warnSoft'],
    ['error', 'errorSoft'],
  ] as const)('%s on %s holds AA', (text, ground) => {
    expect(contrastRatio(palette[text], palette[ground])).toBeGreaterThanOrEqual(AA)
  })

  it('tokens.css has exactly the palette values', () => {
    const css = readFileSync(fileURLToPath(new URL('./tokens.css', import.meta.url)), 'utf8')
    const cssName = (name: string) => '--' + name.replace(/([a-z])([A-Z0-9])/g, '$1-$2').toLowerCase()
    for (const [name, value] of Object.entries(palette)) {
      expect(css, cssName(name)).toContain(`${cssName(name)}: ${value};`)
    }
  })

  it('documents the forbidden pair: onFir3 is only for the darkest fir', () => {
    expect(contrastRatio(palette.onFir3, palette.fir2)).toBeLessThan(AA)
  })
})
