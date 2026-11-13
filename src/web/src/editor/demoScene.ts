import type { PointElement, SceneDocument, SceneElement } from '../api/scene'
import { pointArticles } from './demoCatalog'

/** PROTOTYPE: a starting scene until designs are loaded from the server. */
export const demoScene: SceneDocument = {
  formatVersion: 1,
  measurementSource: 'estimated',
  elements: [
    { id: 'plot', kind: 'plot', status: 'existing-keep', points: [{ x: -12, y: -9 }, { x: 12, y: -9 }, { x: 12, y: 9 }, { x: -12, y: 9 }] },
    { id: 'house', kind: 'building', status: 'existing-keep', height: 5, roof: 'gable', points: [{ x: -5, y: 2 }, { x: 5, y: 2 }, { x: 5, y: 8 }, { x: -5, y: 8 }] },
    { id: 'lawn', kind: 'surface', status: 'new', material: 'grass', points: [{ x: -11, y: -8 }, { x: 11, y: -8 }, { x: 11, y: 1 }, { x: -11, y: 1 }] },
    { id: 'deck', kind: 'surface', status: 'new', material: 'decking', points: [{ x: -4, y: -1 }, { x: 4, y: -1 }, { x: 4, y: 2 }, { x: -4, y: 2 }] },
    { id: 'bed', kind: 'surface', status: 'new', material: 'bed', points: [{ x: -11, y: 3 }, { x: -7, y: 3 }, { x: -7, y: 8 }, { x: -11, y: 8 }] },
    { id: 'hedge-south', kind: 'line', status: 'new', articleId: 'hedge-privet', points: [{ x: -11.5, y: -8.6 }, { x: 11.5, y: -8.6 }] },
    { id: 'fence-east', kind: 'line', status: 'existing-keep', articleId: 'fence-wood', points: [{ x: 11.7, y: -8.6 }, { x: 11.7, y: 8.6 }] },
    { id: 'tree-1', kind: 'point', status: 'new', articleId: 'tree-apple', position: { x: -8, y: -5 }, rotation: 0 },
    { id: 'tree-2', kind: 'point', status: 'existing-keep', articleId: 'tree-birch', position: { x: 7, y: -4 }, rotation: 0 },
    { id: 'lav-1', kind: 'point', status: 'new', articleId: 'perennial-lavender', position: { x: -10, y: 4 }, rotation: 0 },
    { id: 'lav-2', kind: 'point', status: 'new', articleId: 'perennial-lavender', position: { x: -9.4, y: 4.2 }, rotation: 0 },
    { id: 'hosta-1', kind: 'point', status: 'new', articleId: 'perennial-hosta', position: { x: -8, y: 6.5 }, rotation: 0 },
    { id: 'pot-1', kind: 'point', status: 'new', articleId: 'pot-terracotta', position: { x: 3.4, y: -0.5 }, rotation: 0 },
    { id: 'bench-1', kind: 'point', status: 'new', articleId: 'bench-wood', position: { x: -2, y: 0 }, rotation: 0 },
  ],
}

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

/** PROTOTYPE: 300 point elements spread over the lawn, for the performance target in docs/design.md. */
export function stressElements(count: number, seed = 42): SceneElement[] {
  const random = mulberry32(seed)
  return Array.from({ length: count }, (_, i): PointElement => {
    const article = pointArticles[Math.floor(random() * pointArticles.length)]
    return {
      id: `stress-${seed}-${i}`,
      kind: 'point',
      status: 'new',
      articleId: article.id,
      position: { x: -11 + random() * 22, y: -8 + random() * 8.5 },
      rotation: random() * Math.PI * 2,
    }
  })
}
