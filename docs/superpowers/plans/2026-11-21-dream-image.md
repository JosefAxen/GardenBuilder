# Drömbild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The customer saves views in the 3D editor and turns a view into a photorealistic "Inspirationsbild" made
from the editor's own render plus (if they allow it) their nearest photos. Until image keys exist, a test model
returns the render stamped "Testbild", so the whole flow can be tried.

**Architecture:**
- **Scene format 3** adds `views`.
- **The editor** renders the chosen view to a PNG and uploads it with the job.
- **The Ai module** handles everything on the server:
  - it stores renders and results in a private blob container;
  - the `dream-image` job picks up to two photos by camera pose, writes a prompt and calls an `IImageModel` through
    `AiCalls.ImageAsync`, which applies the same guards and cost log as text;
  - a gallery endpoint lists the garden's dream images with short-lived links.
- **The editor panel** "Drömbild" holds the views, the photo opt-out, the quota and the gallery.

**Tech Stack:** .NET 10, EF Core + PostgreSQL (existing `ai.jobs`), Azure Blob (Azurite in tests), Magick.NET-Q8-AnyCPU
(already central), React 19 + R3F + zustand, vitest + Testing Library, xUnit v3.

**Spec:** [docs/superpowers/specs/2026-11-21-dream-image-design.md](../specs/2026-11-21-dream-image-design.md)

## Global Constraints

- **Language:** everything the user sees is Swedish; code, identifiers, comments and commits are English. The UI uses
  Plantskolan tokens and components only (`src/web/src/ui`, DESIGN.md).
- **Who calls providers:** only the Worker calls image providers; the API only enqueues. AI output is checked by plain
  code before it is shown.
- **Modules:** a module never references another module. The Ai module reads designs through `IDesignWorkingCopies`,
  photos through `IGardenPhotos` and articles through `ICatalogSelection` or a new contract method if needed (no
  module reference).
- **Scene format:**
  - `CurrentFormatVersion = 3`, `OldestReadableFormatVersion = 1`; versions 1 and 2 are upgraded with an empty
    `views` list.
  - View rules:
    - at most 8 views;
    - name 1–40 characters after trimming;
    - eye height 0.5–30 m, target height 0–30 m;
    - field of view 20–90°;
    - eye and target within 10 m of the plot.
- **Upload:** PNG only, at most 8 MB and 4096 px a side, re-encoded to JPEG q90 before storage. The editor renders
  1536 × 1024.
- **Storage:** blob container `dream-images`, private, blob names `{gardenId}/{jobId}/render.jpg` and
  `{gardenId}/{jobId}/image.jpg`. SAS links last 10 minutes.
- **Quota:** 5 per garden. Queued, running and succeeded jobs count; failed jobs do not. The existing `AiJobs`
  count already does this.
- **Photos:**
  - only when `sendPhotos` is true;
  - only photos with a camera pose, at most 2;
  - exclude a heading difference over 120°;
  - order by heading difference, then by distance;
  - if no photo has a pose, take the first two uploaded.
- **Prompt:** version `dream-image@1`, in English, listing articles with status `new` or `existing-keep` as
  "name × count".
- **Response check:** the image must decode, be at least 256 px on its short side, and have an aspect ratio between
  4:3 and 16:9 inclusive.
- **Test model:** `Images:Provider = "test"` is the default. It waits 3 s, stamps "Testbild" with a 6 px border and
  costs 0 kr. `Images:TestFailEvery = n` fails every n-th call (0 = never).
- **Copy:**
  - "Inspirationsbild";
  - "Offerten gäller designen, inte bilden.";
  - "Det gick inte att skapa en drömbild just nu. Här är 3D-vyn.";
  - "Designen har ändrats sedan bilden skapades";
  - "Skicka med mina foton (blir mer likt din trädgård)";
  - "{n} av 5 drömbilder kvar".
- **Commits:** ask Josef before every commit (memory rule). "Commit" steps mean ask, then commit.

## Review Focus

1. **Bad uploads.** A file that is not a PNG, is over 8 MB, or declares 20000 × 20000 px gets a 400 with code
   `invalid-image` and no job is queued (Task 6).
2. **Quota race.** Two "Skapa drömbild" requests at 4 of 5 used: exactly one is accepted and the other gets
   `quota-reached` (Task 6).
