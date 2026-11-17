# Modellbibliotek Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the generated point shapes in the editor with 15 recognisable forms built from Quaternius' free pack, tinted from the article where the spec says so, without changing prices.

**Architecture:** The Catalog module's type model becomes the form (`conifer-column`), and the old key becomes its family (`conifer`), which pricing and the plant rules use. A Node tool turns the chosen Quaternius glTF sources into one GLB per form inside the web app, where Vite gives them content-based names. The editor loads the GLBs with drei's `useGLTF` and draws one `InstancedMesh` per GLB material. The generated shapes stay as the low-detail and loading fallback.

**Tech Stack:** .NET 10, EF Core and Npgsql, xUnit v3, React 19, React Three Fiber and drei, three 0.187, Vite 8, vitest, glTF Transform (`@gltf-transform/core`, `@gltf-transform/functions`) and sharp for textures.

**Spec:** `docs/superpowers/specs/2026-11-17-model-library-design.md`

## Global Constraints

- **The scene never stores the form.** Elements point at their article, and `formatVersion` is unchanged.
- **Prices are unchanged.** Pricing reads the family, never the form key.
- **Form keys:** `tree-large`, `tree-fruit`, `tree-small`, `pine`, `spruce`, `conifer-column`, `shrub-round`, `shrub-flowering`, `perennial-tall`, `perennial-low`, `foliage-plant`, `ornamental-grass`, `rock`, `pot`, `furniture`. `hedge`, `fence` and `surface-*` are unchanged.
- **Families:**
  - `tree-*` → `deciduous-tree`;
  - `pine`, `spruce`, `conifer-column` → `conifer`;
  - `shrub-*` → `shrub`;
  - `perennial-*`, `foliage-plant`, `ornamental-grass` → `perennial`;
  - `rock`, `furniture` → `furniture`;
  - `pot` → `pot`.
  - Lines and surfaces: the family equals the key.
- **Triangle caps:** 7 000 for `tree-*`, `pine` and `spruce`; 2 000 for every other form.
- **Licence:** CC0 1.0. Source: https://quaternius.com/packs/stylizednaturemegakit.html (free "Standard" zip, July 2024).
- **Leaves:**
  - Pack colours unless the article's `color` clearly is not green.
  - Must tint: Japanese maple `#b5452f` and Spirea Goldflame `#9fbf45`.
  - Must not tint: `#5f9e3f`, `#8dbb4c`, `#3f7a3a`, `#4c8a3e`.
- **Flowers:** always grey and tinted with `accentColor`. With no accent colour the form is drawn without flowers.
- **Bark, stems and rock:** always pack colours.
- **Project rules:**
  - User-facing text in Swedish.
  - Package versions only in `Directory.Packages.props` (.NET) and `package.json` (web).
  - Migrations via `dotnet ef`, applied by the Migrator.

## Review Focus

1. **A catalog row with an old key** (`conifer`) after this lands. The import must refuse it with a Swedish message that lists the new keys, not a generic error. Test in Task 1.
2. **An article that is drawn before its GLB has loaded, or whose GLB fails to load.** It shows the generated fallback shape, never nothing and never a crash. Test in Task 4.
3. **An existing element marked `existing-remove`.** It is still drawn faded, also on GLB parts with pack textures. Test in Task 4.
4. **A flowering form whose article has no accent colour.** It is drawn without its flower part. Test in Task 4.
5. **A database that still holds old enum names** (`Conifer`, `Shrub`) when the API starts. The migration rewrites them, and no row is left that the enum cannot read. Test in Task 1.

---

### Task 1: Forms and families in the catalog and pricing

**Files:**
- Modify:
  - `src/Modules/Catalog/Domain/TypeModel.cs`
  - `src/Modules/Catalog/Domain/ArticleData.cs` (plant rules by family)
  - `src/Modules/Catalog/Features/ImportArticles/ArticleSheet.cs:231,351`
  - `src/Modules/Catalog/Features/ImportArticles/ExampleCatalog.cs`
  - `src/Modules/Catalog/Features/ListArticles/ListArticles.cs`
  - `src/Modules/Catalog/Features/PriceFacts/CatalogPrices.cs`
  - `src/Contracts/Catalog/ICatalogPrices.cs`
  - `src/Modules/Pricing/Domain/PriceCalculator.cs:132-153`
