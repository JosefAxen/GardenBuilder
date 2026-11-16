# Trädgård och design Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The customer's design is saved on the server a second after every change, protected against two tabs overwriting each other, and opened again on return.

**Architecture:** Garden owns `gardens`; Design owns `designs` (working copy with a revision) and `design_versions`. The editor loads the latest design through `GET /api/designs/latest`, creates one with `POST` on the first change, and saves with `PUT … If-Match: "<revision>"`. A separate `saveStore` in the web debounces, serialises and retries saves.

**Tech Stack:** .NET 10, EF Core with Npgsql (jsonb), minimal APIs, xUnit v3 with Testcontainers, React, zustand, vitest.

**Spec:** docs/superpowers/specs/2026-11-16-garden-design-design.md

## Global Constraints

- Modules never reference each other; Design reaches Garden only through `IGardens` in `src/Contracts`.
- Contracts reference neither ASP.NET Core nor EF Core.
- Tables and columns snake_case; ids from `Ids.New()` (UUID v7); times UTC from `TimeProvider`.
- Migrations only via `dotnet ef migrations add … --output-dir Infrastructure/Migrations`, applied by `src/Migrator`.
- Every scene that is stored is read by `SceneJson.Deserialize` and checked by `ISceneCheck`; body limit 1 MB (413).
- A design that belongs to someone else answers exactly like one that does not exist: 404.
- Copy shown to the customer, verbatim: "Sparad", "Sparar…", "Inte sparad, försöker igen…", "Designen kunde inte
  sparas.", "Designen har ändrats i en annan flik.", "Hämta den senaste", "Behåll min", "Hämtar din design…",
  "Det gick inte att hämta din design. Försöker igen…".
- Autosave 1 000 ms after the last change; retries after 2, 4, 8, 16, 32 s, then every 60 s.
- `keepalive` only for bodies under 60 000 bytes.
- Warnings are errors; package versions only in `Directory.Packages.props`.

## Review Focus

1. **Two tabs on the same design.** The older tab's save must get 412 and show the conflict box, never overwrite
   silently. Tests: `Stale_revision_is_412_with_current_revision` (Task 3), `conflict stops saving until the customer
   chooses` (Task 4).
2. **A change while a save is on its way.** It must be saved right after, with the revision the first save returned,
   and must not conflict with itself. Test: `a change during a save is saved after it with the new revision` (Task 4).
3. **Another customer, or no cookie, asking for or saving a design id.** 404, and the design is unchanged. Tests:
   `Other_customer_gets_404_and_design_is_unchanged`, `No_cookie_gets_404` (Task 3).
4. **The server is unreachable while the customer edits.** Status "Inte sparad, försöker igen…", retries with backoff,
   nothing lost, saved when the server is back. Test: `retries with backoff and saves when the server is back` (Task 4).
5. **Two quick first changes before the first POST answers.** One design, not two. Test: `the first changes create one
   design` (Task 4).

---

### Task 0: Start from main with pricing and identity

PR #5 and PR #6 are merged into `main` by Josef first; this task does not merge pull requests.

- [ ] **Step 1:** `git fetch origin` and `git merge origin/main` into `feat/garden`.
- [ ] **Step 2: Run** `dotnet test --solution GardenBuilder.slnx` and `npm --prefix src/web test`. Expected: PASS
  (the sum of both PRs).

### Task 1: Gardens

**Files:**
- Create: `src/Contracts/Gardens/IGardens.cs`, `src/Modules/Garden/Domain/Garden.cs`,
  `src/Modules/Garden/Infrastructure/GardenConfiguration.cs`, `src/Modules/Garden/Features/CreateGarden/Gardens.cs`
- Modify: `src/Modules/Garden/Infrastructure/GardenDbContext.cs`, `src/Modules/Garden/GardenModule.cs`
- Migration: `AddGardens` (context `GardenDbContext`)
- Test: `tests/Integration/GardensTests.cs`

