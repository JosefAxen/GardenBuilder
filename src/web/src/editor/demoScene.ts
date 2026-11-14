import type { PointElement, SceneDocument, SceneElement } from '../api/scene'
import { newId } from '../api/ids'
import { pointArticles } from './demoCatalog'
import demoSceneJson from './demoScene.json'

/**
 * PROTOTYPE: a starting scene until designs are loaded from the server. It lives in JSON so the C# tests can read
 * it and check that it is a valid scene (tests/Unit/Design/SceneValidatorTests.cs).
 */
export const demoScene = demoSceneJson as unknown as SceneDocument

/** Small seeded random generator, so the stress scene is the same on every run and measurements compare. */
function mulberry32(seed: number) {
  let a = seed
  return () => {
    a = (a + 0x6d2b79f5) | 0
    let t = Math.imul(a ^ (a >>> 15), 1 | a)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

/** PROTOTYPE: point elements spread over the lawn, for the performance target in docs/design.md. */
export function stressElements(count: number, seed = 42): SceneElement[] {
  const random = mulberry32(seed)
  return Array.from({ length: count }, (): PointElement => {
    const article = pointArticles[Math.floor(random() * pointArticles.length)]
    return {
      id: newId(),
      kind: 'point',
      status: 'new',
      measurementSource: 'estimated',
      articleId: article.id,
      position: { x: -11 + random() * 22, y: -8 + random() * 8.5 },
      rotation: random() * Math.PI * 2,
    }
  })
}
