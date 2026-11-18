# Fototolkning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The customer shapes their real plot and house, uploads 1–6 photos, marks where each was taken, and gets an
AI proposal of what exists today that they accept or reject element by element.

**Architecture:** Garden module owns photos (re-encoded with Magick.NET, stored in Blob Storage) and camera poses.
The Ai module runs a `photo-interpretation` job in the existing queue: one Claude call per photo with structured
output, then plain C# moves each photo's camera-relative answer onto the plot, clips, merges duplicates and maps to
scene elements, and filters them through `ISceneCheck`. The editor gets a shape tool for plot and house, a photo panel
with camera pins in a top-down view, and a review list whose "Ta med" is an undoable `add` command.

**Tech Stack:** .NET 10, EF Core + PostgreSQL, Azure Blob Storage (Azurite locally), Magick.NET, Clipper2, Anthropic
C# SDK 13.9.0, React + React Three Fiber + zustand, vitest, xUnit v3.

**Spec:** [docs/superpowers/specs/2026-11-18-photo-interpretation-design.md](../specs/2026-11-18-photo-interpretation-design.md)

## Global Constraints

- Everything the user sees is Swedish; code, identifiers, comments and commits are English (CLAUDE.md).
- Package versions only in `Directory.Packages.props`. New: `Magick.NET-Q8-AnyCPU`, `Clipper2`,
  `Testcontainers.Azurite` (same version as `Testcontainers.PostgreSql`, 4.16.0). Pick the newest stable of the first
  two that is at least two weeks old.
- A module never references another module; cross-module calls go through `src/Contracts` (enforced by
  `tests/Architecture`). Contracts gets no ASP.NET or EF references.
- Only the Worker calls Claude and reads `Anthropic:ApiKey`.
- At most 6 photos per garden, at most 15 MB per file, accepted content JPEG, PNG, WebP, HEIC.
- Re-encode to JPEG, longest side at most 1568 px, upright by EXIF orientation, no metadata. The original is never
  stored.
- Photo links (SAS) live 10 minutes and cover one blob. Blob container: `garden-photos`, private.
- Camera pose: point in scene meters, heading in degrees (0 = north, clockwise). Allowed inside the plot or at most 5 m
  outside.
- Prompt version `photo-interpretation@1`. Model per kind in `Anthropic:Models:photo-interpretation`, default
  `claude-sonnet-5-5`; its price (2 / 10 USD per million tokens) goes into `Ai:Prices` in both Api and Worker settings.
- Placement numbers: discard tolerance 0.5 m outside the plot, discard confidence below 0.3, merge surfaces at ≥ 30 %
  overlap of the smaller, lines within 1.5 m along ≥ half the shorter, points closer than half the larger width.
- Plot edges may lie up to 0.5 m outside the plot in the "inside the plot" check (fences and hedges on the boundary).
- Scene `formatVersion` 2; version 1 is read and upgraded, never rewritten in the database ahead of time.
- Every editor change is a command so undo and redo work. Scene coordinates: x east, y north; three.js north is -Z.
- Ask Josef before every commit (memory rule). Commit steps below mean "ask, then commit".

## Review Focus

1. A portrait phone photo with EXIF orientation 6 must come out upright after re-encoding (Task 4 test).
2. An AI answer with NaN, absurd coordinates (10 km), an empty element list, unknown types or a missing house must
   produce a smaller proposal, never an exception that fails the job (Task 6 tests).
3. Customer B asking for customer A's photos, photo link, camera, interpretation or job gets 404, exactly as a missing
   one (Task 5 and Task 7 tests).
4. Reshaping the plot so that an existing element falls outside must be refused in the editor with a notice; otherwise
   the next save is rejected by the server and the customer is stuck (Task 2 test).
5. A seventh photo, also when two uploads race, gets 409 `too-many-photos` (Task 5 test).

---

### Task 1: Scene format version 2

**Files:**
- Modify: `src/Contracts/Scenes/SceneDocument.cs`, `src/Contracts/Scenes/SceneJson.cs`
- Modify: `src/Modules/Design/Domain/SceneValidator.cs`, `src/Modules/Design/Features/WorkingCopy/DesignEndpoints.cs`
- Modify: `src/web/src/editor/demoScene.json`, regenerate `src/web/src/api/scene.schema.json` and `scene.generated.ts`
- Test: `tests/Unit/Scenes/SceneJsonTests.cs`, `tests/Unit/Design/SceneValidatorTests.cs`,
  `tests/Integration/DesignEndpointTests.cs`