3. **Photo opt-out.** With `sendPhotos=false`, or a garden without photos, the job succeeds and the image model
   receives zero photos (Task 5).
4. **Views outside the plot.** A view 9 m outside the plot is valid; at 11 m the validator refuses it. The editor
   never saves such a view: it says "Ställ kameran närmare trädgården" (Tasks 1 and 8).
5. **Provider failure.** The gallery shows the render with the Swedish failure text, the slot is free again, and the
   failed entry can be removed (Tasks 6 and 9).

---

### Task 1: Scene format 3 with views (.NET)

**Files:**
- Modify: `src/Contracts/Scenes/SceneDocument.cs`, `src/Contracts/Scenes/SceneJson.cs`,
  `src/Modules/Design/Domain/SceneValidator.cs`
- Modify (version-number fixtures only): the tests listed by
  `grep -rln "CurrentFormatVersion\|FormatVersion == 2\|new SceneDocument(2" tests`
- Test: `tests/Unit/Scenes/SceneJsonTests.cs`, `tests/Unit/Design/SceneValidatorTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record Point3(double X, double Y, double Height)`;
  - `public sealed record SceneView(Guid Id, string Name, Point3 Eye, Point3 Target, double FieldOfView)`;
  - `SceneDocument(int FormatVersion, IReadOnlyList<SceneElement> Elements, IReadOnlyList<SceneView>? Views = null)`
    with `IReadOnlyList<SceneView> ViewsOrEmpty => Views ?? []`. It is serialized as `views` and always written
    (`[]` when empty) for version 3.
  - Validator error codes: `too-many-views`, `bad-view-name`, `view-outside-plot`, `bad-view-height`,
    `bad-field-of-view`.

- [ ] **Step 1: Write the failing tests:**
  - `SceneJsonTests.Reads_version_2_and_upgrades_to_3_with_no_views`
  - `Round_trips_a_view` (name "Från altanen", eye (5, -9, 1.7), target (10, 8, 1), fov 50)
  - `Refuses_version_4`
  - `SceneValidatorTests` for each error code, including:
    - **Review Focus 4:** an eye 9 m outside the plot is valid and 11 m is `view-outside-plot`;
    - 9 views give `too-many-views`;
    - a name of only spaces gives `bad-view-name`.
- [ ] **Step 2:** Run `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`. Expected: the new tests
  FAIL (compile errors or assertions).
- [ ] **Step 3:** Implement. `SceneJson` upgrades by setting `Views = []` when it is missing. The validator uses the
  existing `Polygon.DistanceTo` with tolerance 10.
- [ ] **Step 4:** Run all .NET tests (`dotnet test --solution GardenBuilder.slnx`, with Aspire stopped). Expected:
  PASS after the version fixtures are updated.
- [ ] **Step 5:** Run `npm --prefix src/web run gen:scene`, then `npm --prefix src/web test`. Expected: the types now
  have `views?` and `formatVersion: 3`. Web fixtures that use `formatVersion: 2` still pass (the version is not
  validated on the client).
- [ ] **Step 6:** Ask Josef, then commit `feat(scene): format 3 with saved views`.

### Task 2: View commands in the editor store

**Files:**
- Modify: `src/web/src/editor/sceneStore.ts` (commands), `src/web/src/api/scene.ts` (re-export `SceneView`, `Point3`)
- Create: `src/web/src/editor/dream/viewRules.ts`
- Test: `src/web/src/editor/dream/views.test.ts`

**Interfaces:**
- Consumes: `SceneView` and `Point3` from Task 1's generated types.
- Produces: commands `{ type: 'add-view'; view: SceneView }` and `{ type: 'remove-view'; id: string }`, both
  undoable.
- Produces: `viewProblem(view: SceneView, document: SceneDocument): string | null` in `viewRules.ts`, with the Swedish
  messages:
  - "Ge vyn ett namn.";
  - "Ställ kameran närmare trädgården.";
  - "Du kan spara högst 8 vyer.".
  - The limits match Task 1.

- [ ] **Step 1: Write the failing tests:**
  - `add-view` then `undo` returns the exact previous document.
  - `remove-view` removes only that id.
  - `viewProblem` returns each message at its limit (8 existing views; eye 11 m outside the plot; blank name) and
    null for a valid view.
