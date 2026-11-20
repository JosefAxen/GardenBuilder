import { PCFShadowMap, type ShadowMapType } from 'three'
import { palette } from '../ui/palette'

type SceneLook = {
  background: string
  hemisphere: [sky: string, ground: string, intensity: number]
  sun: { color: string; intensity: number; position: [number, number, number] }
  /** The ring under a selected plant, the glow of a selected surface or line. */
  selection: string
  selectedEmissive: string
  /** Lines and handles while shaping the plot or house; the first corner stands out. */
  handle: string
  firstHandle: string
  shadowType: ShadowMapType
}

/**
 * Plantskolan's afternoon light: a warm sky, a low golden sun from the south-west, terracotta for what is selected.
 * Intensities tuned in the browser so the models keep their article colours. Scene coordinates: x east, y north;
 * three.js: x east, z south, so south-west is (-x, +z).
 */
export const sceneLook: SceneLook = {
  background: palette.sceneTop,
  hemisphere: ['#FFF3E0', '#7A6A4F', 1.15],
  sun: { color: '#FFE2B8', intensity: 2.1, position: [-14, 20, 12] },
  selection: palette.terra,
  selectedEmissive: '#5A2410',
  handle: palette.terra,
  firstHandle: palette.fir,
  // PCFSoftShadowMap is gone from this three.js; PCF with a 2048 map is soft enough at this scale.
  shadowType: PCFShadowMap,
}
