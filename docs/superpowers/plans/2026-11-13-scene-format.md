# Scenformatet – genomförandeplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scendokumentet definieras en gång i C#, JSON-schema och TypeScript-typer genereras ur det, editorn använder de genererade typerna och Design-modulen kan validera en scen.

**Architecture:** Typerna är vanliga dataobjekt i `src/Contracts/Scenes`, eftersom Design, Pricing, Ai och admin alla läser formatet. `SceneJson` äger serialiseringsinställningarna och exporterar schemat med `System.Text.Json.Schema.JsonSchemaExporter`. Ett litet konsolprogram skriver schemat till webbklienten, där `json-schema-to-typescript` gör TS-typer. Valideringen är en ren funktion i Design-modulen.

**Tech Stack:** .NET 10, System.Text.Json, xUnit v3, React + TypeScript, json-schema-to-typescript.

**Spec:** [docs/design.md](../../design.md), avsnitten "Scendokumentet", "Editorns tillstånd" och "Regler för data".

## Global Constraints

- Kod, identifierare, kommentarer och commit-meddelanden på engelska. Allt användaren ser på svenska.
- Mått i meter. Koordinater: x = öster, y = norr. Marken är platt.
- Identiteter är UUID v7 (`Ids.New()` i C#).
- Paketversioner bara i `Directory.Packages.props`, aldrig i en csproj.
- Varningar är fel (`Directory.Build.props`).
- En modul refererar aldrig en annan modul. `tests/Architecture` måste fortsatt passera.
- Elementtyper och geometri enligt designen: Tomt (polygon), Byggnad (polygon, höjd, takform), Yta (polygon, m²), Linje (flera punkter, m), Punkt (position och rotation, antal).
- Varje element som kostar något pekar på en artikel. Status är `existing-keep`, `existing-remove` eller `new`.
- Varje element anger varifrån måtten kommer: `estimated`, `customer` eller `verified`.
- Formatet har ett versionsnummer. Nuvarande version är `1`.

## Review Focus

1. Ett dokument med okänd `formatVersion` (t.ex. 2, eller fältet saknas) ska ge ett tydligt fel, inte krascha eller tolkas som version 1. Testas i Task 1 och Task 4.
2. JSON med okänt fält eller okänd `kind` ska avvisas, inte tyst tappas, så att AI-utdata aldrig halvt läses in. Testas i Task 1.
3. En tomt med nästan noll yta (alla punkter på en linje) ska ge valideringsfel, eftersom den annars ger prisrader på 0 m² och en osynlig scen. Testas i Task 4.
4. Orimliga koordinater (t.ex. 1e9 m från origo eller `NaN`) ska ge valideringsfel innan de når editorn eller prisberäkningen. Testas i Task 4.
5. Editorns startscen måste vara giltig enligt C#-valideringen, så att klient och server inte glider isär. Testas i Task 4 genom att C# läser webbens `demoScene.json`.

---

### Task 0: Commit the editor prototype

**Files:** allt som redan är ändrat på grenen `feat/editor-prototype` under `src/web/src/editor/` (`EditorPage.tsx`, `SceneCanvas.tsx`, `sceneStore.ts`, `demoCatalog.ts`, `demoScene.ts`, `models.ts`, `PointInstances.tsx`, `LineView.tsx`, `spacing.ts`, `editor.css`) samt de här två planerna.

- [ ] **Step 1: Verify the web client**

Run: `npm --prefix src/web run lint` och `npm --prefix src/web run build`
Expected: inga fel och inga varningar från oxlint, och `vite build` slutar med `built in`.

- [ ] **Step 2: Commit**

```bash
git add src/web/src/editor docs/superpowers/plans
git commit -m "Add 3D editor prototype with instancing, commands and 300-element stress test"
```

---

### Task 1: Scene types and JSON in Contracts

**Files:**
- Create: `src/Contracts/Scenes/SceneDocument.cs` (dokument, element och enum-typer)
- Create: `src/Contracts/Scenes/SceneJson.cs`
- Modify: `tests/Unit/GardenBuilder.UnitTests.csproj` (lägg till ProjectReference till `src/Contracts`)
- Test: `tests/Unit/Scenes/SceneJsonTests.cs`

**Interfaces:**
- Produces (namespace `GardenBuilder.Contracts.Scenes`):
  - `sealed record SceneDocument(int FormatVersion, IReadOnlyList<SceneElement> Elements)` med `public const int CurrentFormatVersion = 1;`
  - `abstract record SceneElement(Guid Id, ElementStatus Status, MeasurementSource MeasurementSource)` med `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` och `[JsonDerivedType]` för `"plot"`, `"building"`, `"surface"`, `"line"`, `"point"`.
  - `sealed record PlotElement(Guid Id, ElementStatus Status, MeasurementSource MeasurementSource, IReadOnlyList<Point2> Points, bool Sloped) : SceneElement`
  - `sealed record BuildingElement(Guid Id, ElementStatus Status, MeasurementSource MeasurementSource, IReadOnlyList<Point2> Points, double Height, RoofShape Roof) : SceneElement`
  - `sealed record SurfaceElement(Guid Id, ElementStatus Status, MeasurementSource MeasurementSource, Guid? ArticleId, IReadOnlyList<Point2> Points, SurfaceMaterial Material) : SceneElement`
  - `sealed record LineElement(Guid Id, ElementStatus Status, MeasurementSource MeasurementSource, Guid? ArticleId, IReadOnlyList<Point2> Points) : SceneElement`
  - `sealed record PointElement(Guid Id, ElementStatus Status, MeasurementSource MeasurementSource, Guid? ArticleId, Point2 Position, double Rotation) : SceneElement`. `Rotation` är radianer, moturs sett ovanifrån.
  - `enum ElementStatus { ExistingKeep, ExistingRemove, New }`, `enum MeasurementSource { Estimated, Customer, Verified }`, `enum RoofShape { Flat, Gable }`, `enum SurfaceMaterial { Grass, Paving, Gravel, Decking, Bed }`
  - `static class SceneJson` med `JsonSerializerOptions Options`, `string Serialize(SceneDocument scene)`, `SceneDocument Deserialize(string json)`.
- `Point2` kommer från `GardenBuilder.SharedKernel` och serialiseras som `{ "x": …, "y": … }`.

- [ ] **Step 1: Write the failing tests** i `tests/Unit/Scenes/SceneJsonTests.cs`

```csharp
[Fact] public void Round_trips_every_element_kind()
// Bygg en scen med ett element av varje sort, Serialize → Deserialize → Serialize.
// Assert.Equal(first, second) på de två JSON-strängarna.

[Fact] public void Writes_kind_and_kebab_case_enums()
// Serialize en scen med en PointElement (ExistingKeep, Estimated) och en BuildingElement (Gable).
// Assert.Contains för "\"kind\":\"point\"", "\"status\":\"existing-keep\"",
// "\"measurementSource\":\"estimated\"", "\"roof\":\"gable\"" och "\"formatVersion\":1".

[Fact] public void Reads_kind_even_when_it_is_not_the_first_property()
// {"formatVersion":1,"elements":[{"id":"<guid>","status":"new","measurementSource":"estimated",
//   "articleId":null,"position":{"x":1,"y":2},"rotation":0,"kind":"point"}]}
// Assert.IsType<PointElement>(Deserialize(json).Elements[0])

[Fact] public void Rejects_unknown_property()
// {"formatVersion":1,"elements":[],"extra":1} → Assert.Throws<JsonException>

[Fact] public void Rejects_unknown_kind()
// element med "kind":"pond" → Assert.Throws<JsonException>

[Fact] public void Rejects_missing_elements()
// {"formatVersion":1} → Assert.Throws<JsonException>
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`
Expected: kompileringsfel, eftersom `GardenBuilder.Contracts.Scenes` inte finns.

- [ ] **Step 3: Implement the types and `SceneJson`**

`SceneJson.Options` byggs en gång och görs skrivskyddad (`MakeReadOnly()`):
- `new JsonSerializerOptions(JsonSerializerDefaults.Web)` (camelCase)
- `Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower))`
- `RespectNullableAnnotations = true`, `RespectRequiredConstructorParameters = true`
- `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow`
- `AllowOutOfOrderMetadataProperties = true`
- `TypeInfoResolver = new DefaultJsonTypeInfoResolver()`, som schemaexporten i Task 2 behöver.

`Deserialize` fångar `NotSupportedException` och kastar `JsonException` med den som inre undantag. Anroparen ska bara behöva hantera en undantagstyp. Ett `null`-resultat ger också `JsonException`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`
Expected: PASS, inklusive de befintliga SharedKernel-testerna.

- [ ] **Step 5: Run the architecture tests**

Run: `dotnet test --project tests/Architecture/GardenBuilder.ArchitectureTests.csproj`
Expected: PASS. Contracts beror bara på SharedKernel och BCL.

- [ ] **Step 6: Commit**

```bash
git add src/Contracts/Scenes tests/Unit
git commit -m "Define the scene document in Contracts with strict JSON"
```

---

### Task 2: JSON schema export and drift guard

**Files:**
- Modify: `src/Contracts/Scenes/SceneJson.cs` (lägg till `ExportSchema`)
- Create: `tools/SchemaExport/GardenBuilder.SchemaExport.csproj` och `tools/SchemaExport/Program.cs`
- Modify: `GardenBuilder.slnx` (lägg till verktyget i en `/tools/`-mapp)
- Create: `src/web/src/api/scene.schema.json` (genererad, committas)
- Test: `tests/Unit/Scenes/SceneSchemaTests.cs`

**Interfaces:**
- Consumes: `SceneJson.Options`, `SceneDocument` från Task 1.
- Produces: `public static JsonNode SceneJson.ExportSchema()`. Programmet `dotnet run --project tools/SchemaExport -- <path>` skriver schemat indenterat och avslutar med en radbrytning.

- [ ] **Step 1: Write the failing tests** i `tests/Unit/Scenes/SceneSchemaTests.cs`

```csharp
[Fact] public void Schema_names_every_scene_type_once_in_defs()
// var defs = SceneJson.ExportSchema()["$defs"]!.AsObject();
// Assert.True(defs.ContainsKey(...)) för "SceneElement", "PlotElement", "BuildingElement",
// "SurfaceElement", "LineElement", "PointElement", "Point2", "ElementStatus",
// "MeasurementSource", "RoofShape", "SurfaceMaterial".

[Fact] public void Schema_pins_kind_per_element_type()
// defs["PointElement"]["properties"]["kind"]["const"] == "point" och "kind" finns i defs["PointElement"]["required"].

[Fact] public void Schema_disallows_additional_properties()
// defs["PlotElement"]["additionalProperties"] är false.

[Fact] public void Committed_schema_is_up_to_date()
// Hitta repo-roten genom att gå uppåt från AppContext.BaseDirectory tills GardenBuilder.slnx finns.
// Läs src/web/src/api/scene.schema.json och jämför med ExportSchema().ToJsonString(indented) + "\n".
// Normalisera "\r\n" till "\n" på båda sidor. Felmeddelandet ska säga: "Run npm --prefix src/web run gen:scene".
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`
Expected: kompileringsfel, eftersom `ExportSchema` inte finns.

- [ ] **Step 3: Implement `ExportSchema()`**

Anropa `Options.GetJsonSchemaAsNode(typeof(SceneDocument), exporterOptions)`. `exporterOptions.TransformSchemaNode` lägger till `"title": <typnamn>` på varje schemanod som saknar egenskap (`context.PropertyInfo is null`) och vars typ antingen är `Point2` eller ligger i namnrymden `GardenBuilder.Contracts.Scenes`.

Sedan lyfts de namngivna typerna ut till `$defs`, så att varje typ beskrivs en gång. Annars dupliceras typerna i den genererade TS-koden:

```
hoist(node):
  för varje barn i node (objektfält och arrayelement, djupet först):
    om barnet är ett objekt med "title" och inte är roten:
      namn = barnet["title"]
      om $defs saknar namn: hoist(barnet) och lägg en kopia under $defs[namn]
      ersätt barnet med { "$ref": "#/$defs/<namn>" }
    annars: hoist(barnet)
```

Roten behåller sin `title` (`SceneDocument`) och får fältet `$defs`.

- [ ] **Step 4: Implement the export tool**

`tools/SchemaExport/Program.cs` skriver `SceneJson.ExportSchema()` indenterat och avslutar med `"\n"` till sökvägen i `args[0]`. Verktyget returnerar 1 och skriver ut hur det används om argumentet saknas. Csproj: `OutputType Exe`, och en ProjectReference till Contracts.

- [ ] **Step 5: Generate the committed schema**

Run: `dotnet run --project tools/SchemaExport -- src/web/src/api/scene.schema.json`
Expected: filen skapas. `"$defs"` innehåller de elva typnamnen från Step 1.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Contracts/Scenes tools/SchemaExport GardenBuilder.slnx src/web/src/api/scene.schema.json tests/Unit
git commit -m "Export the scene JSON schema and guard it against drift"
```

---

### Task 3: Generated TypeScript types in the editor

**Files:**
- Modify: `src/web/package.json` (devDependency `json-schema-to-typescript` `^15.0.4`, skripten `gen:scene` och `gen:scene:ts`)
- Create: `src/web/src/api/scene.generated.ts` (genererad, committas)
- Modify: `src/web/src/api/scene.ts` (bara re-exporter)
- Create: `src/web/src/api/ids.ts`
- Create: `src/web/src/editor/demoScene.json`
- Modify: `src/web/src/editor/demoScene.ts`, `demoCatalog.ts`, `sceneStore.ts`, `SceneCanvas.tsx`, `PointInstances.tsx`, `LineView.tsx`, `spacing.ts`, `EditorPage.tsx`
- Modify: `src/web/tsconfig.app.json` (`"resolveJsonModule": true`)
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `src/web/src/api/scene.schema.json` från Task 2.
- Produces:
  - `src/web/src/api/scene.ts` exporterar typerna `SceneDocument`, `SceneElement`, `PlotElement`, `BuildingElement`, `SurfaceElement`, `LineElement`, `PointElement`, `Point2`, `ElementStatus`, `MeasurementSource` från `./scene.generated`.
  - `newId(): string` i `src/web/src/api/ids.ts` ger ett UUID v7.
  - `src/web/src/editor/demoScene.json` är editorns startscen. Task 4 läser den från C#.

- [ ] **Step 1: Add the generator scripts** i `src/web/package.json`

```json
"gen:scene": "dotnet run --project ../../tools/SchemaExport -- src/api/scene.schema.json && npm run gen:scene:ts",
"gen:scene:ts": "json2ts -i src/api/scene.schema.json -o src/api/scene.generated.ts --bannerComment \"/* Generated from scene.schema.json by npm run gen:scene. Do not edit. */\""
```

- [ ] **Step 2: Generate the types**

Run: `npm --prefix src/web install` och sedan `npm --prefix src/web run gen:scene`
Expected: `scene.generated.ts` innehåller interfaces för `PlotElement` … `PointElement` och `Point2`, varje element med ett `kind`-fält av literaltyp (t.ex. `kind: "point"`), och `SceneElement` som union av de fem.

Om `SceneElement` blir en intersection med ett tomt objekt i stället för en ren union: låt `TransformSchemaNode` i Task 2 ta bort `"type": "object"` från den polymorfa basnoden, generera om och kör om Task 2:s tester.

- [ ] **Step 3: Replace the hand-written types**

`scene.ts` blir bara re-exporter. I editorn byts `Vec2` mot `Point2`. `measurementSource` flyttas från dokumentnivå till varje element. Nya element får `measurementSource: 'estimated'`.

- [ ] **Step 4: Implement `newId()`** i `src/web/src/api/ids.ts`

De första 48 bitarna är `Date.now()` i millisekunder. Versionsnibblan sätts till `7` och variantbitarna till `10`. Resten är slump från `crypto.getRandomValues`. Formatet är `8-4-4-4-12` hex med gemener. Byt alla `crypto.randomUUID()` och `stress-…`-id:n i editorn mot `newId()`.

- [ ] **Step 5: Move the start scene to JSON and give the catalog UUIDs**

- `demoScene.json` innehåller samma scen som idag, med fasta UUID v7-id:n och `formatVersion: 1`.
- `demoScene.ts` exporterar `demoScene = demoSceneJson as unknown as SceneDocument` och behåller `stressElements`.
- I `demoCatalog.ts` får varje artikel ett fast UUID som `id`.
- Lägg till typen `SurfaceArticle` (`geometry: 'surface'`, `color`) och tre artiklar: "Gräsmatta, rullgräs", "Trädäck, tryckimpregnerat" och "Rabatt, planteringsjord". Ytorna `lawn`, `deck` och `bed` i startscenen pekar på dem.
- `SceneCanvas` färgar en yta med artikelns färg. Utan artikel används färgen för `material`.
- I katalogpanelen visas "Lägg till" inte för ytor, eftersom det ännu inte finns något polygonverktyg. "Byt" fungerar.

- [ ] **Step 6: Verify the web client**

Run: `npm --prefix src/web run lint` och `npm --prefix src/web run build`
Expected: inga fel. I synnerhet ska `switch (element.kind)` i `SceneCanvas.tsx` och `sceneStore.ts` kompilera, vilket visar att unionen smalnar av på `kind`.

- [ ] **Step 7: Check the editor in the browser**

Starta förhandsgranskningen `web` och öppna `/editor`. Expected:
- Startscenen ser ut som förut.
- "Lägg till 300 växter", dra, "Byt" och ångra fungerar.
- Konsolen visar inga fel.

- [ ] **Step 8: Guard the generated TypeScript in CI**

Lägg till ett steg i `.github/workflows/ci.yml` direkt efter "Web client – install, lint, build", med `working-directory: src/web`:

```yaml
run: |
  npm run gen:scene:ts
  git diff --exit-code -- src/api/scene.generated.ts
```

- [ ] **Step 9: Commit**

```bash
git add src/web .github/workflows/ci.yml
git commit -m "Generate scene TypeScript types from the C# schema and use them in the editor"
```

---

### Task 4: Scene validation in Design

**Files:**
- Create: `src/Modules/Design/Domain/SceneValidator.cs`
- Modify: `tests/Unit/GardenBuilder.UnitTests.csproj` (ProjectReference till `src/Modules/Design`, som redan har `InternalsVisibleTo` för `GardenBuilder.UnitTests`)
- Test: `tests/Unit/Design/SceneValidatorTests.cs`

**Interfaces:**
- Consumes: scentyperna och `SceneJson.Deserialize` från Task 1, och `demoScene.json` från Task 3.
- Produces (namespace `GardenBuilder.Modules.Design.Domain`, internal):
  - `sealed record SceneError(string Code, Guid? ElementId, string Message)`. `Message` är på engelska för loggar och AI-återkoppling. Klienten översätter `Code` till svenska.
  - `static class SceneValidator` med `IReadOnlyList<SceneError> Validate(SceneDocument scene)`. En tom lista betyder att scenen är giltig.

Regler och felkoder:

| Code | När |
| --- | --- |
| `format-version` | `FormatVersion != SceneDocument.CurrentFormatVersion` |
| `duplicate-id` | Två element har samma `Id` |
| `plot-count` | Antalet `PlotElement` är inte exakt 1 |
| `too-few-points` | Polygon (tomt, byggnad, yta) har färre än 3 punkter, eller en linje har färre än 2 |
| `degenerate-polygon` | Polygonens yta är under 0,01 m² (`Polygon.Area`) |
| `out-of-range` | Ett tal är inte ändligt, eller en koordinat har absolutbelopp över 1000 m |
| `building-height` | Byggnadens höjd är ≤ 0 eller > 30 m |
| `outside-plot` | En `PointElement`s position ligger utanför tomtens polygon (`Polygon.Contains`) |
| `missing-article` | Status `New` och elementet är yta, linje eller punkt utan `ArticleId` |

Varje regel ger ett fel per felaktigt element med `ElementId` satt. Fel som gäller hela dokumentet har `ElementId = null`. `outside-plot` hoppas över om det inte finns exakt en giltig tomt.

- [ ] **Step 1: Write the failing tests** i `tests/Unit/Design/SceneValidatorTests.cs`

Använd en hjälpfunktion `ValidScene()`: en tomt 20 × 20 m centrerad i origo, ett hus, en ny gräsyta med artikel, en ny häck med artikel och ett nytt träd med artikel i (2, −5). Varje test ändrar en sak med `with` och asserterar exakt en felkod.

```csharp
[Fact] public void Valid_scene_has_no_errors()                  // Assert.Empty
[Fact] public void Unknown_format_version_is_an_error()         // FormatVersion = 2 → "format-version"
[Fact] public void Duplicate_ids_are_an_error()                 // trädet får husets Id → "duplicate-id"
[Fact] public void Missing_plot_is_an_error()                   // tomten borttagen → "plot-count"
[Fact] public void Line_with_one_point_is_an_error()            // → "too-few-points" med häckens Id
[Fact] public void Plot_with_points_on_a_line_is_an_error()     // (0,0),(5,0),(10,0) → "degenerate-polygon"
[Fact] public void Far_away_coordinate_is_an_error()            // trädet i (1e9, 0) → "out-of-range"
[Fact] public void NaN_rotation_is_an_error()                   // Rotation = double.NaN → "out-of-range"
[Fact] public void Building_height_must_be_positive()           // Height = 0 → "building-height"
[Fact] public void Point_outside_plot_is_an_error()             // trädet i (15, 0) → "outside-plot"
[Fact] public void New_element_without_article_is_an_error()    // trädets ArticleId = null → "missing-article"
[Fact] public void Existing_element_without_article_is_allowed() // ExistingKeep och ArticleId = null → Assert.Empty
[Fact] public void Web_demo_scene_is_valid()
// Läs src/web/src/editor/demoScene.json från repo-roten (samma hjälpfunktion som i Task 2),
// SceneJson.Deserialize, Assert.Empty(SceneValidator.Validate(scene)).
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`
Expected: kompileringsfel, eftersom `SceneValidator` inte finns.

- [ ] **Step 3: Implement `SceneValidator.Validate`**

Använd `Polygon` och `Point2` från SharedKernel för yta och "punkt i polygon". Bygg en `Polygon` först när punktantalet är kontrollerat, eftersom konstruktorn kastar vid färre än 3 punkter.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`
Expected: PASS.

- [ ] **Step 5: Run everything**

Run: `dotnet test --solution GardenBuilder.slnx` (Docker måste vara igång för integrationstesterna)
Expected: PASS i Unit, Architecture och Integration.

- [ ] **Step 6: Commit and open the PR**

```bash
git add src/Modules/Design tests/Unit
git commit -m "Validate scene documents in the Design module"
```

Pusha `feat/editor-prototype` och öppna en PR mot `main` med titeln "3D editor prototype and scene format".
