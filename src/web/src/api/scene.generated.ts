/* Generated from scene.schema.json by npm run gen:scene. Do not edit. */

export type SceneElement = PlotElement | BuildingElement | SurfaceElement | LineElement | PointElement;
export type ElementStatus = "existing-keep" | "existing-remove" | "new";
export type MeasurementSource = "estimated" | "customer" | "verified";
export type RoofShape = "flat" | "gable";
export type SurfaceMaterial = "grass" | "paving" | "gravel" | "decking" | "bed";

export interface SceneDocument {
  formatVersion: number;
  elements: SceneElement[];
  views?: SceneView[] | null;
}
export interface PlotElement {
  kind: "plot";
  points: Point2[];
  sloped: boolean;
  id: string;
  status: ElementStatus;
  measurementSource: MeasurementSource;
}
export interface Point2 {
  x: number;
  y: number;
}
export interface BuildingElement {
  kind: "building";
  points: Point2[];
  height: number;
  roof: RoofShape;
  wallColor?: string | null;
  roofColor?: string | null;
  id: string;
  status: ElementStatus;
  measurementSource: MeasurementSource;
}
export interface SurfaceElement {
  kind: "surface";
  articleId: string | null;
  points: Point2[];
  material: SurfaceMaterial;
  id: string;
  status: ElementStatus;
  measurementSource: MeasurementSource;
}
export interface LineElement {
  kind: "line";
  articleId: string | null;
  points: Point2[];
  form?: string | null;
  height?: number | null;
  id: string;
  status: ElementStatus;
  measurementSource: MeasurementSource;
}
export interface PointElement {
  kind: "point";
  articleId: string | null;
  position: Point2;
  rotation: number;
  form?: string | null;
  width?: number | null;
  height?: number | null;
  id: string;
  status: ElementStatus;
  measurementSource: MeasurementSource;
}
export interface SceneView {
  id: string;
  name: string;
  eye: Point3;
  target: Point3;
  fieldOfView: number;
}
export interface Point3 {
  x: number;
  y: number;
  height: number;
}
