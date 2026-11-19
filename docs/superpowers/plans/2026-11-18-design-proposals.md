# Designförslag Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The customer states style, budget, care level and hardiness zone and gets two whole-garden proposals built
from stock articles, which they can try in 3D, undo, or choose.

**Architecture:** Plain C# in the Ai module divides the free ground into named zones with light, selects at most 150
fitting articles, and a layout engine turns Claude's building blocks into exact scene elements; a check (scene rules,
stock, zone, light, untouched existing things, budget) feeds errors back for at most two retries. A `design-proposal`
job makes two proposals; the editor gets a "Förslag" panel where "Prova" is one undoable command and "Välj det här"
freezes a design version.

**Tech Stack:** .NET 10, EF Core + PostgreSQL, Clipper2 (already in the Ai module), Anthropic C# SDK 13.9.0 with
structured output, React + React Three Fiber + zustand, vitest, xUnit v3.

**Spec:** [docs/superpowers/specs/2026-11-18-design-proposals-design.md](../specs/2026-11-18-design-proposals-design.md)

## Global Constraints

- Everything the user sees is Swedish; code, identifiers, comments and commits English (CLAUDE.md).
- Modules talk only through `src/Contracts` (tests/Architecture). Contracts gets no ASP.NET or EF references.
- Only the Worker calls Claude. Prices are computed only on the server.
- Hardiness: a plant fits when *article zone ≥ customer zone* (zones 1–8, higher is colder).
- At most 150 articles to the model. At most 8 zones. Two proposals per job: "Nära din stil" and "Lite djärvare".
- At most two retries per proposal (three calls), so at most six calls per job. Prompt version `design-proposal@1`.
  Model `Anthropic:Models:design-proposal` = `claude-sonnet-5-5`.
- Zones: house zones 4 m deep, boundary zones 3 m deep, zones under 4 m² merge into a neighbour, facades are house
  edges ≥ 2 m named by the nearest of four compass directions. Shade = north of the house within height × 1.5;
  sun = south and west of the house; part shade otherwise and within the crown of an existing tree.
- Margins around existing things that stay: plants half their width (default 1 m), surfaces 0.3 m.
- Grass split: pieces under 0.5 m² are dropped. The kept lawn keeps its id; removed pieces get new ids,
  status `existing-remove`, material grass.
- Budget ceilings (incl. work and VAT): `under-50k` 50 000, `50k-150k` 150 000, `150k-300k` 300 000, `over-300k` none.
  The price span's midpoint must be ≤ the ceiling; still over after retries → shown with "Över din budget".
- A proposal survives when at least half its blocks remain after dropping failed blocks and their dependents.
- Polling gives up after 8 minutes. Fallback copy: "Vi kunde inte ta fram förslag just nu. Designa vidare själv i katalogen."
- Ask Josef before every commit (memory rule).

## Review Focus

1. A garden with almost no free ground (tiny plot, big house, everything kept) must give a smaller proposal or the
   fallback, never an exception (Task 4 and Task 6 tests).
2. The model naming an article id that is not in the selection, or a zone key that does not exist, is a block error
   sent back on retry, not a crash (Task 5 test).
3. An existing tree or hedge the customer kept must stay exactly as it was, same id and position, in every proposal
   (Task 5 test).
4. "Prova" then "Ångra" must give back the exact scene from before, and "Välj det här" must only freeze a version after
   the proposal is saved (Task 7 test).
5. Another customer asking for a proposal job, or choosing one, gets 404 like a missing one (Task 6 test).

---

### Task 1: Contracts for the catalog selection and the price estimate

**Files:**
- Create: `src/Contracts/Catalog/ICatalogSelection.cs`, `src/Contracts/Pricing/IPriceEstimates.cs`,
  `src/Modules/Catalog/Features/Selection/CatalogSelection.cs`, `src/Modules/Pricing/Features/EstimatePrice/PriceEstimates.cs`