- Create: migration `RenameTypeModelsToForms` in `src/Modules/Catalog/Infrastructure/Migrations`.
- Test:
  - `tests/Unit/Catalog/TypeModelTests.cs`, `ArticleSheetTests.cs`, `ExampleCatalogTests.cs`, `ArticleDataTests.cs`;
  - `tests/Unit/Pricing/PriceCalculatorTests.cs`;
  - `tests/Integration/CatalogListTests.cs`, `CatalogPricesTests.cs`, `EstimatePriceTests.cs`;
  - new `tests/Integration/TypeModelMigrationTests.cs`.

**Interfaces:**
- Produces:
  - Enum members: `TreeLarge, TreeFruit, TreeSmall, Pine, Spruce, ConiferColumn, ShrubRound, ShrubFlowering, PerennialTall, PerennialLow, FoliagePlant, OrnamentalGrass, Rock, Pot, Furniture`, plus the unchanged line and surface members.
  - `TypeModels.Family(TypeModel) : string`, returning the family key.
  - `ArticlePriceFacts(... string TypeModel, string Family, bool Discontinued)`.
  - `GET /api/catalog/articles` items gain `family: string`.

- [ ] **Step 1: Write the failing tests**
  - `TypeModelTests`:
    - `Every_form_has_its_family`: theory over all 15 point keys plus `hedge`, `fence` and `surface-grass`, asserting the family from Global Constraints.
    - `Old_keys_no_longer_parse`: `deciduous-tree`, `conifer`, `shrub` and `perennial` give `TryParse == false`.
    - `IsPlant_follows_the_family`: `rock` false, `conifer-column` true, `foliage-plant` true.
  - `ArticleSheetTests.Old_type_model_lists_the_new_keys`: a row with `conifer` gives an `ImportError` on "Typmodell" whose message contains `conifer-column` and `spruce`.
  - `ExampleCatalogTests.Forms_match_the_design`:
    - `BA-SM-120` → `ConiferColumn`; `TR-AP-150` → `TreeFruit`; `TR-BJ-200` → `TreeLarge`; `TR-LO-060` → `TreeSmall`.
    - `BA-GR-100` → `Spruce`; `BA-TA-030` → `ShrubRound`; `BU-BX-030` → `ShrubRound`.
    - `BU-HO-040`, `BU-SY-060`, `BU-RH-040` and `BU-SP-030` → `ShrubFlowering`.
    - `PE-FU-001` → `FoliagePlant`; `PE-LA-001` → `PerennialTall`.
    - `KR-TE-040` → `Pot`; `MO-BA-150` → `Furniture`.
  - `PriceCalculatorTests.Removal_class_comes_from_the_family`: a removed point whose facts have `TypeModel = "conifer-column", Family = "conifer"` gives the barrträd removal line; `Family = "deciduous-tree"` gives lövträd.
  - `TypeModelMigrationTests.Old_enum_names_are_rewritten` (integration):
    - Migrate to the migration before this one.
    - Insert article rows whose `type_model` is `DeciduousTree`, `Conifer`, `Shrub` and `Perennial` with plain SQL.
    - Migrate to the latest.
    - Read back `TreeLarge`, `Spruce`, `ShrubRound` and `PerennialTall`.

- [ ] **Step 2: Run them and see them fail**
  - Run: `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj` and `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj -- --filter-class "*TypeModelMigrationTests"`.
  - Expected: compile errors for the missing members, then failures.

