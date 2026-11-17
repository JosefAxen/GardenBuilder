import { existsSync, readFileSync, statSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { NodeIO, type Document } from '@gltf-transform/core'
import { ALL_EXTENSIONS } from '@gltf-transform/extensions'
import { getBounds } from '@gltf-transform/functions'
import { describe, expect, it } from 'vitest'

type ManifestEntry = { key: string; source: string | null; nominalHeight: number; maxTriangles: number }

const here = (path: string) => fileURLToPath(new URL(path, import.meta.url))
const manifest = JSON.parse(readFileSync(here('../../../../../assets/models/manifest.json'), 'utf8')) as ManifestEntry[]
const packForms = manifest.filter((e) => e.source !== null)
const glbPath = (key: string) => here(`./${key}.glb`)
const generated = () =>
  existsSync(here('./library.generated.json'))
    ? (JSON.parse(readFileSync(here('./library.generated.json'), 'utf8')) as Record<string, { height: number }>)
    : {}

const io = new NodeIO().registerExtensions(ALL_EXTENSIONS)

function triangles(document: Document) {
  let count = 0
  for (const mesh of document.getRoot().listMeshes()) {
    for (const primitive of mesh.listPrimitives()) {
      const indices = primitive.getIndices()
      count += (indices ? indices.getCount() : primitive.getAttribute('POSITION')!.getCount()) / 3
    }
  }
  return count
}

describe('model library', () => {
  it('every pack form in the manifest has a GLB and a generated entry', () => {
    const entries = generated()
    for (const form of packForms) {
      expect(existsSync(glbPath(form.key)), `${form.key}.glb`).toBe(true)
      expect(entries[form.key], form.key).toBeDefined()
    }
  })

  it.each(packForms)('$key keeps its triangle cap', async ({ key, maxTriangles }) => {
    const document = await io.read(glbPath(key))
    expect(triangles(document)).toBeLessThanOrEqual(maxTriangles)
  })

  it.each(packForms)('$key stands on the ground at its nominal height', async ({ key, nominalHeight }) => {
    const document = await io.read(glbPath(key))
    const { min, max } = getBounds(document.getRoot().listScenes()[0])
    expect(min[1]).toBeCloseTo(0, 2)
    expect(Math.abs(max[1] - min[1] - nominalHeight) / nominalHeight).toBeLessThan(0.02)
    // Centred on the spot it stands on.
    expect(Math.abs((min[0] + max[0]) / 2)).toBeLessThan(0.02 * nominalHeight)
    expect(Math.abs((min[2] + max[2]) / 2)).toBeLessThan(0.02 * nominalHeight)
  })

  it('the models stay small', () => {
    const sizes = packForms.map((f) => statSync(glbPath(f.key)).size)
    for (const [i, size] of sizes.entries()) expect(size, packForms[i].key).toBeLessThanOrEqual(600_000)
    expect(sizes.reduce((a, b) => a + b, 0)).toBeLessThanOrEqual(4_000_000)
  })
})