- Modify: `CatalogModule.cs`, `PricingModule.cs` (register), `src/Modules/Catalog/Features/ImportArticles/ArticleSheet.cs`
  (help text for the zone column), `docs/superpowers/specs/2026-11-14-catalog-design.md` (one line on the zone rule)
- Test: `tests/Integration/CatalogSelectionTests.cs`, `tests/Integration/PriceEstimatesTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  // Light is a list of "sun", "part-shade", "shade". Material is the surface material key ("grass", "paving", …) or null.
  public sealed record CatalogArticle(
      Guid Id, string Name, string Unit, Money Price, string TypeModel, string Family, string Geometry, string? Material,
      int? HardinessZone, double? Height, double? Width, double? MatureHeight, double? MatureWidth, IReadOnlyList<string> Light);
  public interface ICatalogSelection { Task<IReadOnlyList<CatalogArticle>> InStockAsync(CancellationToken cancellationToken); }
  public sealed record PriceSpan(Money Low, Money High) { public Money Middle { get; } }
  public interface IPriceEstimates { Task<PriceSpan> EstimateAsync(SceneDocument scene, CancellationToken cancellationToken); }
  ```
  `InStockAsync` returns articles with `StockStatus.InStock` only. `EstimateAsync` uses `ICatalogPrices`,
  `PriceSettingsStore` and `PriceCalculator.Estimate` exactly like `EstimatePrice` and returns `Total`.

- [ ] **Step 1: Write the failing tests:** `In_stock_lists_only_in_stock_articles_with_plant_fields` (import the example
  catalog, mark one out of stock, one discontinued; neither is listed; a plant has zone, mature width and light),
  `Estimate_matches_the_estimate_endpoint` (same scene through `/api/pricing/estimate` and `IPriceEstimates` gives the
  same total).
- [ ] **Step 2: Run and see them fail.** `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj -- --filter-class "*CatalogSelectionTests" --filter-class "*PriceEstimatesTests"`. Expected: compile errors, then FAIL.
- [ ] **Step 3: Implement** the contracts and both classes; register them; the import template's zone help text says
  "Kallaste zonen växten klarar, 1–8 (zon V = klarar zon I–V)."
- [ ] **Step 4: Run the whole .NET suite.** `dotnet test --solution GardenBuilder.slnx`. Expected: PASS.
- [ ] **Step 5: Commit** — `feat: catalog selection and price estimate contracts`.

### Task 2: Zones

**Files:**
- Create: `src/Modules/Ai/Features/DesignProposals/Zones.cs`
- Test: `tests/Unit/Ai/ZonesTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  internal enum ZoneLight { Sun, PartShade, Shade }
  internal enum ZoneKind { House, Boundary, Middle }
  /// <param name="Key">"house-south", "boundary-north", "middle" …</param>
  /// <param name="Name">Swedish, e.g. "söder om huset", "längs norra gränsen", "mitten".</param>
  internal sealed record Zone(string Key, string Name, ZoneKind Kind, string? Facing, IReadOnlyList<Point2[]> Outline, double Area, ZoneLight Light);
  internal static class Zones
  {
      /// <summary>Free ground: plot − house − kept things with margin; lawn and removed things count as free.</summary>
      public static IReadOnlyList<Point2[]> FreeGround(SceneDocument scene);
      public static IReadOnlyList<Zone> Divide(SceneDocument scene);
      public static string Facing(Point2 from, Point2 to); // outward normal of a counter-clockwise edge → "north" | "south" | "east" | "west"
  }
  ```
  Use Clipper2 (`PathsD`, precision 3). Kept things: every element except the plot, the house, grass surfaces and
  `existing-remove` elements; points by a circle of half their width (default 1 m) as a 16-gon, lines buffered 0.3 m,
  surfaces grown 0.3 m.

