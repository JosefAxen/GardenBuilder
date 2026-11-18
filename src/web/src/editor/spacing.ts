import type { Point2 } from '../api/scene'

export type PlantFootprint = { id: string; position: Point2; matureWidth: number }

/** Two plants are too close when their centres are nearer than this share of the sum of their mature radii. */
const CROWDING_FACTOR = 0.7

/** Returns the ids of plants that stand too close to another plant (docs/design.md, "Editorns tillstånd"). */
export function findCrowdedPlants(plants: PlantFootprint[]): Set<string> {
  const crowded = new Set<string>()
  for (let i = 0; i < plants.length; i++) {
    const a = plants[i]
    for (let j = i + 1; j < plants.length; j++) {
      const b = plants[j]
      const limit = ((a.matureWidth + b.matureWidth) / 2) * CROWDING_FACTOR
      const dx = a.position.x - b.position.x
      const dy = a.position.y - b.position.y
      if (dx * dx + dy * dy < limit * limit) {
        crowded.add(a.id)
        crowded.add(b.id)
      }
    }
  }
  return crowded
}

/** The plants that count in the crowding check: not those that will be removed, and only those with a known width. */
export function crowdingFootprints(
  plants: { id: string; position: Point2; matureWidth: number | null; removed: boolean }[],
): PlantFootprint[] {
  return plants.flatMap((p) => (!p.removed && p.matureWidth ? [{ id: p.id, position: p.position, matureWidth: p.matureWidth }] : []))
}
