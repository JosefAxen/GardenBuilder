import type { CatalogArticle, TypeModelKey } from '../api/catalog'
import { leafTintNeeded } from './modelColors'
import library from './models/library.generated.json'
import { NOMINAL_HEIGHT, type ModelPart, type PointModelKey } from './models'

/**
 * The forms drawn at a point, and how to draw them (docs/superpowers/specs/2026-11-17-model-library-design.md).
 * The form is only how an article looks: the article is what the element is.
 */
export const POINT_FORMS = [
  'tree-large',
  'tree-fruit',
  'tree-small',
  'pine',
  'spruce',
  'conifer-column',
  'shrub-round',
  'shrub-flowering',
  'perennial-tall',
  'perennial-low',
  'foliage-plant',
  'ornamental-grass',
  'rock',
  'pot',
  'furniture',
] as const satisfies readonly TypeModelKey[]

export type PointFormKey = (typeof POINT_FORMS)[number]

export const isPointForm = (key: string): key is PointFormKey => (POINT_FORMS as readonly string[]).includes(key)

/**
 * The GLB files built from the Quaternius pack by `npm run build:models`. Written out one by one so Vite sees each file
 * and gives it a content-based name. Pots and furniture are made in code.
 */
export const GLB_URLS: Partial<Record<PointFormKey, string>> = {
  'tree-large': new URL('./models/tree-large.glb', import.meta.url).href,
  'tree-fruit': new URL('./models/tree-fruit.glb', import.meta.url).href,
  'tree-small': new URL('./models/tree-small.glb', import.meta.url).href,
  pine: new URL('./models/pine.glb', import.meta.url).href,
  spruce: new URL('./models/spruce.glb', import.meta.url).href,
  'conifer-column': new URL('./models/conifer-column.glb', import.meta.url).href,
  'shrub-round': new URL('./models/shrub-round.glb', import.meta.url).href,
  'shrub-flowering': new URL('./models/shrub-flowering.glb', import.meta.url).href,
  'perennial-tall': new URL('./models/perennial-tall.glb', import.meta.url).href,
  'perennial-low': new URL('./models/perennial-low.glb', import.meta.url).href,
  'foliage-plant': new URL('./models/foliage-plant.glb', import.meta.url).href,
  'ornamental-grass': new URL('./models/ornamental-grass.glb', import.meta.url).href,
  rock: new URL('./models/rock.glb', import.meta.url).href,
}

/** The height in meters of a GLB form at scale 1 (1 m for plants; see assets/models/manifest.json). */
export function nominalHeightOf(key: PointFormKey): number {
  return (library as Record<string, { height: number }>)[key]?.height ?? 1
}

/** The generated shape drawn while a GLB loads, when it fails, and with "Detaljerade modeller" off. */
const FALLBACK: Record<PointFormKey, PointModelKey> = {
  'tree-large': 'deciduous-tree',
  'tree-fruit': 'deciduous-tree',
  'tree-small': 'deciduous-tree',
  pine: 'conifer',
  spruce: 'conifer',
  'conifer-column': 'conifer',
  'shrub-round': 'shrub',
  'shrub-flowering': 'shrub',
  'perennial-tall': 'perennial',
  'perennial-low': 'perennial',
  'foliage-plant': 'perennial',
  'ornamental-grass': 'perennial',
  rock: 'rock',
  pot: 'pot',
  furniture: 'furniture',
}

export const fallbackKey = (key: PointFormKey): PointModelKey => FALLBACK[key]

export const fallbackFor = (key: PointFormKey, models: Record<PointModelKey, ModelPart[]>): ModelPart[] => models[FALLBACK[key]]

export const fallbackNominalHeight = (key: PointFormKey) => NOMINAL_HEIGHT[FALLBACK[key]]

export type GlbRole = 'leaves' | 'leaves-tinted' | 'flowers' | 'fixed'

/** One material of a GLB form, drawn for the articles `when` accepts, multiplied by `tint`. */
export type GlbPart = {
  key: string
  material: string
  role: GlbRole
  when: (article: CatalogArticle) => boolean
  tint: (article: CatalogArticle) => string
}

const WHITE = '#ffffff'
const always = () => true

/**
 * The parts of a GLB form, by its material names. Leaves become two parts, so pack-coloured and tinted articles can
 * each share one InstancedMesh: the tinted part uses a grey copy of the texture.
 */
export function partsFor(materials: string[]): GlbPart[] {
  return materials.flatMap((material): GlbPart[] => {
    if (/flower/i.test(material)) {
      return [{ key: material, material, role: 'flowers', when: (a) => a.accentColor !== null, tint: (a) => a.accentColor ?? WHITE }]
    }
    if (/leaf|leaves|grass/i.test(material)) {
      return [
        { key: material, material, role: 'leaves', when: (a) => !leafTintNeeded(a.color), tint: () => WHITE },
        { key: `${material}-tinted`, material, role: 'leaves-tinted', when: (a) => leafTintNeeded(a.color), tint: (a) => a.color },
      ]
    }
    return [{ key: material, material, role: 'fixed', when: always, tint: () => WHITE }]
  })
}