- [ ] **Step 1: Write the failing tests** on a 20 × 20 plot with a 10 × 6 house in the north half:
  `A_house_gives_four_house_zones_named_by_compass` (keys `house-north/south/east/west`, names "norr om huset" …),
  `South_of_the_house_is_sun_and_north_is_shade`, `Boundary_zones_are_three_meters_deep_and_named_by_side`,
  `The_rest_is_the_middle`, `At_most_eight_zones`, `Zones_under_four_square_meters_merge_into_a_neighbour`,
  `A_kept_tree_is_cut_out_with_half_its_width`, `A_removed_hedge_is_free_ground`, `Lawn_is_free_ground`,
  `Part_shade_under_a_kept_tree`, `An_L_shaped_house_has_one_zone_per_compass_direction`,
  `A_plot_full_of_kept_things_gives_no_zones` (no exception).
- [ ] **Step 2: Run and see them fail.** `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj -- --filter-class "*ZonesTests"`. Expected: FAIL.
- [ ] **Step 3: Implement** `Zones`.
- [ ] **Step 4: Run.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(ai): divide the garden into zones`.

### Task 3: Wishes and article selection

**Files:**
- Create: `src/Modules/Ai/Features/DesignProposals/ProposalWishes.cs`, `ArticleSelection.cs`
- Test: `tests/Unit/Ai/ArticleSelectionTests.cs`

**Interfaces:**
- Consumes: `CatalogArticle` (Task 1), `Zone` (Task 2).
- Produces:
  ```csharp
  /// <param name="Style">"natural" | "modern" | "classic" | "rural".</param>
  /// <param name="Budget">"under-50k" | "50k-150k" | "150k-300k" | "over-300k".</param>
  /// <param name="Care">"low" | "medium" | "high".</param>
  /// <param name="Note">At most 300 characters.</param>
  internal sealed record ProposalWishes(string Style, string Budget, string Care, int Zone, string? Note)
  {
      public IReadOnlyList<string> Problems();   // Swedish, empty when valid
      public decimal? Ceiling { get; }           // 50 000 / 150 000 / 300 000 / null
  }
  internal static class ArticleSelection
  {
      public const int Max = 150;
      public static IReadOnlyList<CatalogArticle> Select(IReadOnlyList<CatalogArticle> inStock, int customerZone, IReadOnlyList<Zone> zones);
  }
  ```
  Non-plants (surfaces, fences, furniture, pots, rocks) are always kept; plants need `HardinessZone ≥ customerZone`;
  plants are ranked by how many zones' light they suit (most first, then name) and cut so the total is ≤ 150.

- [ ] **Step 1: Write the failing tests:** `Zone_v_plant_fits_zone_three_but_not_zone_six`, `Surfaces_are_always_offered`,
  `Never_more_than_150`, `Plants_that_suit_more_zones_come_first`, `Out_of_range_wishes_are_named_in_swedish`
  (style "gothic" → "Välj en stil i listan.", zone 9 → "Växtzonen ska vara mellan 1 och 8.", 301-character note →
  "Önskemålet får vara högst 300 tecken."), `Ceilings_are_50_150_300_and_none`.
- [ ] **Step 2: Run and see them fail.** `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj -- --filter-class "*ArticleSelectionTests"`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(ai): proposal wishes and article selection`.

### Task 4: Layout engine

**Files:**
- Create: `src/Modules/Ai/Features/DesignProposals/Blocks.cs`, `LayoutEngine.cs`, `LawnSplit.cs`
- Test: `tests/Unit/Ai/LayoutEngineTests.cs`, `tests/Unit/Ai/LawnSplitTests.cs`

