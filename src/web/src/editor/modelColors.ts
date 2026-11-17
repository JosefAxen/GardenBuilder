/**
 * Colour rules for the Quaternius forms (docs/superpowers/specs/2026-11-17-model-library-design.md): leaves keep the
 * pack's colours unless the article's colour clearly is not green; flowers are always tinted with the accent colour.
 */

/** Hues between these count as green (degrees). Yellow-green such as Spirea Goldflame (76°) falls below. */
const GREEN_FROM = 80
const GREEN_TO = 170
/** Colours greyer than this are not green either, e.g. silver-leaved plants. */
const MIN_SATURATION = 0.15

function hueAndSaturation(color: string): { hue: number; saturation: number } | null {
  const match = /^#([0-9a-f]{6})$/i.exec(color.trim())
  if (!match) return null
  const n = parseInt(match[1], 16)
  const [r, g, b] = [(n >> 16) & 255, (n >> 8) & 255, n & 255].map((v) => v / 255)
  const max = Math.max(r, g, b)
  const min = Math.min(r, g, b)
  const delta = max - min
  const lightness = (max + min) / 2
  const saturation = delta === 0 ? 0 : delta / (1 - Math.abs(2 * lightness - 1))
  let hue = 0
  if (delta !== 0) {
    if (max === r) hue = 60 * (((g - b) / delta) % 6)
    else if (max === g) hue = 60 * ((b - r) / delta + 2)
    else hue = 60 * ((r - g) / delta + 4)
  }
  return { hue: (hue + 360) % 360, saturation }
}

/** True when the article's leaf colour clearly is not green, so the pack's green leaves would show the wrong plant. */
export function leafTintNeeded(color: string): boolean {
  const hs = hueAndSaturation(color)
  if (!hs) return false
  return hs.saturation < MIN_SATURATION || hs.hue < GREEN_FROM || hs.hue > GREEN_TO
}

/** The average luminance of the opaque pixels after greying: light, so a tint gives about the tint's own colour. */
const TARGET_LUMINANCE = 210

/** Turns RGBA pixels grey in place. Fully transparent pixels (cut-out leaf edges) do not count toward the average. */
export function greyPixels(rgba: Uint8ClampedArray): void {
  let sum = 0
  let count = 0
  for (let i = 0; i < rgba.length; i += 4) {
    if (rgba[i + 3] === 0) continue
    sum += 0.299 * rgba[i] + 0.587 * rgba[i + 1] + 0.114 * rgba[i + 2]
    count++
  }
  const k = count === 0 || sum === 0 ? 1 : TARGET_LUMINANCE / (sum / count)
  for (let i = 0; i < rgba.length; i += 4) {
    const v = (0.299 * rgba[i] + 0.587 * rgba[i + 1] + 0.114 * rgba[i + 2]) * k
    rgba[i] = rgba[i + 1] = rgba[i + 2] = v
  }
}