**Interfaces:**
- Produces: `SceneDocument.CurrentFormatVersion = 2`.
  `PointElement(..., string? Form = null, double? Width = null, double? Height = null)`,
  `LineElement(..., string? Form = null, double? Height = null)`,
  `BuildingElement(..., string? WallColor = null, string? RoofColor = null)` — new optional trailing parameters, JSON
  `form`, `width`, `height`, `wallColor`, `roofColor`, omitted when null.
  `SceneJson.Deserialize` reads versions 1 and 2 and always returns version 2.
  New validator codes: `missing-form` (point or line with neither article nor form), `bad-color` (not `#RRGGBB`),
  `bad-size` (width or height not in (0, 40] m). `outside-plot` now also for surfaces, lines and buildings, with every
  point allowed up to `SceneValidator.PlotTolerance = 0.5` m outside.
  DesignResponse gains `GardenId` (JSON `gardenId`); its scene is re-serialized through `SceneJson`, so stored
  version 1 comes back as version 2.

- [ ] **Step 1: Write the failing tests.** In `SceneJsonTests`: `Version_1_is_read_as_version_2` (a v1 JSON with a
  point without the new fields deserializes, `FormatVersion == 2`, `Form == null`), `Version_3_is_refused`
  (`JsonException`), `New_fields_round_trip` (point with `Form = "tree-large", Width = 6, Height = 9`, building with
  `WallColor = "#E3B23C"` survive Serialize → Deserialize), `Null_new_fields_are_omitted` (serialized v2 point has no
  `"form"` key). In `SceneValidatorTests`: `Existing_point_without_article_or_form_is_missing_form`,
  `Existing_point_with_form_is_valid`, `Hedge_half_a_meter_outside_is_valid` (line at y = plotMaxY + 0.4),
  `Surface_two_meters_outside_is_outside_plot`, `Building_outside_is_outside_plot`, `Wall_color_red_is_bad_color`.
  In `DesignEndpointTests`: `Get_returns_garden_id_and_version_2_for_a_stored_version_1_scene`.
- [ ] **Step 2: Run them and see them fail.** Run:
  `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj -- --filter-class "*SceneJsonTests" --filter-class "*SceneValidatorTests"`.
  Expected: compile errors, then FAIL.
- [ ] **Step 3: Implement.** Records and `SceneJson` as in Interfaces (`EnsureSupportedFormatVersion` accepts 1 and 2;
  after reading set `FormatVersion` to 2). Validator: replace the points-only plot check with a check over every point
  of surfaces, lines, buildings and point elements using the distance to the plot polygon (inside, or within
  `PlotTolerance`); add a `DistanceTo(Point2)` to `Polygon` in `src/SharedKernel/Geometry.cs` with its own unit test.
  `DesignEndpoints` returns `GardenId` and re-serializes the scene. Bump `demoScene.json` to `"formatVersion": 2`.
  Run `npm --prefix src/web run gen:scene` and fix TypeScript errors (`npm --prefix src/web run build`).
- [ ] **Step 4: Run all .NET and web tests.** Run: `dotnet test --solution GardenBuilder.slnx` and
  `npm --prefix src/web test`. Expected: PASS (update existing tests that hard-code version 1 to version 2).
- [ ] **Step 5: Commit** — `feat(scene): format version 2 with forms, sizes and house colours`.

### Task 2: Shape the plot and the house

**Files:**
- Create: `src/web/src/editor/shapeCommands.ts`, `src/web/src/editor/shapeCommands.test.ts`,
  `src/web/src/editor/geometry.ts`, `src/web/src/editor/geometry.test.ts`, `src/web/src/editor/ShapeHandles.tsx`
- Modify: `src/web/src/editor/sceneStore.ts`, `SceneCanvas.tsx`, `EditorPage.tsx`, `editor.css`