**Interfaces:**
- Consumes: `Zone`, `ZoneLight` (Task 2), `CatalogArticle` (Task 1).
- Produces:
  ```csharp
  /// <param name="Type">"patio" | "path" | "gravel" | "bed" | "shrubs" | "hedge" | "tree" | "fruit-trees" | "lawn" | "furniture".</param>
  internal sealed record Block(
      string Type, string? Zone, double? Area, double? Length, double? Depth, double? Width, string? Side,
      string? From, string? To, IReadOnlyList<Guid>? Articles, int? Count);
  internal sealed record BlockError(int Block, string Message);    // Message in Swedish, for the model and the logs
  internal sealed record Layout(SceneDocument Scene, IReadOnlyList<BlockError> Errors, IReadOnlyList<int> Placed);
  internal static class LayoutEngine
  {
      public static Layout Build(SceneDocument current, IReadOnlyList<Zone> zones, IReadOnlyList<Block> blocks, IReadOnlyDictionary<Guid, CatalogArticle> articles);
  }
  internal static class LawnSplit
  {
      /// <summary>Cuts the used area out of existing lawns: kept part keeps its id, cut pieces become existing-remove.</summary>
      public static IReadOnlyList<SceneElement> Apply(IReadOnlyList<SceneElement> elements, IReadOnlyList<Point2[]> used);
  }
  ```
  Rules per block are the spec's table, section 3. New elements: status `new`, source `estimated`, the block's article.
  Blocks are placed in the given order; a block that does not fit gets a `BlockError` such as
  `"Uteplatsen på 40 m² får inte plats söder om huset, högst 22 m²."` and places nothing. New surfaces never overlap
  each other or kept things; plants are never placed on a patio, path or gravel. Furniture needs a placed patio;
  a path's `From` may be `"patio"`. Every element the current scene has is copied unchanged (same id), except lawns,
  which go through `LawnSplit`.

- [ ] **Step 1: Write the failing tests** (20 × 20 plot, 10 × 6 house, a lawn over the south half, a kept birch):
  `Patio_lies_against_the_south_facade_with_the_asked_area` (area ± 0.5 m², touches the facade),
  `Too_big_a_patio_is_an_error_with_the_largest_area_that_fits`, `Path_runs_from_the_patio_to_a_zone_without_crossing_the_birch`,
  `Bed_plants_keep_their_mature_width_apart` (no two closer than (wA + wB)/2 × 0.7),
  `Hedge_follows_the_boundary_half_a_meter_inside`, `Tree_keeps_half_its_crown_from_everything`,
  `Fruit_trees_stand_four_meters_apart`, `Furniture_without_a_patio_is_an_error`,
  `Unknown_zone_or_article_is_an_error_not_an_exception`, `Kept_things_are_copied_unchanged`,
  `Nothing_new_overlaps_the_birch`, `A_garden_without_free_ground_places_nothing_and_does_not_throw`.
  In `LawnSplitTests`: `Cut_part_becomes_existing_remove_and_kept_part_keeps_its_id`, `Pieces_under_half_a_square_meter_are_dropped`,
  `A_lawn_cut_in_two_gives_two_kept_lawns`, `A_lawn_not_touched_is_unchanged`.
- [ ] **Step 2: Run and see them fail.** `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj -- --filter-class "*LayoutEngineTests" --filter-class "*LawnSplitTests"`. Expected: FAIL.
- [ ] **Step 3: Implement** `LawnSplit` first, then `LayoutEngine` block by block, each test going green in turn.
  Paths: a straight line between the two zones' centres, bent once around a blocked area if it crosses one; buffered to
  the path width as a surface.