**Interfaces:**
- Produces: `public interface IGardens { Task<Guid> CreateAsync(Guid customerId, CancellationToken cancellationToken); }`
- Produces: `internal sealed class Garden { Guid Id; Guid CustomerId; DateTimeOffset CreatedAt; static Garden Create(Guid customerId, DateTimeOffset now); }` → table `garden.gardens`, index on `customer_id`.

- [ ] **Step 1: Write the failing test** `Create_saves_a_garden_for_the_customer`: `CreateAsync(customerId)` returns an id;
  reading `GardenDbContext.Gardens` finds one row with that id, that customer and `CreatedAt` within a second of now.
- [ ] **Step 2: Run** `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj --filter-class "*GardensTests"`. Expected: FAIL (does not compile: `IGardens` missing).
- [ ] **Step 3:** Implement entity, configuration, `Gardens : IGardens` (scoped), register in `AddGardenModule`, add
  migration `AddGardens`.
- [ ] **Step 4: Run** the same command. Expected: PASS. Then `dotnet test --project tests/Architecture/GardenBuilder.ArchitectureTests.csproj`. Expected: PASS.
- [ ] **Step 5: Commit (after Josef's yes)** `"Add gardens"`.

### Task 2: Designs, working copy and versions

**Files:**
- Create: `src/Contracts/Designs/IDesignVersions.cs`, `src/Modules/Design/Domain/Design.cs`,
  `src/Modules/Design/Domain/DesignVersion.cs`, `src/Modules/Design/Infrastructure/DesignConfiguration.cs`,
  `src/Modules/Design/Features/WorkingCopy/WorkingCopyStore.cs`, `src/Modules/Design/Features/Versions/DesignVersions.cs`
- Modify: `src/Modules/Design/Infrastructure/DesignDbContext.cs`, `src/Modules/Design/DesignModule.cs`
- Migration: `AddDesigns` (context `DesignDbContext`)
- Test: `tests/Integration/DesignStoreTests.cs`

**Interfaces:**
- Consumes: `SceneJson.Serialize/Deserialize`, `SceneDocument` (Contracts).
- Produces (Contracts):
  - `public enum DesignVersionReason { AiProposal, QuoteRequest, StaffRevision }` (stored as `ai-proposal`, `quote-request`, `staff-revision`)
  - `public sealed record DesignVersionRef(Guid Id, int Number);`
  - `public interface IDesignVersions { Task<DesignVersionRef?> CreateAsync(Guid designId, DesignVersionReason reason, CancellationToken cancellationToken); Task<SceneDocument?> GetAsync(Guid versionId, CancellationToken cancellationToken); }` — null when the design or version does not exist.
- Produces (module, internal):
  - `Design { Guid Id; Guid GardenId; Guid CustomerId; string WorkingCopy (jsonb); int Revision; DateTimeOffset CreatedAt; DateTimeOffset UpdatedAt; static Design Create(Guid gardenId, Guid customerId, SceneDocument scene, DateTimeOffset now) }` — revision starts at 1. Table `design.designs`, index `(customer_id, updated_at)`.
  - `DesignVersion { Guid Id; Guid DesignId; int Number; string Scene (jsonb); DesignVersionReason Reason; DateTimeOffset CreatedAt }` — table `design.design_versions`, unique `(design_id, number)`. No setters after creation.
  - `WorkingCopyStore` (scoped):
    - `Task<Design> CreateAsync(Guid gardenId, Guid customerId, SceneDocument scene, CancellationToken ct)`
    - `Task<Design?> FindAsync(Guid id, Guid customerId, CancellationToken ct)` — null for another owner.
    - `Task<Design?> LatestAsync(Guid customerId, CancellationToken ct)` — highest `UpdatedAt`.
    - `Task<SaveResult> SaveAsync(Guid id, Guid customerId, int expectedRevision, SceneDocument scene, CancellationToken ct)`
    - `abstract record SaveResult { Saved(int Revision); NotFound; Conflict(int CurrentRevision) }`
  - `SaveAsync` is one `ExecuteUpdateAsync` where id, customer and revision match, setting the scene, `Revision + 1` and `UpdatedAt`; zero rows → read the row to tell `NotFound` (missing or other owner) from `Conflict`.

- [ ] **Step 1: Write the failing tests:**
  - `Save_with_current_revision_bumps_it`: create (revision 1) → `SaveAsync(…, 1, scene2)` is `Saved(2)`, and `FindAsync` returns scene2 and revision 2.
  - `Save_with_old_revision_is_conflict`: after one save, `SaveAsync(…, 1, …)` is `Conflict(2)` and the stored scene is unchanged.
  - `Save_by_other_customer_is_not_found`: `SaveAsync(id, otherCustomer, 1, …)` is `NotFound`; scene unchanged.
  - `Latest_is_the_most_recently_saved`: two designs for one customer, save the first → `LatestAsync` returns the first.
  - `Version_freezes_the_working_copy`: `CreateAsync(designId, QuoteRequest)` is `(id, 1)`, a second is number 2; saving the working copy afterwards leaves `GetAsync(firstVersionId)` with the old scene.
  - `Version_of_missing_design_is_null`.
- [ ] **Step 2: Run** `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj --filter-class "*DesignStoreTests"`. Expected: FAIL (does not compile).
- [ ] **Step 3:** Implement entities, configuration (enum as kebab string), store, `DesignVersions : IDesignVersions`
  (number = highest + 1; a unique-index violation from a parallel call is retried once), registration, migration `AddDesigns`.
- [ ] **Step 4: Run** the same command. Expected: PASS.
- [ ] **Step 5: Commit (after Josef's yes)** `"Add designs with working copy and versions"`.

### Task 3: Design endpoints

**Files:**
- Create: `src/Modules/Design/Features/WorkingCopy/SceneBody.cs` (reads the body as in `EstimatePrice`: 1 MB, `SceneJson`, `ISceneCheck`),
  `src/Modules/Design/Features/WorkingCopy/DesignEndpoints.cs`
- Modify: `src/Modules/Design/DesignModule.cs` (map the group `/api/designs`)
- Test: `tests/Integration/DesignEndpointTests.cs`

**Interfaces:**
- Consumes: `WorkingCopyStore` (Task 2), `IGardens` (Task 1), `ICurrentCustomer` (`FindAsync`, `GetOrCreateAsync`).
- Produces (HTTP):
  - `DesignResponse(Guid Id, int Revision, JsonElement Scene)`; `ETag: "<revision>"` on every 200/201.
  - `GET /api/designs/latest` → 200 | 404. Uses `FindAsync`; never creates a customer.
  - `GET /api/designs/{id:guid}` → 200 | 404.
  - `POST /api/designs` (body: scene) → 201 `Location: /api/designs/{id}` | 400 `unreadable-scene` / `invalid-scene` + problems | 413. Checks the scene before `GetOrCreateAsync` and `IGardens.CreateAsync`.
  - `PUT /api/designs/{id:guid}/working-copy` (body: scene, `If-Match: "<n>"`) → 200 | 400 | 404 | 412 `{ "code": "conflict", "revision": n }` | 413 | 428 `{ "code": "revision-required" }` (missing or unreadable `If-Match`; `W/` and quotes are accepted).

- [ ] **Step 1: Write the failing tests** (an https client keeps the customer cookie, as in `CurrentCustomerTests`):
  - `First_post_creates_customer_garden_and_design`: POST → 201, `ETag` `"1"`, a `gb-customer` cookie; `GET latest` with the cookie → same id, revision 1, the scene.
  - `Latest_without_cookie_is_404_and_creates_no_customer`: count of `identity.customers` unchanged.
  - `Put_with_current_revision_saves`: PUT `If-Match: "1"` → 200, `ETag` `"2"`; GET returns the new scene.
  - `Stale_revision_is_412_with_current_revision`: two PUTs with `"1"` → the second is 412 with `revision: 2`.
  - `Missing_if_match_is_428`.
  - `Other_customer_gets_404_and_design_is_unchanged`: a second client (own cookie) GETs and PUTs the first client's id → 404 both; the first client's GET still shows revision 1.
  - `No_cookie_gets_404`: GET and PUT on an existing id without cookie → 404.
  - `Invalid_scene_is_400_and_creates_nothing`: POST a scene with a point outside the plot → 400 `invalid-scene`; no garden row.
  - `Scene_over_1_MB_is_413`.
- [ ] **Step 2: Run** `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj --filter-class "*DesignEndpointTests"`. Expected: FAIL.
- [ ] **Step 3:** Implement `SceneBody` and the four handlers.
- [ ] **Step 4: Run** the same command. Expected: PASS. Then the whole solution: `dotnet test --solution GardenBuilder.slnx`. Expected: PASS.
- [ ] **Step 5: Commit (after Josef's yes)** `"Add design endpoints with optimistic locking"`.

### Task 4: Saving in the web

**Files:**
- Create: `src/web/src/api/designs.ts`, `src/web/src/api/designs.test.ts`, `src/web/src/editor/saveStore.ts`, `src/web/src/editor/saveStore.test.ts`
- Modify: `src/web/src/editor/sceneStore.ts`, `src/web/src/editor/sceneStore.test.ts`

**Interfaces:**
- Consumes: the HTTP contract of Task 3.
- Produces (`api/designs.ts`):
  - `type SavedDesign = { id: string; revision: number; scene: SceneDocument }`
  - `class DesignError extends Error { reason: 'conflict' | 'not-found' | 'invalid' | 'unavailable'; revision?: number }` (412 → conflict with `revision`; 404 → not-found; 400/413 → invalid; network, timeout, 5xx → unavailable)
  - `fetchLatestDesign(): Promise<SavedDesign | null>` (404 → null), `fetchDesign(id): Promise<SavedDesign>`,
    `createDesign(scene): Promise<{ id: string; revision: number }>`,
    `saveWorkingCopy(id, revision, scene, { keepalive? }): Promise<number>` (new revision from `ETag`). All with `credentials: 'same-origin'` and a 10 s timeout.
- Produces (`sceneStore`): `load(document: SceneDocument): void` — replaces the document and clears `past`, `future`, `selectedId`, `drag`, `notice`; not an undo step.
- Produces (`saveStore`):
  - `type SaveStatus = 'idle' | 'pending' | 'saving' | 'saved' | 'retrying' | 'conflict' | 'invalid'`
  - `useSaveStore` with `{ status, designId: string | null, revision: number, conflictRevision: number | null }`
  - `startSaving(saved: SavedDesign | null, shown: SceneDocument): void` — the document now shown counts as saved.
  - `scheduleSave(document: SceneDocument): void` — ignored when it is the document last saved or loaded (same object).
  - `takeLatest(): Promise<void>` (fetches, `load`s, status `saved`), `keepMine(): void` (saves with `conflictRevision`).
  - `saveOnLeave(document: SceneDocument): void` (sends with `keepalive` when the JSON is under 60 000 bytes), `hasUnsavedChanges(): boolean`.
  - `statusText(status): string` with the copy from Global Constraints (`idle` → `''`; `pending` and `saving` → "Sparar…").

- [ ] **Step 1: Write the failing tests** (vitest fake timers; `fetch` stubbed as in `priceStore.test.ts`):
  - `api/designs.test.ts`: 404 on latest → null; 412 → `DesignError('conflict')` with `revision`; `ETag "7"` → 7; `If-Match` is `"3"`; network error → `unavailable`.
  - `sceneStore.test.ts`: `load` empties undo and redo, and `undo()` afterwards changes nothing.
  - `saveStore.test.ts`:
    - `nothing is sent before the first change`.
    - `saves one second after the last change` (three changes 300 ms apart → one PUT, 1 000 ms after the last).
    - `the first changes create one design` (change, POST pending, change again → one POST, then one PUT with revision 1).
    - `a change during a save is saved after it with the new revision` (PUT with `"1"` pending, change → after it answers `"2"`, a PUT with `"2"`).
    - `retries with backoff and saves when the server is back` (two failures → status `retrying`, attempts at +2 s and +4 s, then `saved`).
    - `conflict stops saving until the customer chooses` (412 → `conflict`, later changes send nothing; `keepMine` → PUT with `If-Match` = conflict revision).
    - `takeLatest loads the server's design` (`sceneStore.document` becomes the fetched scene, status `saved`).
    - `invalid is not retried` (400 → `invalid`, no request after 60 s; the next change sends again).
    - `not-found starts a new design on the next save`.
    - `saveOnLeave uses keepalive only under 60 000 bytes`.
- [ ] **Step 2: Run** `npm --prefix src/web test`. Expected: FAIL (modules missing).
- [ ] **Step 3:** Implement the API client, `load` and `saveStore`.
- [ ] **Step 4: Run** `npm --prefix src/web test`, `npm --prefix src/web run lint`, `npm --prefix src/web run build`. Expected: PASS, clean.
- [ ] **Step 5: Commit (after Josef's yes)** `"Save the design from the editor"`.

### Task 5: The editor opens and saves the design

**Files:**
- Create: `src/web/src/editor/SaveStatus.tsx` (status text, conflict box with the two buttons)
- Modify: `src/web/src/editor/EditorPage.tsx`, `src/web/src/editor/editor.css`

**Interfaces:**
- Consumes: `fetchLatestDesign`, `useSceneStore.load`, `startSaving`, `scheduleSave`, `saveOnLeave`, `hasUnsavedChanges`, `takeLatest`, `keepMine`, `statusText` (Task 4).

- [ ] **Step 1:** On mount: `fetchLatestDesign()` → `load(scene)` and `startSaving(saved, scene)`, or on null `startSaving(null, demoScene)`. Until it has answered, the editor shows "Hämtar din design…" over the scene and saves nothing (otherwise a change would create a second design next to the customer's real one). On `unavailable` the text becomes "Det gick inte att hämta din design. Försöker igen…" and the call is retried after 2, 4, 8 … s, at most 60.
- [ ] **Step 2:** Subscribe to `useSceneStore` document changes → `scheduleSave(document)`. `pagehide` → `saveOnLeave`; `beforeunload` → `preventDefault()` while `hasUnsavedChanges()`.
- [ ] **Step 3:** `SaveStatus` in the status bar; the conflict box shows "Designen har ändrats i en annan flik." with "Hämta den senaste" and "Behåll min".
- [ ] **Step 4: Run** `npm --prefix src/web test`, lint and build. Expected: PASS, clean.
- [ ] **Step 5: Check in the browser** (`preview_start` "aspire"):
  - A first visit shows the demo scene and no request to `/api/designs` other than `latest` (404).
  - Moving a plant → "Sparar…" then "Sparad"; reload → the moved plant is still there and undo is empty.
  - Two tabs: change in tab A, then in tab B → tab B shows the conflict box; "Hämta den senaste" shows tab A's change; "Behåll min" saves tab B's.
  - Stop the API in the dashboard, change something → "Inte sparad, försöker igen…"; start it → "Sparad".
  - No console errors.
- [ ] **Step 6: Commit (after Josef's yes)** `"Open and autosave the design in the editor"`.

### Task 6: Docs

**Files:**
- Modify: `docs/design.md` ("Regler för data": the revision and `If-Match`; "Editorns tillstånd": the save status and conflict box), `docs/superpowers/plans/2026-11-13-roadmap.md` (row 4 done with a link to the spec; under "Att göra för dig" nothing new).

- [ ] **Step 1:** Update both documents.
- [ ] **Step 2: Run** `dotnet test --solution GardenBuilder.slnx` and `npm --prefix src/web test`. Expected: PASS.
- [ ] **Step 3: Commit (after Josef's yes)** `"Update design and roadmap for saving designs"`.
- [ ] **Step 4:** Ask Josef about push and PR.