- [ ] **Step 2:** Run `npm --prefix src/web test -- views`. Expected: FAIL.
- [ ] **Step 3:** Implement. `applyCommand` treats a missing `views` as `[]`.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): view commands`.

### Task 3: Image model, test model and image calls

**Files:**
- Create: `src/Modules/Ai/Providers/IImageModel.cs`, `src/Modules/Ai/Providers/TestImageModel.cs`,
  `src/Modules/Ai/Providers/ImagesOptions.cs`
- Modify:
  - `src/Modules/Ai/Domain/AiCost.cs`: `ModelPrice.PerImageUsd` and `AiCost.OfImage(string model, AiPricingOptions)`;
  - `src/Modules/Ai/Features/Run/IAiJobHandler.cs`: `IAiCalls.ImageAsync`;
  - `src/Modules/Ai/Features/Run/AiCalls.cs`;
  - `src/Modules/Ai/GardenBuilder.Modules.Ai.csproj`: Magick.NET reference without a version;
  - `src/Modules/Ai/AiJobProcessingExtensions.cs`: register `IImageModel` from `ImagesOptions`;
  - `src/Worker/appsettings.json`: `"Images": { "Provider": "test", "Models": { "dream-image": "test-image" } }` and
    `Ai:Prices:test-image` with `PerImageUsd` 0.
- Test: `tests/Unit/Ai/TestImageModelTests.cs`, `tests/Unit/Ai/AiCostTests.cs` (one more case),
  `tests/Integration/AiJobRunnerTests.cs` (image call guards)

**Interfaces:**
- Produces:
  - `internal sealed record ImageRequest(string Prompt, byte[] Reference, IReadOnlyList<byte[]> Photos, int Width, int Height, string Model = "")`
  - `internal sealed record ImageResponse(byte[] Jpeg, string Model)`
  - `internal interface IImageModel { string Provider { get; } string ModelFor(string kind); Task<ImageResponse> EditAsync(ImageRequest request, CancellationToken cancellationToken); }`
  - `IAiCalls.ImageAsync(string promptVersion, ImageRequest request, CancellationToken) : Task<ImageResponse>`.
    The guards are the same as `TextAsync`, the lease is renewed, and one `ai.calls` row is written with tokens 0 and
    the cost from `AiCost.OfImage`.
  - `TestImageModel` (`Provider` "test") draws the stamp with Magick.NET. Its delay is configurable (3 s by default;
    tests use 0). `Images:TestFailEvery` makes it throw `AiProviderException("Testläget misslyckades med flit.", retryable: false)`.

- [ ] **Step 1: Write the failing tests:**
  - `TestImageModelTests`: the output decodes with the same size as the reference; the pixel at (3, 3) has the border
    colour; with `TestFailEvery = 2` the second call throws.
  - `AiCostTests`: `OfImage("test-image")` is 0 kr; a model with `PerImageUsd` 0.04 at rate 10.5 is 0.42 kr.
  - `AiJobRunnerTests`: an image call with the step off is refused with `step-off`; an image call over the daily
    budget is refused; a successful call logs one row with model `test-image`.
- [ ] **Step 2:** Run the unit and integration test projects. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the same commands. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(ai): image model interface, test model and image calls`.

### Task 4: Photo choice, prompt and response check

**Files:**
- Create in `src/Modules/Ai/Features/DreamImages/`: `DreamPhotos.cs`, `DreamPrompt.cs`, `DreamImageCheck.cs`
- Test: `tests/Unit/Ai/DreamPhotosTests.cs`, `DreamPromptTests.cs`, `DreamImageCheckTests.cs`

**Interfaces:**
- Produces: `DreamPhotos.Choose(SceneView view, IReadOnlyList<GardenPhotoInfo> photos, bool sendPhotos) : IReadOnlyList<Guid>`
  - The view's heading is the compass direction from eye to target: 0 is north, clockwise.
- Produces: `DreamPrompt.Version = "dream-image@1"` and `DreamPrompt.Text(IReadOnlyList<(string Name, int Count)> items, int photoCount) : string`.
  - The text must say: keep every object's position, size and count; add no plants, furniture or buildings; use the
    later images only for the house, surroundings, light and materials; Swedish garden in daylight.
  - It lists `name × count`. With `photoCount` 0 the sentence about later images is left out.