**Interfaces:**
- Produces in `sceneStore.ts`: new `SceneCommand` variants
  `{ type: 'move-point'; id: string; index: number; point: Point2 }`,
  `{ type: 'insert-point'; id: string; index: number; point: Point2 }` (inserted before `index`),
  `{ type: 'remove-point'; id: string; index: number }`, `{ type: 'redraw'; id: string; points: Point2[] }` — only for
  `plot` and `building` elements. `Tool` gains `{ mode: 'shape'; id: string }` and
  `{ mode: 'draw'; id: string; points: Point2[] }`. Store gains `view: 'perspective' | 'top'` and `setView`.
- Produces in `shapeCommands.ts`: `shapeProblem(document: SceneDocument): string | null` (Swedish message or null),
  `tryShape(command: SceneCommand): void` (executes when `shapeProblem` of the result is null, else sets the notice).
- Produces in `geometry.ts`: `isSelfIntersecting(points: Point2[]): boolean`, `distanceToPolygon(p, points): number`,
  `segmentLengths(points: Point2[]): number[]`.

- [ ] **Step 1: Write the failing tests** in `shapeCommands.test.ts`: `move-point moves one corner and undo restores
  it`; `insert-point on a 4-corner plot gives 5 points`; `remove-point refuses to go below 3 points` (document
  unchanged); `redraw replaces all points`; `shapeProblem` returns exactly
  `"Tomtgränsen får inte korsa sig själv."` for a bowtie plot, `"Huset måste ligga inom tomten."` when a house corner
  is 1 m outside, `"2 saker hamnar utanför tomten. Flytta dem först."` when two existing points end up more than
  0.5 m outside, and null for the demo scene; `tryShape` leaves the document unchanged and sets that notice. In
  `geometry.test.ts`: bowtie is self-intersecting, square is not; `segmentLengths` of a 3 × 4 rectangle is
  `[3, 4, 3, 4]`.
- [ ] **Step 2: Run them and see them fail.** Run: `npm --prefix src/web test -- shapeCommands geometry`. Expected: FAIL.
- [ ] **Step 3: Implement** the commands in `applyCommand`, `geometry.ts`, `shapeCommands.ts`. Then the UI: a
  "Forma tomt" and a "Forma hus" button in `EditorPage`; they switch to `view: 'top'` (orthographic camera straight
  down, north up) and `tool: shape`. `ShapeHandles` draws a handle per corner (drag → `move-point` via `tryShape` on
  drop, one undo step per drag), a smaller handle mid-segment (drag → `insert-point`), double-click on a corner →
  `remove-point`, and the length of each segment in meters as a label ("4,2 m"). "Rita om" starts `tool: draw`; clicks
  add points, a click on the first point finishes with `redraw`. Escape or "Klar" returns to `select` and
  `perspective`.
- [ ] **Step 4: Run the web tests and look at it.** Run: `npm --prefix src/web test` and `npm --prefix src/web run lint`.
  Expected: PASS. Start the app, reshape the plot with a curved edge, undo, check the save succeeds (status "Sparad").
- [ ] **Step 5: Commit** — `feat(editor): shape the plot and house point by point`.

### Task 3: Draw existing things without an article; "Byt" in the same place

**Files:**
- Modify: `src/web/src/editor/SceneCanvas.tsx`, `PointInstances.tsx`, `LineView.tsx`, `spacing.ts`,
  `selectionCommands.ts`, `sizing.ts`
- Test: `src/web/src/editor/sizing.test.ts`, `src/web/src/editor/selectionCommands.test.ts`,
  `src/web/src/editor/spacing.test.ts` (create if missing)

**Interfaces:**
- Consumes: Task 1's `form`, `width`, `height`, `wallColor`, `roofColor`.
- Produces: `formOf(element, articles): PointFormKey | null` and `sizeOf(element, article?): { width; height }` in
  `sizing.ts` — an article wins; otherwise the element's own form and size.

- [ ] **Step 1: Write the failing tests:** `an existing tree without article is drawn as its form at its own size`
  (`formOf` → `'tree-large'`, `sizeOf` → width 6, height 9); `removed plants are left out of the crowding check`;
  `after Byt and undo the restored element is selected` (the old id is `selectedId` after `undo`);
  `a click on a spot with a removed and a new element selects the new one` (test the pick helper that orders hits:
  not-removed before removed).
- [ ] **Step 2: Run them and see them fail.** Run: `npm --prefix src/web test -- sizing selectionCommands spacing`.
  Expected: FAIL.
