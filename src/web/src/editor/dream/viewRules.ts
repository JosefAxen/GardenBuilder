import type { SceneDocument, SceneView } from '../../api/scene'
import { distanceToPolygon } from '../geometry'

/** The same limits as SceneValidator on the server. */
export const MAX_VIEWS = 8
const MAX_NAME = 40
/** How far outside the plot the camera may stand or look, for example from the street. */
const TOLERANCE = 10

/** Why this view cannot be saved, in Swedish, or null when it can. */
export function viewProblem(view: SceneView, document: SceneDocument): string | null {
  if ((document.views ?? []).filter((v) => v.id !== view.id).length >= MAX_VIEWS) return `Du kan spara högst ${MAX_VIEWS} vyer.`
  const name = view.name.trim()
  if (name.length === 0 || name.length > MAX_NAME) return 'Ge vyn ett namn.'
  const plot = document.elements.find((e) => e.kind === 'plot')
  if (plot && [view.eye, view.target].some((p) => distanceToPolygon(p, plot.points) > TOLERANCE)) return 'Ställ kameran närmare trädgården.'
  return null
}