- Produces: `DreamImageCheck.Accept(byte[] jpeg) : bool`, using the limits in Global Constraints.

- [ ] **Step 1: Write the failing tests:**
  - **Photo choice:**
    - Two posed photos facing the view and one facing away: the two facing photos are chosen, the closer heading
      first.
    - A photo at 121° difference is excluded.
    - With no poses, the first two uploaded are chosen.
    - With `sendPhotos=false` the result is empty.
  - **Prompt:**
    - It contains "Tuja Smaragd 40–60 cm × 12" and the keep-positions sentence.
    - With 0 photos it has no "later images" sentence.
  - **Check:**
    - 1536 × 1024 is accepted.
    - 200 × 150 is refused.
    - 1000 × 1000 (1:1) is refused.
    - Bytes that are not an image are refused.
- [ ] **Step 2:** Run `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the same command. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(ai): dream image photo choice, prompt and check`.

### Task 5: Storage and the dream-image job

**Files:**
- Create in `src/Modules/Ai/Features/DreamImages/`: `DreamImageStore.cs`, `DreamImageHandler.cs`
- Modify: `AiJobProcessingExtensions.cs` (register the handler), `AiModule.cs` (register `DreamImageStore` for
  the API too)
- Test: `tests/Integration/DreamImageJobTests.cs`

**Interfaces:**
- Consumes: Tasks 3 and 4; `IDesignWorkingCopies`, `IGardenPhotos`, `ICatalogSelection` (or the catalogue contract that
  gives article names by id).
- Produces: `DreamImageStore`:
  - `UploadAsync(string blobName, byte[] jpeg, CancellationToken)`;
  - `ReadAsync(string blobName, CancellationToken)`;
  - `DeleteAsync(string blobName, CancellationToken)`;
  - `LinkFor(string blobName) : Uri`, a 10-minute SAS link;
  - `RenderBlob(Guid gardenId, Guid jobId)` and `ImageBlob(Guid gardenId, Guid jobId)`.
- Produces: `DreamImageInput(Guid DesignId, Guid ViewId, int Revision, bool SendPhotos)` and
  `DreamImageResult(string ViewName, int Revision, bool Test)`.
  - The image is always stored at `ImageBlob`.
  - The render is always stored at `RenderBlob`, uploaded by the endpoint before the job runs.

- [ ] **Step 1: Write the failing tests** (Azurite and PostgreSQL as in `DesignProposalTests`, with a recording fake
  `IImageModel` that returns a 1536 × 1024 JPEG):
  - A job with two posed photos and `sendPhotos=true` sends exactly 2 photos, stores `image.jpg` and succeeds.
  - **Review Focus 3:** `sendPhotos=false` sends 0 photos and succeeds; a garden without photos sends 0 photos.
  - A response that fails `DreamImageCheck` fails the job with "Det gick inte att skapa en drömbild just nu."
  - A view id that no longer exists in the design still runs, from the uploaded render, with `ViewName` "Vy".