- [ ] **Step 3: Implement.** `placed` in `SceneCanvas` uses `formOf`/`sizeOf` instead of skipping article-less points;
  lines without article draw a hedge or fence by `form` and `height`. The building uses `wallColor`/`roofColor` when
  set. Crowding filters `removed`. Undo after a `replaceSelected` batch selects the element the batch removed (store a
  `reselectOnUndo` id on the undo entry, or select by checking which id came back; pick one and comment it).
  Clicks go through a new `pickTarget(hits: SceneElement[]): SceneElement | undefined` in `selectionCommands.ts` that
  prefers elements that are not `existing-remove`.
  Not in this plan: deleting photos when a garden is deleted — no garden deletion exists yet; delprojekt 13 adds it
  and must delete the `{gardenId}/` blobs.
- [ ] **Step 4: Run.** `npm --prefix src/web test` and lint. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(editor): draw existing things by form and fix Byt in the same place`.

### Task 4: Re-encode photos

**Files:**
- Create: `src/Modules/Garden/Features/Photos/PhotoEncoder.cs`
- Modify: `Directory.Packages.props`, `src/Modules/Garden/GardenBuilder.Modules.Garden.csproj`
- Test: `tests/Unit/Garden/PhotoEncoderTests.cs` (add the Garden project reference to the unit test csproj); test
  images generated in the test with Magick.NET, plus one small real HEIC in `tests/Unit/Garden/Files/` (make it
  yourself, no personal content).

**Interfaces:**
- Produces: `internal static class PhotoEncoder` with
  `public const int MaxBytes = 15 * 1024 * 1024; public const int MaxSide = 1568;` and
  `public static EncodedPhoto? Encode(Stream input)` returning null when the content is not an accepted image, where
  `internal sealed record EncodedPhoto(byte[] Jpeg, int Width, int Height)`.

- [ ] **Step 1: Write the failing tests:** `A_4000x3000_jpeg_becomes_1568x1176`, `Gps_and_exif_are_removed` (input with
  GPS tags and a comment; output has no EXIF, XMP, IPTC or comment profile), `Orientation_6_comes_out_upright`
  (a 300×200 image tagged orientation 6 → 200×300, no orientation tag), `Small_images_are_not_enlarged`,
  `Png_and_webp_are_accepted`, `Heic_is_accepted`, `Text_renamed_to_jpg_is_refused` (null),
  `A_gif_is_refused` (null).
- [ ] **Step 2: Run and see them fail.** Run:
  `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj -- --filter-class "*PhotoEncoderTests"`. Expected:
  compile errors, then FAIL.
- [ ] **Step 3: Implement** with `MagickImage`: detect format from content (`MagickImageInfo`/`MagickFormatInfo`), allow
  only Jpeg, Png, WebP, Heic/Heif; `AutoOrient()`, resize to fit `MaxSide` only when larger, `Strip()`, write JPEG
  quality 85. If `Heic_is_accepted` fails on the CI Linux runner, drop HEIC from the allowed list, delete that test,
  and write a ruling — the spec allows it ("Spara fotot som JPEG").
- [ ] **Step 4: Run.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(garden): re-encode photos without metadata`.

### Task 5: Photos and camera poses in the Garden module

**Files:**
- Create: `src/Modules/Garden/Domain/GardenPhoto.cs`, `src/Modules/Garden/Features/Photos/PhotoEndpoints.cs`,
  `PhotoStore.cs`, `GardenPhotos.cs`; migration `AddGardenPhotos`; `src/Contracts/Gardens/IGardenPhotos.cs`
- Modify: `GardenModule.cs`, `GardenConfiguration.cs`, `GardenDbContext.cs`, `src/Api/Program.cs`,
  `src/Worker/Program.cs` (`builder.AddAzureBlobServiceClient(ResourceNames.Blobs)`),
  `tests/Integration/ApiFactory.cs` (Azurite container, `ConnectionStrings:blobs`)
- Test: `tests/Integration/GardenPhotoEndpointTests.cs`

**Interfaces:**
- Produces in Contracts:
  ```csharp
  public sealed record CameraPose(Point2 Position, double HeadingDegrees);
  public sealed record GardenPhotoInfo(Guid Id, Guid GardenId, int Width, int Height, CameraPose? Camera);
  public interface IGardenPhotos
  {
      Task<IReadOnlyList<GardenPhotoInfo>> ListAsync(Guid gardenId, CancellationToken cancellationToken);
      /// <summary>The re-encoded JPEG, or null when the photo no longer exists.</summary>
      Task<byte[]?> ReadAsync(Guid photoId, CancellationToken cancellationToken);
  }
  ```