- [ ] **Step 3: Implement**
  - Give `TypeModels.Facts` a `Family` and rename the members. `IsPlant` is true when the family is `deciduous-tree`, `conifer`, `shrub` or `perennial`, or the key is `hedge`.
  - `ArticleData` and `ExampleCatalog.PlantingHours` check the family where they checked `Perennial`, `Hedge` or `Fence` before.
  - `ArticleSheet` names the new keys for any unknown key, for example: "Okänd typmodell. Använd en av: tree-large, tree-fruit, …".
  - `CatalogPrices` fills `Family`.
  - `PriceCalculator.ClassOf` switches on `article?.Family`.
  - `ListArticles` adds `family`.
  - Add the migration with `dotnet ef migrations add RenameTypeModelsToForms ...`. Its `Up` runs `UPDATE catalog.articles SET type_model = ... WHERE type_model = ...` for the four renames. `Down` reverses them, mapping every new tree form to `DeciduousTree` and so on by family. Check the table name in `ArticleConfiguration`.

- [ ] **Step 4: Run the .NET suite**
  - Run: `dotnet test --solution GardenBuilder.slnx` (needs Docker).
  - Expected: all pass. Fix the existing tests that used the old members by switching them to the form with the same family.

- [ ] **Step 5: Commit after Josef's yes**: `"Split type models into forms and families"`.

### Task 2: Model sources, manifest and build tool

