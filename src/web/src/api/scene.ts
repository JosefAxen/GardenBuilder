/**
 * The scene document: the system's most important contract (docs/design.md, "Scendokumentet").
 * The types are defined once in C# (src/Contracts/Scenes) and generated from there: npm run gen:scene.
 * Coordinates are meters in a local system where +x is east and +y is north. The ground is flat.
 */
export type {
  BuildingElement,
  ElementStatus,
  LineElement,
  MeasurementSource,
  PlotElement,
  Point2,
  PointElement,
  RoofShape,
  SceneDocument,
  SceneElement,
  SurfaceElement,
  SurfaceMaterial,
} from './scene.generated'