- Produces endpoints (all check that the current customer owns the garden; otherwise 404):
  `POST /api/gardens/{gardenId}/photos` (multipart, field `file`) → 201 `{ id, width, height, url }`;
  400 `{ code: "not-an-image" }`, 413 `{ code: "too-large" }`, 409 `{ code: "too-many-photos" }`.
  `GET /api/gardens/{gardenId}/photos` → `[{ id, width, height, url, camera: { x, y, heading } | null }]`, `url` a
  10-minute read SAS for that blob. `DELETE .../photos/{photoId}` → 204.
  `PUT .../photos/{photoId}/camera` body `{ x, y, heading }` → 204; 400 `{ code: "camera-outside-plot" }` when the
  point is more than 5 m outside the plot of the garden's design, read through the new contract below.
- Produces in Contracts and the Design module (Task 7 consumes it):
  ```csharp
  public sealed record DesignScene(Guid DesignId, Guid GardenId, Guid CustomerId, SceneDocument Scene);
  public interface IDesignWorkingCopies
  {
      Task<DesignScene?> GetAsync(Guid designId, CancellationToken cancellationToken);
      Task<DesignScene?> LatestForGardenAsync(Guid gardenId, CancellationToken cancellationToken);
  }
  ```
  Implemented in `src/Modules/Design/Features/WorkingCopy/DesignWorkingCopies.cs`.
- Blob name: `{gardenId}/{photoId}.jpg`. The 6-photo cap is enforced in one transaction that counts and inserts with
  `SELECT ... FOR UPDATE` on the garden row, so two racing uploads cannot both become number 6.

- [ ] **Step 1: Write the failing tests:** `Upload_list_and_read_a_photo` (the SAS url returns JPEG bytes with no EXIF);
  `Seventh_photo_is_409_too_many_photos`; `Two_uploads_racing_for_the_sixth_slot_store_one` (5 stored, two parallel
  uploads → one 201, one 409); `File_over_15_MB_is_413`; `Text_file_is_400_not_an_image`;
  `Another_customers_garden_is_404_for_every_endpoint`; `Camera_is_saved_and_listed`;
  `Camera_six_meters_outside_the_plot_is_400`; `Deleting_removes_the_blob`.
- [ ] **Step 2: Run and see them fail.** Run:
  `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj -- --filter-class "*GardenPhotoEndpointTests"`.
  Expected: compile errors, then FAIL.
- [ ] **Step 3: Implement** entity (`id, garden_id, blob_name, width, height, camera_x, camera_y, camera_heading,
  created_at`), migration with the command from CLAUDE.md, store over `BlobServiceClient` (create the container if
  missing, private), endpoints, `IGardenPhotos`. Request size limit 16 MB on the upload endpoint. Add the
  `photos` → design `gardenId` link the client needs: the editor reads `gardenId` from Task 1's DesignResponse.
- [ ] **Step 4: Run** the whole integration suite. Expected: PASS. Check `tests/Architecture` still passes.
- [ ] **Step 5: Commit** — `feat(garden): photo upload, listing and camera poses`.

### Task 6: From camera answers to scene elements (pure code)

**Files:**
- Create in `src/Modules/Ai/Features/PhotoInterpretation/`: `PhotoAnswer.cs`, `PhotoPlacement.cs`,
  `PhotoProposal.cs`
- Modify: `Directory.Packages.props`, `src/Modules/Ai/GardenBuilder.Modules.Ai.csproj` (Clipper2)
- Test: `tests/Unit/Ai/PhotoPlacementTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  // What Claude returns for one photo, camera frame: x right, y ahead, meters (the spike's schema).
  internal sealed record PhotoAnswer(string? Summary, PhotoHouse? House, IReadOnlyList<PhotoThing> Elements, IReadOnlyList<string>? Unsure);
  internal sealed record PhotoHouse(bool Visible, string? WallColor, string? RoofForm, string? RoofColor, double Confidence);
  internal sealed record PhotoThing(string Type, string Label, string Shape, IReadOnlyList<double[]> Points, double? WidthM, double? HeightM, double Confidence);
  internal sealed record PhotoProposal(IReadOnlyList<ProposedElement> Elements, HouseLook? House, IReadOnlyList<string> Dropped);
  internal sealed record ProposedElement(string Label, double Confidence, SceneElement Element);
  internal sealed record HouseLook(string WallColor, string RoofColor, RoofShape Roof);
  internal static class PhotoPlacement
  {
      public static PhotoProposal Place(IReadOnlyList<(PhotoAnswer Answer, CameraPose Camera)> photos, IReadOnlyList<Point2> plot, IReadOnlyList<Point2> house);
  }
  ```