**Files:**
- Create:
  - `assets/models/quaternius/`: the `.gltf`, `.bin` and every texture they reference for `CommonTree_1`, `CommonTree_3`, `CommonTree_5`, `Pine_1`, `Pine_5`, `Bush_Common`, `Bush_Common_Flowers`, `Flower_3_Group`, `Flower_4_Group`, `Plant_1_Big`, `Grass_Wispy_Tall` and `Rock_Medium_1`, plus `Leaves_NormalTree_C.png`.
  - `assets/models/quaternius/LICENSE.txt` (the pack's licence text) and `SOURCE.md` (URL, version, date fetched 2026-11-17).
  - `assets/models/manifest.json`.
  - `src/web/scripts/build-models.mjs`.
  - Outputs: `src/web/src/editor/models/<key>.glb` for the 13 pack forms, and `src/web/src/editor/models/library.generated.json`.
- Modify: `src/web/package.json` (devDependencies `@gltf-transform/core`, `@gltf-transform/functions`, `sharp`; script `build:models`).
- Test:
  - `src/web/src/editor/models/library.test.ts`;
  - `tests/Unit/Catalog/ModelManifestTests.cs`.

**Interfaces:**
- Produces, in `manifest.json`:
  - an array of `{ key, family, source: string | null, leafTexture?: string, widthFactor?: number, nominalHeight: number, maxTriangles: number, license: "CC0-1.0", sourceUrl }`;
  - `source: null` for `pot` and `furniture`, which are made in code.
- Produces, in `library.generated.json`: `{ [key]: { height: number, width: number, triangles: number } }` for each GLB.
- Each GLB has its base at y = 0, centred on x/z, and is scaled to `nominalHeight`.
- Material names stay those of the source (`Leaves_NormalTree`, `Flowers`, `Bark_NormalTree`, …). The editor finds leaves and flowers by name.

- [ ] **Step 1: Write the failing tests**
  - `library.test.ts` (Node environment): read every GLB with `NodeIO`.
    - `each form keeps its triangle cap`: triangles ≤ `maxTriangles` from the manifest.
    - `each GLB stands on the ground at its nominal height`: bounding box min y ≈ 0 (±0.01) and height ≈ `nominalHeight` (±2 %).
    - `every pack form in the manifest has a GLB and a generated entry`.
    - `the models stay small`: every GLB ≤ 600 kB, all together ≤ 4 MB.
  - `ModelManifestTests.Manifest_matches_the_catalog_forms` (uses `RepoRoot`): the manifest keys equal the point-geometry keys of `TypeModels`, and each family equals `TypeModels.Family`.

- [ ] **Step 2: Run them and see them fail**
  - Run: `npm --prefix src/web test -- --run src/editor/models` and `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`.
  - Expected: FAIL, because there is no manifest or GLB yet.

- [ ] **Step 3: Write the manifest and the tool**
  - Manifest values:
    - `nominalHeight` 1.0 for plants and 0.6 for `rock`.
    - `widthFactor` 0.42 for `conifer-column` and 1.64 for `tree-fruit`.
    - `leafTexture: "Leaves_NormalTree_C.png"` for `conifer-column` and `shrub-round`.
    - The caps from Global Constraints.
  - `build-models.mjs`, per pack form:
    - read the source;
    - drop normal maps;
    - swap the leaf texture where the manifest says;
    - scale x/z by `widthFactor`, then scale uniformly to `nominalHeight`;
    - move the base to y = 0, centred;
    - `dedup`, `prune` and `weld`;
    - resize textures to at most 512 px and encode them as WebP with sharp;
    - write the GLB and the generated entry.
  - Run it with: `npm --prefix src/web run build:models -- <path to the unzipped glTF folder or assets/models/quaternius>`.

- [ ] **Step 4: Build the models and run the tests**
  - Run: `npm --prefix src/web run build:models`, then the tests from Step 2.
  - Expected: PASS. Look at the triangle and size figures in the output.

- [ ] **Step 5: Commit after Josef's yes**: `"Add the Quaternius model sources and build tool"`.

### Task 3: Catalog types and colour rules in the web app

**Files:**
- Modify:
  - `src/web/src/api/catalog.ts` (`TypeModelKey`, `family`);
  - `src/web/src/editor/testArticles.ts`;
  - `src/web/src/editor/sizing.ts`.
- Create: `src/web/src/editor/modelColors.ts`.
- Test: `src/web/src/editor/modelColors.test.ts`, `src/web/src/editor/sizing.test.ts`.

**Interfaces:**
- Produces:
  - `type TypeModelKey`, the 15 form keys plus lines and surfaces.
  - `CatalogArticle.family: string`.
  - `isPlant(article: CatalogArticle): boolean`, by family.
  - `leafTintNeeded(color: string): boolean`.
  - `greyPixels(rgba: Uint8ClampedArray): void`, which turns the pixels grey in place, scaled so that the average opaque pixel has luminance 210.
  - `renderScale(article: CatalogArticle, nominalHeight: number): number`.

- [ ] **Step 1: Write the failing tests**
  - `leafTintNeeded`:
    - true for `#b5452f` and `#9fbf45`;
    - false for `#5f9e3f`, `#8dbb4c`, `#3f7a3a` and `#4c8a3e`;
    - false for an unreadable colour.
  - `greyPixels`:
    - a pure red opaque pixel and a pure green one become r = g = b;
    - the opaque average comes out at 210 ± 1;
    - fully transparent pixels do not count toward the average.
  - `renderScale`:
    - a plant with height 1.3 and nominal height 1 gives 1.3;
    - a pot with scale 1.2 gives 1.2 whatever the nominal height.

- [ ] **Step 2: Run them and see them fail**
  - Run: `npm --prefix src/web test -- --run src/editor/modelColors src/editor/sizing`.
  - Expected: FAIL.

- [ ] **Step 3: Implement**
  - `leafTintNeeded` tints when the hue is outside 80°–170° or the saturation is under 0.15. The spec's examples pin this.
  - `renderScale` divides the planting height by `nominalHeight` for plants, and otherwise returns `article.scale`.

- [ ] **Step 4: Run the web suite, lint and build**
  - Run: `npm --prefix src/web test`, `npm --prefix src/web run lint` and `npm --prefix src/web run build`.
  - Expected: PASS. Callers of the old `renderScale(article)` are updated in Task 4, so this task leaves `PointInstances` passing the fallback's nominal height.

- [ ] **Step 5: Commit after Josef's yes**: `"Add form keys and colour rules to the web catalog"`.

### Task 4: The editor draws the GLB forms

**Files:**
- Create:
  - `src/web/src/editor/modelLibrary.ts`: the GLB URLs, built with `new URL('./models/<key>.glb', import.meta.url)`, and the material roles.
  - `src/web/src/editor/GlbInstances.tsx`.
- Modify:
  - `src/web/src/editor/models.ts`: keep the generated shapes as the fallback, keyed by form, and add `rock`.
  - `src/web/src/editor/SceneCanvas.tsx:141-177,299`;
  - `src/web/src/editor/PointInstances.tsx`;
  - `src/web/src/editor/EditorPage.tsx` (checkbox unchanged in meaning: on = GLB).
- Test: `src/web/src/editor/modelLibrary.test.ts`.

**Interfaces:**
- Consumes: `library.generated.json` (Task 2); `leafTintNeeded`, `greyPixels`, `renderScale` and `isPlant` (Task 3).
- Produces:
  - `partsFor(key: PointFormKey, materials: string[]): GlbPart[]`, where `GlbPart = { key: string; material: string; role: 'leaves' | 'leaves-tinted' | 'flowers' | 'fixed'; when: (a: CatalogArticle) => boolean }`.
  - `fallbackFor(key: PointFormKey): ModelPart[]`.

- [ ] **Step 1: Write the failing tests** (`modelLibrary.test.ts`)
  - `leaves split into pack and tinted parts`: for `shrub-flowering` with materials `['Leaves_NormalTree','Flowers']`, the parts are:
    - `leaves`, whose `when` is false for a `#b5452f` article;
    - `leaves-tinted`, whose `when` is true for it;
    - `flowers`, whose `when` is false when `accentColor` is null.
  - `bark is fixed`: `Bark_NormalTree` gives role `fixed` with `when` always true.
  - `every point form has a fallback`: `fallbackFor` returns at least one part for each of the 15 keys.

- [ ] **Step 2: Run them and see them fail**
  - Run: `npm --prefix src/web test -- --run src/editor/modelLibrary`.
  - Expected: FAIL.

- [ ] **Step 3: Implement**
  - `GlbInstances` loads one form with `useGLTF` inside `<Suspense>`, with the fallback shapes as the Suspense fallback and in an error boundary.
  - For each part it draws one `InstancedMesh` with the GLB geometry and a cloned material:
    - `leaves-tinted` and `flowers` get a grey copy of the map (`greyPixels` on a canvas, cached per texture);
    - the instance colour is white for `leaves` and `fixed`, `article.color` for `leaves-tinted` and `accentColor` for `flowers`.
  - Removed elements lerp the instance colour toward `#b0b0b0` as today, which also fades textured parts.
  - Scale per instance uses `renderScale(article, nominalHeight)` and `widthFactor` is already baked into the geometry.
  - `detail === 'low'` draws only the fallbacks.

- [ ] **Step 4: Run the web suite and check in the browser**
  - Run: `npm --prefix src/web test`, `lint` and `build`.
  - Browser check with `preview_start` "aspire":
    - import the example catalog with the Aspire button;
    - place a thuja, an apple tree, a Japanese maple, a white and a pink hortensia, and a rock;
    - the thuja is a slim column, the maple's leaves are red, and the hortensias have white and pink flowers;
    - mark a placed tree as "Ta bort" and see it faded;
    - "Lägg till 300 växter" still works;
    - turn "Detaljerade modeller" off to see the fallbacks;
    - no console errors.

- [ ] **Step 5: Commit after Josef's yes**: `"Draw the Quaternius forms in the editor"`.

### Task 5: Docs

**Files:**
- Modify:
  - `docs/design.md` ("Modellbibliotek": forms, families, colours, files in the web app instead of Blob Storage);
  - `docs/superpowers/plans/2026-11-13-roadmap.md` (row 2 "Klar" with links to its spec and plan; row 6 "Klar" with links; note under "Att göra för dig" that the fps measurement should now be done with the real models);
  - `docs/superpowers/specs/2026-11-14-catalog-design.md` (the "Typmodeller (fast lista tills delprojekt 6)" section points to the model library spec);
  - `assets/catalog/artiklar-exempel.xlsx` (regenerated by the import tool so its Typmodell column has the new keys).

- [ ] **Step 1:** Update the documents and regenerate the example sheet with the import tool's write mode.
- [ ] **Step 2: Run** `dotnet test --solution GardenBuilder.slnx` and `npm --prefix src/web test`. Expected: PASS.
- [ ] **Step 3: Commit after Josef's yes**: `"Update design and roadmap for the model library"`.
- [ ] **Step 4:** Ask Josef about push and PR.