- [ ] **Step 4: Run.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(ai): layout engine for proposal blocks`.

### Task 5: The proposal check

**Files:**
- Create: `src/Modules/Ai/Features/DesignProposals/ProposalCheck.cs`
- Test: `tests/Unit/Ai/ProposalCheckTests.cs`

**Interfaces:**
- Consumes: `Layout`, `Block`, `BlockError` (Task 4), `ProposalWishes` (Task 3), `PriceSpan` (Task 1), `SceneProblem`.
- Produces:
  ```csharp
  internal sealed record CheckedProposal(SceneDocument Scene, IReadOnlyList<BlockError> Errors, bool OverBudget);
  internal static class ProposalCheck
  {
      /// <summary>All errors: the layout's, scene rules, stock, zone, light, kept things changed, budget.</summary>
      public static IReadOnlyList<BlockError> Errors(SceneDocument current, Layout layout, IReadOnlyList<Block> blocks,
          IReadOnlyDictionary<Guid, CatalogArticle> selection, IReadOnlyList<Zone> zones, ProposalWishes wishes,
          IReadOnlyList<SceneProblem> sceneProblems, PriceSpan price);
      /// <summary>Drops failed blocks and those that depend on them; null when fewer than half remain.</summary>
      public static IReadOnlyList<Block>? WithoutFailed(IReadOnlyList<Block> blocks, IReadOnlyList<BlockError> errors);
  }
  ```
  Budget errors use block −1 and read "Förslaget kostar cirka 210 000 kr men budgeten är högst 150 000 kr."

- [ ] **Step 1: Write the failing tests:** `Article_outside_the_selection_is_an_error`, `Plant_too_tender_for_the_zone_is_an_error`,
  `Sun_plant_in_a_shade_zone_is_an_error`, `A_moved_or_missing_kept_element_is_an_error`, `Over_budget_is_an_error_with_both_amounts`,
  `No_ceiling_means_no_budget_error`, `Scene_problems_point_at_the_block_that_made_the_element`,
  `Dropping_a_patio_drops_its_path_and_furniture`, `Fewer_than_half_left_gives_null`.
- [ ] **Step 2: Run and see them fail.** `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj -- --filter-class "*ProposalCheckTests"`. Expected: FAIL.
- [ ] **Step 3: Implement.** The layout returns which block made each new element (extend `Layout` with
  `IReadOnlyDictionary<Guid, int> MadeBy` if needed and note it in the ledger).
- [ ] **Step 4: Run.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(ai): check design proposals`.

### Task 6: The design proposal job and its endpoints

**Files:**
- Create in `src/Modules/Ai/Features/DesignProposals/`: `ProposalPrompt.cs`, `DesignProposalHandler.cs`, `DesignProposalEndpoints.cs`
- Modify: `AiJobProcessingExtensions.cs`, `AiModule.cs`, `src/Worker/Program.cs` (add `AddCatalogModule()` and
  `AddPricingModule()`), `src/Worker/*.csproj`, both `appsettings.json` (`Models:design-proposal`), `tests/Integration/AiJobRunnerTests.cs`
  (its stand-in handler now uses the dream-image kind)
- Test: `tests/Integration/DesignProposalTests.cs`

**Interfaces:**
- Consumes: everything above, `IDesignWorkingCopies`, `IDesignVersions`, `ISceneCheck`, `IAiJobs`.
- Produces: job input `{ designId, wishes }`; result
  `{ proposals: [{ name, motivation, scene, low, high, overBudget, dropped: [..] }], zones: [..] }` (scene through
  `SceneJson.Options`, enums as kebab-case names). Endpoints, all owner-checked (404 otherwise):
  `POST /api/gardens/{gardenId}/design-proposals` body `{ designId, style, budget, care, zone, note }` → 202 `{ id }`;
  400 `{ code: "invalid-wishes", problems: [..] }`; 409 with the queue's codes.
  `GET .../design-proposals/{jobId}` and `GET .../design-proposals/latest` → `{ id, status, result, failure }`.
  `POST .../design-proposals/{jobId}/chosen` body `{ designId, index }` → 204 after `IDesignVersions.CreateAsync(designId, AiProposal)`.
- Handler: zones and selection once; then per proposal: call (prompt + zones + selection + wishes + previous errors),
  parse blocks, layout, check; retry with the errors at most twice; then `WithoutFailed`, rebuild and recheck once
  (no call). A proposal that is null is left out. Both left out → `AiProviderException("Vi kunde inte ta fram förslag just nu.", retryable: false)`.
  A guard stopping mid-job keeps a finished first proposal (return it) instead of failing.