- Rules (spec section 4, numbers in Global Constraints). Transform: scene = camera.Position + rotate(answer point),
  where heading h (degrees, clockwise from north) maps camera `y` (ahead) to (sin h, cos h) and camera `x` (right) to
  (cos h, −sin h). Type map exactly as the spec's table; trees: `pine` when the label or type says conifer
  ("barr", "gran", "tall", "tuja", "conifer"), otherwise `tree-large` when height ≥ 6 m, else `tree-small`. Colours:
  the AI's words map to hex through a fixed table in `PhotoPlacement` (falu red `#8E2A1E`, yellow `#E3B23C`,
  white `#F2F0EA`, grey `#9A9C9E`, black `#2B2B2B`, brown `#6B4A32`, green `#5E7A4A`, blue `#5F7FA0`, red-brown
  `#7A3B2E`); unknown words give no colour. `RoofForm` hip/gable → `Gable`, flat/shed → `Flat`. New ids from
  `Ids.New()`, status `ExistingKeep`, source `Estimated`. Everything outside the house is kept; things inside the house
  polygon are dropped. `Dropped` holds Swedish one-liners, e.g. `"Förråd (stöds inte än)"`.

- [ ] **Step 1: Write the failing tests:** `Heading_90_turns_ahead_into_east` (camera at (0,0) heading 90, point
  (0, 5) → (5, 0) ± 1e-9); `Heading_0_keeps_ahead_as_north`; `Thing_far_outside_the_plot_is_dropped`;
  `Hedge_on_the_boundary_is_kept`; `Lawn_is_clipped_to_the_plot` (area equals the overlap ± 0.01 m²);
  `Same_lawn_from_two_photos_becomes_one_surface`; `Two_trees_three_meters_apart_stay_two`;
  `Two_trees_half_a_meter_apart_become_one_with_the_higher_confidence`; `Confidence_0_2_is_dropped`;
  `Shed_is_dropped_and_named`; `Tall_spruce_becomes_pine`; `Yellow_wall_becomes_E3B23C_and_hip_roof_becomes_gable`;
  `House_from_the_most_confident_photo_wins`; `Invisible_house_gives_no_house_look`;
  `NaN_and_ten_kilometers_are_dropped_not_thrown`; `Empty_answers_give_an_empty_proposal`;
  `Unknown_type_is_dropped`; `Area_with_two_points_is_dropped`; `Thing_inside_the_house_is_dropped`.
- [ ] **Step 2: Run and see them fail.** Run:
  `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj -- --filter-class "*PhotoPlacementTests"`.
  Expected: compile errors, then FAIL.
- [ ] **Step 3: Implement** `PhotoPlacement.Place` with Clipper2 (`Clipper.Intersect` for clipping, `Clipper.Union`
  for merging lawns and overlapping surfaces of one material; meters scaled to integer centimetres for Clipper64).
  Merging lines: average the matched points; points: average positions. Keep functions small and private.