- [ ] **Step 2:** Run `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj`. Expected: FAIL.
- [ ] **Step 3:** Implement. The handler reads the render from the store, never from the request.
- [ ] **Step 4:** Run the same command. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(ai): dream image job and storage`.

### Task 6: Endpoints

**Files:**
- Create: `src/Modules/Ai/Features/DreamImages/DreamImageEndpoints.cs`
- Modify: the Ai module's endpoint mapping (as `MapDesignProposals`)
- Test: `tests/Integration/DreamImageEndpointTests.cs`

**Interfaces:**
- `POST /api/gardens/{gardenId}/dream-images` takes multipart fields `designId`, `viewId`, `sendPhotos` and `render`
  (a PNG file).
  - The design must be the current customer's and the view must exist in it.
  - The render is re-encoded and stored, then the job is enqueued with the design's revision.
  - It answers 202 `{ id }`, 400 `{ code: "invalid-image" | "no-such-view" }`, 404, or 409 with the refusal code.
  - If the refusal comes after storing, the stored render is deleted.
- `GET /api/gardens/{gardenId}/dream-images` returns
  `[{ id, status, viewName, revision, test, createdAt, imageUrl?, renderUrl, failure? }]`, newest first, the
  customer's own jobs only, with fresh links.
- `GET /api/gardens/{gardenId}/dream-images/{id}` returns the same shape for one job.
- `DELETE /api/gardens/{gardenId}/dream-images/{id}` answers 204 for failed jobs only (it deletes the job row and its
  blobs) and 409 `not-failed` otherwise.

- [ ] **Step 1: Write the failing tests:**
  - **Review Focus 1:** a JPEG renamed `.png`, a 9 MB PNG and a PNG declaring 20000 × 20000 each answer 400
    `invalid-image` with no job queued.
  - An unknown view gives 400 `no-such-view`.
  - **Review Focus 2:** at 4 of 5 used, two parallel POSTs give exactly one 202 and one 409 `quota-reached`.
  - **Review Focus 5:** a failed job is listed with a `renderUrl` and the failure text, does not count against the
    quota, and DELETE removes it.
  - Another customer's garden or job answers 404.
  - The full flow with `TestImageModel` (delay 0) gives a succeeded job with `imageUrl` and `test: true`.
- [ ] **Step 2:** Run the integration tests. Expected: FAIL.
- [ ] **Step 3:** Implement. Race safety: enqueue inside the same `SELECT … FOR UPDATE` pattern used for the photo
  cap, or count again inside the transaction.
- [ ] **Step 4:** Run all .NET tests. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(ai): dream image endpoints`.

### Task 7: Web API client and dream store

**Files:**
- Create: `src/web/src/api/dreams.ts`, `src/web/src/editor/dream/dreamStore.ts`
- Test: `src/web/src/api/dreams.test.ts`, `src/web/src/editor/dream/dreamStore.test.ts`

**Interfaces:**
- Produces:
  - `DreamImage` type (the GET shape);
  - `startDream(gardenId, designId, viewId, sendPhotos, png: Blob): Promise<string>`;
  - `listDreams(gardenId)`;
  - `deleteDream(gardenId, id)`;
  - `DreamError` with the messages from the codes (`invalid-image`, `no-such-view`, `quota-reached`, `budget-reached`,
    `step-off`).
- Produces: `useDreamStore` with `{ images, state: 'idle' | 'working' | 'failed', message }`.
  - `loadDreams(gardenId)`.
  - `createDream(view, png)` polls the list every 3 s until the job is done, giving up after 5 minutes with the
    failure copy.
  - `removeDream(id)`.
  - `sendPhotos` is kept in `localStorage` under `dream-send-photos:{gardenId}`, wrapped in try/catch, default true.
  - `remaining = 5 - images.filter(i => i.status !== 'failed').length`.

- [ ] **Step 1: Write the failing tests:**
  - The multipart body has the four fields.
  - A 409 `quota-reached` gives "Du har skapat fem drömbilder för den här trädgården."
  - Polling stops when the job succeeds.
  - It gives up after 5 minutes.
  - `remaining` ignores failed images.
  - `sendPhotos` defaults to true when `localStorage` throws.
- [ ] **Step 2:** Run `npm --prefix src/web test -- dream`. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): dream image client and store`.

### Task 8: Saving and rendering a view in the scene

**Files:**
- Create: `src/web/src/editor/dream/viewCapture.ts`, `src/web/src/editor/dream/CaptureBridge.tsx`
- Modify: `src/web/src/editor/SceneCanvas.tsx` (mount `<CaptureBridge/>` inside the Canvas)
- Test: `src/web/src/editor/dream/viewCapture.test.ts`

**Interfaces:**
- Produces: `cameraToView(position: [number, number, number], target: [number, number, number], fov: number, name: string): SceneView`,
  which converts from three.js (x, y up, z south) to the scene (x east, y north, height).
- Produces: `viewToCamera(view): { position, target, fov }`, the inverse.
- Produces: `currentView(name: string): SceneView | null`, read from the bridge.
- Produces: `showView(view): void`, which moves the orbit camera there.
- Produces: `renderView(view, width = 1536, height = 1024): Promise<Blob>`, a PNG drawn to a `WebGLRenderTarget` with a
  `PerspectiveCamera` from the view. It reads the pixels, flips them, and turns them into a PNG Blob through a canvas.

- [ ] **Step 1: Write the failing tests:**
  - `cameraToView([10, 1.7, 9], [10, 1, -8], 50, "Från altanen")` gives eye (10, -9, 1.7) and target (10, 8, 1).
  - `viewToCamera(cameraToView(x))` round-trips.
  - Heading 0 is north.
- [ ] **Step 2:** Run `npm --prefix src/web test -- viewCapture`. Expected: FAIL.
- [ ] **Step 3:** Implement. The bridge registers `gl`, `scene`, `camera` and `controls` in a module variable on mount
  and clears them on unmount.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS. In the browser, `renderView` gives a 1536 × 1024
  PNG of the current scene (pane visible).
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): save and render views`.