- [ ] **Step 1: Write the failing tests** with a fake model returning canned block lists:
  `Two_valid_proposals_are_stored_with_price_and_the_design_is_unchanged`,
  `An_invalid_proposal_is_retried_with_the_errors_and_passes`, `Three_invalid_answers_drop_that_proposal_only`,
  `Both_proposals_failing_fails_the_job_in_swedish`, `Over_budget_after_retries_is_kept_and_flagged`,
  `Invalid_wishes_are_400_with_swedish_problems`, `Sixth_round_is_409_quota_reached`,
  `Latest_is_fetched_again_and_another_customer_gets_404`, `Choosing_freezes_a_version`, `Choosing_someone_elses_is_404`,
  `Proposal_uses_the_model_for_its_kind`.
- [ ] **Step 2: Run and see them fail.** `dotnet test --project tests/Integration/GardenBuilder.IntegrationTests.csproj -- --filter-class "*DesignProposalTests"`. Expected: compile errors, then FAIL.
- [ ] **Step 3: Implement** prompt (Swedish wishes, zones with name/area/light, the selection as id, name, type,
  sizes, light, price; the block types and their parameters; for "Lite djärvare" one extra paragraph asking for more
  variation within the same limits) and schema, handler, endpoints, registration, settings.
- [ ] **Step 4: Run the whole .NET suite.** `dotnet test --solution GardenBuilder.slnx`. Expected: PASS.
- [ ] **Step 5: Commit** — `feat(ai): design proposal job`.

### Task 7: The "Förslag" panel

**Files:**
- Create: `src/web/src/api/proposals.ts`, `proposals.test.ts`, `src/web/src/editor/proposals/ProposalPanel.tsx`,
  `proposalStore.ts`, `proposalStore.test.ts`
- Modify: `src/web/src/editor/sceneStore.ts` (command `{ type: 'replace-scene'; document: SceneDocument }`),
  `EditorPage.tsx` (panel switch gets "Förslag"), `editor.css`

**Interfaces:**
- Consumes: Task 6 endpoints.
- Produces: `proposalStore` with `wishes`, `interpret`-like `state: 'idle' | 'running' | 'done' | 'failed'`, `proposals`,
  `tried: number | null`, `start()`, `tryProposal(index)`, `choose(index)`, `load(gardenId)`.

- [ ] **Step 1: Write the failing tests:** `replace-scene and undo give back the exact scene before`,
  `tryProposal replaces the scene as one undo step`, `choose waits for the save and then posts chosen`,
  `a finished round is shown again on load`, `gives up after eight minutes with the fallback`,
  `invalid-wishes shows the server's problems`.
- [ ] **Step 2: Run and see them fail.** `npm --prefix src/web test -- proposals proposalStore`. Expected: FAIL.
- [ ] **Step 3: Implement.** Form: Stil (Naturlig/Modern/Klassisk/Lantlig), Budget (Under 50 000 kr / 50 000–150 000 kr /
  150 000–300 000 kr / Över 300 000 kr), Skötsel (Låg/Medel/Hög), Växtzon (I–VIII, help "Zon I är mildast, i Skåne;
  zon VIII är kallast, i fjällen."), Önskemål (textarea, 300). Button "Ge mig förslag"; "Tar fram förslag …".
  Cards: name, motivation, "cirka X–Y kr", "Över din budget" when flagged, buttons "Prova" and "Välj det här".
  Fallback copy from Global Constraints.
- [ ] **Step 4: Run** web tests, lint, build. Start the app with the fake answers disabled (no Claude call) and check
  the form and the fallback.
- [ ] **Step 5: Commit** — `feat(editor): design proposal panel`.

### Task 8: Real run and docs

- [ ] **Step 1: Ask Josef before any real Claude call.** With his yes, run one round on his test garden; note time,
  cost and how the two proposals look (screenshots).
- [ ] **Step 2: Update** `docs/design.md` ("Så blir designförslagen giltiga": link to the spec, model), roadmap row 9
  ("Klar" with links), `evals/README.md` (how to rerun a proposal round).
- [ ] **Step 3: Run everything.** `dotnet test --solution GardenBuilder.slnx`, `npm --prefix src/web test`, lint, build. Expected: PASS.
- [ ] **Step 4: Commit** — `docs: design proposals done`.
