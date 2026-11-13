import {
  BoxGeometry,
  type BufferGeometry,
  ConeGeometry,
  CylinderGeometry,
  DodecahedronGeometry,
  IcosahedronGeometry,
} from 'three'
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js'
import type { ModelKind, PointArticle } from './demoCatalog'

/**
 * PROTOTYPE: type models generated in code, until the Quaternius GLB files are in place. Each model is a few parts,
 * and each part is drawn as one InstancedMesh for every element that uses it. "high" detail roughly matches the
 * triangle count of a stylized GLB plant (1–3k triangles), so the performance measurement is honest.
 */
export type ModelPart = {
  key: string
  geometry: BufferGeometry
  /** 'primary' and 'accent' take the article's colours; anything else is a fixed colour. */
  color: 'primary' | 'accent' | (string & {})
  /** Leave the part out for articles where it does not apply, for example blooms on a boxwood. */
  when?: (article: PointArticle) => boolean
}

const BARK = '#7a5a3a'
const hasAccent = (a: PointArticle) => a.accent !== undefined

function at(geometry: BufferGeometry, x: number, y: number, z = 0, scaleY = 1) {
  geometry.scale(1, scaleY, 1)
  geometry.translate(x, y, z)
  return geometry
}

export function buildModels(detail: 'low' | 'high'): Record<ModelKind, ModelPart[]> {
  const ico = detail === 'high' ? 3 : 0
  const radial = detail === 'high' ? 16 : 6

  return {
    deciduous: [
      { key: 'trunk', color: BARK, geometry: at(new CylinderGeometry(0.12, 0.18, 1.6, radial), 0, 0.8) },
      {
        key: 'crown',
        color: 'primary',
        geometry: mergeGeometries([
          at(new IcosahedronGeometry(1.1, ico), 0, 2.4),
          at(new IcosahedronGeometry(0.7, ico), 0.6, 2.0, 0.3),
        ]),
      },
    ],
    conifer: [
      { key: 'trunk', color: BARK, geometry: at(new CylinderGeometry(0.1, 0.14, 0.6, radial), 0, 0.3) },
      { key: 'crown', color: 'primary', geometry: at(new ConeGeometry(0.9, 2.8, radial + 1, detail === 'high' ? 6 : 1), 0, 1.9) },
    ],
    bush: [
      { key: 'leaves', color: 'primary', geometry: at(detail === 'high' ? new IcosahedronGeometry(0.6, 3) : new DodecahedronGeometry(0.6, 0), 0, 0.45, 0, 0.8) },
      { key: 'blooms', color: 'accent', when: hasAccent, geometry: mergeGeometries([
        at(new IcosahedronGeometry(0.18, ico > 0 ? 1 : 0), 0.3, 0.8, 0.1),
        at(new IcosahedronGeometry(0.15, ico > 0 ? 1 : 0), -0.25, 0.75, -0.2),
        at(new IcosahedronGeometry(0.16, ico > 0 ? 1 : 0), 0, 0.85, -0.35),
      ]) },
    ],
    perennial: [
      { key: 'leaves', color: 'primary', geometry: at(new IcosahedronGeometry(0.3, ico > 0 ? 2 : 0), 0, 0.15, 0, 0.6) },
      { key: 'blooms', color: 'accent', when: hasAccent, geometry: mergeGeometries([
        at(new ConeGeometry(0.08, 0.3, 5), 0, 0.45),
        at(new ConeGeometry(0.07, 0.25, 5), 0.15, 0.38, 0.05),
        at(new ConeGeometry(0.07, 0.25, 5), -0.12, 0.38, -0.08),
      ]) },
    ],
    pot: [
      { key: 'pot', color: 'primary', geometry: at(new CylinderGeometry(0.3, 0.22, 0.5, radial + 4), 0, 0.25) },
      { key: 'plant', color: 'accent', when: hasAccent, geometry: at(new IcosahedronGeometry(0.32, ico > 0 ? 2 : 0), 0, 0.68) },
    ],
    bench: [
      {
        key: 'wood',
        color: 'primary',
        geometry: mergeGeometries([
          at(new BoxGeometry(1.5, 0.08, 0.45), 0, 0.45),
          at(new BoxGeometry(1.5, 0.4, 0.06), 0, 0.75, -0.2),
          at(new BoxGeometry(0.08, 0.45, 0.4), 0.65, 0.22),
          at(new BoxGeometry(0.08, 0.45, 0.4), -0.65, 0.22),
        ]),
      },
    ],
  }
}