### Task 9: The "Drömbild" panel

**Files:**
- Create: `src/web/src/editor/dream/DreamPanel.tsx`, `src/web/src/editor/dream/dream.css`
- Modify: `editorUiStore.ts` (add the panel `'dream'`), `shelf/Shelf.tsx` (a fourth option "Drömbild", icon
  `Sparkles`), `shell/SidePanel.tsx` (title)
- Test: `src/web/src/editor/dream/dreamPanel.test.tsx`

**Interfaces:**
- Consumes: Tasks 2, 7 and 8.

**The panel content:**
- **"Dina vyer":** each view has "Visa", "Skapa drömbild" and "Ta bort". Below them are a name field and the button
  "Spara den här vyn", which shows `viewProblem`'s message.
- **The photo checkbox:** "Skicka med mina foton (blir mer likt din trädgård)", plus a line saying what is sent.
- **The quota line:** "{n} av 5 drömbilder kvar".
- **While working:** "Skapar drömbild … det tar ungefär en minut".
- **The gallery, newest first:**
  - each picture has the badge "Inspirationsbild", the view name and the date;
  - "Testbild" when `test`;
  - "Designen har ändrats sedan bilden skapades" when its revision is older than `useSaveStore.revision`;
  - failed images show the render with "Det gick inte att skapa en drömbild just nu. Här är 3D-vyn." and
    "Ta bort".
- **Click to enlarge:** an overlay with the image, "Offerten gäller designen, inte bilden.", "Ladda ner" (an
  `<a download>` with a fresh link) and "Stäng". Esc closes it.

- [ ] **Step 1: Write the failing tests:**
  - With no views the panel explains how to save one.
  - Saving a blank name shows "Ge vyn ett namn.".
  - With 0 remaining, "Skapa drömbild" is disabled with the quota text.
  - A failed image shows the render, the failure text and "Ta bort".
  - A succeeded test image shows "Inspirationsbild" and "Testbild".
  - Opening an image and pressing Esc closes the overlay and does not clear the scene selection.
- [ ] **Step 2:** Run `npm --prefix src/web test -- dreamPanel`. Expected: FAIL.
- [ ] **Step 3:** Implement with Plantskolan components and tokens only.
- [ ] **Step 4:** Run `npm --prefix src/web test` and `npm --prefix src/web run lint`. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): Drömbild panel`.

### Task 10: The whole flow in the browser and the docs

**Files:**
- Modify:
  - `docs/superpowers/plans/2026-11-13-roadmap.md`: row 10 Klar, with a link to the design and the plan;
  - `docs/design.md`: one sentence under "AI-flödena" saying that photos are sent only with the customer's consent,
    and that the provider choice is pending;
  - `evals/README.md`: how to compare two providers once the keys exist;
  - `DESIGN.md`: add the Drömbild panel to the editor layout.

- [ ] **Step 1:** Run Aspire. In the browser (pane visible), with a garden that has an uploaded and placed photo:
  1. save a view;
  2. create a dream image;
  3. see "Testbild" in the gallery;
  4. open it, download it, and close it;
  5. set `Images:TestFailEvery = 1` in Worker user secrets, create another, and see the failure with the render;
  6. remove it;
  7. check the quota line.
  - Capture screenshots to `.impeccable/review/dream/`.
- [ ] **Step 2:** Run all checks: `npm --prefix src/web test`, `npm --prefix src/web run lint`,
  `npm --prefix src/web run build`, and `dotnet test --solution GardenBuilder.slnx` (Aspire stopped). Expected: all
  PASS.
- [ ] **Step 3:** Ask Josef, then commit `docs: dream image done`.
