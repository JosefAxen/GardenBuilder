/**
 * The scene document: the system's most important contract (docs/design.md, "Scenmodellen och 3D-editorn").
 *
 * TEMPORARY: hand-written until the types are defined in C# and generated from there.
 * Coordinates are meters in a local system where +y points north. The ground is flat.
 */
export type Vec2 = { x: number; y: number }

export type MeasurementSource = 'estimated' | 'customer' | 'verified'

export type ElementStatus = 'existing-keep' | 'existing-remove' | 'new'

type ElementBase = {
  id: string
  status: ElementStatus
  /** Catalog article. Required for everything that costs money. */
  articleId?: string
}

export type PlotElement = ElementBase & { kind: 'plot'; points: Vec2[] }
export type BuildingElement = ElementBase & { kind: 'building'; points: Vec2[]; height: number; roof: 'flat' | 'gable' }
export type SurfaceElement = ElementBase & { kind: 'surface'; points: Vec2[]; material: string }
export type LineElement = ElementBase & { kind: 'line'; points: Vec2[] }
export type PointElement = ElementBase & { kind: 'point'; position: Vec2; rotation: number }

export type SceneElement = PlotElement | BuildingElement | SurfaceElement | LineElement | PointElement

export type SceneDocument = {
  formatVersion: 1
  measurementSource: MeasurementSource
  elements: SceneElement[]
}
