// Turns the Quaternius sources in assets/models/quaternius into one GLB per form in src/editor/models, as
// assets/models/manifest.json describes (docs/superpowers/specs/2026-11-17-model-library-design.md).
//
//   npm run build:models [-- <folder with the source .gltf files>]
//
// Each GLB stands on y = 0, centred on x/z, scaled to the form's nominal height, with textures at most 512 px as WebP.
// Material names are kept: the editor finds leaves and flowers by them.
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { NodeIO } from '@gltf-transform/core'
import { ALL_EXTENSIONS, EXTTextureWebP } from '@gltf-transform/extensions'
import { dedup, flatten, getBounds, join as joinMeshes, prune, textureCompress, transformMesh, weld } from '@gltf-transform/functions'
import { Matrix4 } from 'three'
import sharp from 'sharp'

const repo = fileURLToPath(new URL('../../../', import.meta.url))
const sourceDir = process.argv[2] ?? join(repo, 'assets/models/quaternius')
const outDir = fileURLToPath(new URL('../src/editor/models/', import.meta.url))
const manifest = JSON.parse(readFileSync(join(repo, 'assets/models/manifest.json'), 'utf8'))

const io = new NodeIO().registerExtensions(ALL_EXTENSIONS)
const generated = {}

for (const form of manifest.filter((f) => f.source !== null)) {
  const document = await io.read(join(sourceDir, `${form.source}.gltf`))
  const root = document.getRoot()

  // Normal maps add size and little at this scale.
  for (const material of root.listMaterials()) material.setNormalTexture(null)

  if (form.leafTexture) {
    const image = readFileSync(join(sourceDir, form.leafTexture))
    for (const material of root.listMaterials().filter((m) => /leaf|leaves/i.test(m.getName()))) {
      material.getBaseColorTexture()?.setImage(image).setMimeType('image/png').setURI(form.leafTexture)
    }
  }

  await document.transform(flatten(), dedup(), prune(), weld())

  // Narrower or wider first, then scale to the nominal height, then stand it on the ground, centred.
  const scene = root.listScenes()[0]
  const factor = form.widthFactor ?? 1
  const meshes = root.listNodes().filter((n) => n.getMesh())
  const apply = (matrix) => {
    for (const node of meshes) transformMesh(node.getMesh(), matrix)
  }
  for (const node of meshes) {
    // Bake each node's own transform into its mesh, so the bounds below are the drawn bounds.
    apply(new Matrix4().fromArray(node.getWorldMatrix()).toArray())
    node.setMatrix(new Matrix4().toArray())
  }
  apply(new Matrix4().makeScale(factor, 1, factor).toArray())
  let { min, max } = getBounds(scene)
  const k = form.nominalHeight / (max[1] - min[1])
  apply(new Matrix4().makeScale(k, k, k).toArray())
  ;({ min, max } = getBounds(scene))
  apply(new Matrix4().makeTranslation(-(min[0] + max[0]) / 2, -min[1], -(min[2] + max[2]) / 2).toArray())

  await document.transform(
    joinMeshes(),
    textureCompress({ encoder: sharp, targetFormat: 'webp', resize: [512, 512] }),
    prune(),
  )
  document.createExtension(EXTTextureWebP).setRequired(true)

  const path = join(outDir, `${form.key}.glb`)
  await io.write(path, document)

  ;({ min, max } = getBounds(scene))
  let triangles = 0
  for (const mesh of root.listMeshes()) {
    for (const primitive of mesh.listPrimitives()) {
      const indices = primitive.getIndices()
      triangles += (indices ? indices.getCount() : primitive.getAttribute('POSITION').getCount()) / 3
    }
  }
  const round = (n) => Math.round(n * 1000) / 1000
  generated[form.key] = { height: round(max[1] - min[1]), width: round(Math.max(max[0] - min[0], max[2] - min[2])), triangles }
  console.log(`${form.key.padEnd(18)} ${String(triangles).padStart(6)} tri  ${readFileSync(path).length.toLocaleString('sv-SE').padStart(9)} B`)
}

writeFileSync(join(outDir, 'library.generated.json'), JSON.stringify(generated, null, 2) + '\n')