- [ ] **Step 4: Run.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(ai): place photo answers on the plot`.

### Task 7: The photo interpretation job

**Files:**
- Create: `src/Modules/Ai/Features/PhotoInterpretation/PhotoInterpretationHandler.cs`, `PhotoPrompt.cs`,
  `PhotoInterpretationEndpoints.cs`
- Modify: `src/Modules/Ai/Providers/ITextModel.cs`, `ClaudeTextModel.cs`, `src/Modules/Ai/Features/Run/AiCalls.cs`,
  `AiJobProcessingExtensions.cs`, `AiModule.cs`, `src/Worker/appsettings.json`, `src/Api/appsettings.json`
- Test: `tests/Integration/PhotoInterpretationTests.cs`; update `tests/Integration/AiJobRunnerTests.cs` fakes

**Interfaces:**
- Consumes: `IGardenPhotos` and `IDesignWorkingCopies` (Task 5), `PhotoPlacement` (Task 6), `ISceneCheck`.
- Produces:
  ```csharp
  // Ai providers
  internal sealed record ImageInput(byte[] Jpeg);
  internal sealed record TextRequest(string? System, string Prompt, int MaxTokens, string Model = "",
      IReadOnlyList<ImageInput>? Images = null, JsonElement? OutputSchema = null);
  // ITextModel: `string Model` is replaced by `string ModelFor(string kind)`;
  // AiCalls resolves ModelFor(job.Kind), checks its price, and passes it as request.Model.
  ```
  `AnthropicOptions` gains `Dictionary<string, string> Models`; `ModelFor(kind)` returns `Models[kind]` or `Model`.
  `ClaudeTextModel` sends images as base64 JPEG blocks before the prompt text, and `OutputSchema` as structured output
  (`OutputConfig.Format` with a JSON schema — look up the exact C# types in the SDK, see the claude-api skill's
  `csharp/claude-api/README.md`; compile-fix if needed).
  Job input JSON: `{ designId, photoIds: [..] }`. Result JSON: `{ elements: [{ label, confidence, element }],
  house: { wallColor, roofColor, roof } | null, dropped: [..], skippedPhotos: n }` with `element` serialized through
  `SceneJson.Options`.
  Endpoints (customer must own the garden and the design must belong to it; else 404):
  `POST /api/gardens/{gardenId}/photo-interpretations` body `{ designId }` → 202 `{ id }`;
  409 `{ code: "no-photos" }`, 409 `{ code: "camera-missing" }`, 409 with the queue's codes `quota-reached`,
  `budget-reached`, `step-off`.
  `GET /api/gardens/{gardenId}/photo-interpretations/{jobId}` → `{ status, result | null, failure | null }`.
- Handler: per photo, in order, one `calls.TextAsync("photo-interpretation@1", ...)` with the image, `PhotoPrompt.Text`
  (the spike prompt, adapted: no comments in the JSON, enums as listed) and `PhotoPrompt.Schema`; MaxTokens 16000. A
  photo that no longer exists or whose answer does not parse is skipped (`skippedPhotos`). If every photo was skipped,
  throw `AiProviderException("Vi kunde inte tolka fotona.", retryable: false)`. Elements that `ISceneCheck` rejects
  when added one by one to the working copy are moved to `dropped` as `"<label> (passar inte på tomten)"`.

- [ ] **Step 1: Write the failing tests** with a fake `ITextModel` that returns canned `PhotoAnswer` JSON:
  `Two_photos_give_one_merged_proposal_and_two_logged_calls`; `Photo_interpretation_uses_the_model_for_its_kind`
  (settings `Models:photo-interpretation = claude-sonnet-5-5`, the logged call says that model);
  `Unparseable_answer_skips_that_photo`; `All_photos_unparseable_fails_with_swedish_reason`;
  `Deleted_photo_is_skipped`; `Start_without_camera_is_409_camera_missing`; `Start_without_photos_is_409_no_photos`;
  `Fourth_interpretation_is_409_quota_reached`; `Another_customers_job_is_404`;
  `Result_never_changes_the_design` (working copy revision unchanged after the job).
- [ ] **Step 2: Run and see them fail.** Run:
  `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj -- --filter-class "*PhotoInterpretationTests"`.
  Expected: compile errors, then FAIL.
- [ ] **Step 3: Implement** in the order: `TextRequest`/`ITextModel`/`AiCalls`
  change (fix the connection test and its tests); `ClaudeTextModel` images and schema; handler and registration in
  `AddAiJobRunner`; endpoints in `MapAiEndpoints`. Add the Sonnet 5.5 price and
  `"Models": { "photo-interpretation": "claude-sonnet-5-5" }` to both appsettings files.
- [ ] **Step 4: Run** the whole .NET suite: `dotnet test --solution GardenBuilder.slnx`. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(ai): photo interpretation job`.

### Task 8: Photo panel, camera pins and review in the editor

**Files:**
- Create: `src/web/src/api/photos.ts`, `photos.test.ts`, `src/web/src/editor/photos/PhotoPanel.tsx`,
  `CameraPins.tsx`, `ProposalGhosts.tsx`, `reviewStore.ts`, `reviewStore.test.ts`
- Modify: `EditorPage.tsx`, `SceneCanvas.tsx`, `useSavedDesign.ts` (keep `gardenId`), `editor.css`

**Interfaces:**
- Consumes: Task 5 and Task 7 endpoints, Task 2's top view.
- Produces: `photos.ts` — `listPhotos(gardenId)`, `uploadPhoto(gardenId, file)`, `deletePhoto(gardenId, id)`,
  `setCamera(gardenId, id, camera)`, `startInterpretation(gardenId, designId)`, `fetchInterpretation(gardenId, jobId)`,
  errors as `PhotoError` with the server `code`. `reviewStore.ts` — zustand store with
  `proposals: Proposal[]`, `house: HouseLook | null`, `take(id)`, `drop(id)`, `takeAll()`, `takeHouse()`; `take`
  executes `{ type: 'add' }` (or one `batch` for `takeAll`) on the scene store.

- [ ] **Step 1: Write the failing tests:** `photos.test.ts` — upload sends multipart `file`; `too-many-photos` becomes
  `PhotoError` with that code. `reviewStore.test.ts` — `take adds the element and undo removes it`; `takeAll is one
  undo step`; `drop removes it from the list only`; `takeHouse sets wall and roof colour on the building`; `a proposal
  the server refuses on save is removed with "Passar inte längre på tomten."`.
- [ ] **Step 2: Run and see them fail.** Run: `npm --prefix src/web test -- photos reviewStore`. Expected: FAIL.
- [ ] **Step 3: Implement.** Panel "Foton": upload button (accept `image/jpeg,image/png,image/webp,image/heic`), up to 6
  thumbnails with a remove button, each with "Visa var du stod" (top view, click = position, drag = direction, saved
  with `setCamera`; the thumbnail shows beside the scene meanwhile); `CameraPins` draws an arrow per placed photo in
  the top view. "Tolka foton" is enabled when every photo has a camera; it polls every 2 s showing
  "Tolkar dina foton …". On success the list shows each proposal (label, "säker"/"ganska säker"/"osäker" for
  ≥ 0,7 / ≥ 0,4 / lower) with "Ta med" and "Ta bort", plus "Ta med alla" and, when present, "Husets färg och tak" with
  "Ta med". `ProposalGhosts` draws the not-yet-taken proposals at 40 % opacity. On failure or an empty result:
  "Vi kunde inte tolka fotona. Lägg in det som finns i trädgården själv, det går snabbt." Server codes to Swedish:
  `too-many-photos` "Du kan ladda upp högst 6 foton.", `too-large` "Fotot är större än 15 MB.", `not-an-image`
  "Filen är inte ett foto vi kan läsa. Spara det som JPEG och försök igen.", `camera-outside-plot` "Stå på tomten
  eller högst 5 meter utanför.", `quota-reached` "Du har tolkat foton för den här trädgården tre gånger.",
  `budget-reached`/`step-off` "Fototolkningen är inte tillgänglig just nu. Lägg in det som finns själv.",
  `camera-missing` "Visa var du stod för varje foto först.".
- [ ] **Step 4: Run** `npm --prefix src/web test`, lint and build. Expected: PASS. Start the app with a fake answer
  (no Claude call) and click through upload → pin → tolka → ta med alla → ångra.
- [ ] **Step 5: Commit** — `feat(editor): photo panel, camera pins and proposal review`.

### Task 9: Real run, docs and roadmap

**Files:**
- Modify: `docs/design.md` (Fototolkning row and "Gemensamma regler": model per kind), roadmap row 8 and the two
  "Till 8" notes, `evals/README.md` (how to rerun the spike), `README` of `docs/` if it lists specs

- [ ] **Step 1: Ask Josef before any real Claude call.** With his yes, start the app, shape his test garden, upload
  the photos in `evals/photos/`, pin them, run "Tolka foton" once with Sonnet 5.5. Note time, cost (from admin) and
  how many proposals were right.
- [ ] **Step 2: Update the docs** with what the run showed and mark row 8 "Klar" with links to spec and plan.
- [ ] **Step 3: Run everything.** `dotnet test --solution GardenBuilder.slnx`, `npm --prefix src/web test`, lint,
  build. Expected: PASS.
- [ ] **Step 4: Commit** — `docs: photo interpretation done`.
