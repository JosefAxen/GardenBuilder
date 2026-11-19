# Plantskolan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the whole web app the Plantskolan look: a token-based design system with Familjen Grotesk, Lucide icons
and ten shared components, a rebuilt editor (fir frame, warm scene window, floating tool rail, plant label, catalogue
shelf with search, view switch), warm scene lighting, and restyled admin, staff login and start page.

**Architecture:** Plain CSS custom properties in `src/web/src/ui/tokens.css`, small React components in
`src/web/src/ui/`, existing per-area CSS files rewritten against the tokens. Editor-only UI state (open panel, price
details, catalogue query and category) lives in a new zustand store `editorUiStore`; scene state stays in `sceneStore`.
Pure helpers (shelf filtering, label facts, thumbnail keys, contrast) carry the logic and the unit tests; components
get Testing Library tests where they have behaviour.

**Tech Stack:** React 19, TypeScript 7, Vite 8, zustand 5, React Three Fiber 9 / three 0.187, vitest 5,
`@testing-library/react`, `@testing-library/user-event`, jsdom, `@fontsource-variable/familjen-grotesk` 5.3.0,
`lucide-react` 1.76.0.

**Spec:** [docs/superpowers/specs/2026-11-19-plantskolan-design-system-design.md](../specs/2026-11-19-plantskolan-design-system-design.md)
(reference image and HTML in `docs/superpowers/specs/assets/2026-11-19-plantskolan-editor.*`)

## Global Constraints

- Everything the user sees is Swedish; code, identifiers, comments and commits English (CLAUDE.md).
- Token values are the spec's table, verbatim. Components and page CSS use tokens only, never raw colour values
  (exception: the scene's three.js colours in `SceneCanvas.tsx`, which read the same hex values from `ui/palette.ts`).
- Contrast pairs that must hold ≥ 4.5:1 (measured): ink/ink-2/ink-3 on linen; ink-2/ink-3 on linen-2; on-fir,
  on-fir-2, on-fir-3 on fir; white on terra; terra-ink on terra-soft; on-fir on cut. **`--on-fir-3` is only allowed on
  `--fir`** (on `--fir-2` it is 4.28:1).
- Fonts and icons are bundled by Vite; nothing is fetched from a third party at runtime (no Google Fonts).
- No new editor functions except catalogue search and the Ovanifrån/3D switch. Every existing control keeps working,
  including keyboard shortcuts Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, Delete/Backspace, Q, E, Esc.
- No dummy controls: no step bar, no "Begär offert", no zoom buttons, no address.
- Visible focus ring (2 px terra, 2 px offset) on everything focusable; `prefers-reduced-motion` turns transitions off.
- Prices: only what the server sends, formatted with the existing `formatRange` / `Intl.NumberFormat('sv-SE')`.
- 60 fps with 300 plants ("Lägg till 300 växter", "Mät prestanda") must still hold; measure before and after.
- Package versions: the web app's `package.json` (not Directory.Packages.props, which is .NET only).
- Ask Josef before every commit (memory rule). Commit steps below mean "ask, then commit".

## Review Focus

1. Esc while the price details are open must close only the details, not also clear the selection or leave shaping.
2. A long catalogue (200+ articles, varieties with 4 sizes) and an empty search result: the shelf must scroll, show
   "Inga artiklar matchar …" and never overflow the window.
3. The view switch during shaping or photo pinning must stay locked to "Ovanifrån", and leaving shaping must still
   return to 3D as today.
4. A selected element with no article (existing lawn), a surface article, and an element marked for removal: the
   plant label must show sensible content (no price row, "Behåll") and never crash.
5. Narrow desktop (1280 × 800) with the Förslag panel open and a long "Byt mot" list: nothing overlaps the price,
   the rail or the shelf.

Each line has its test in the owning task (Tasks 4, 5, 3, 6, 9 respectively).

---

### Task 1: Tooling, tokens and fonts

**Files:**
- Modify: `src/web/package.json` (deps + devDeps), `src/web/vite.config.ts` (vitest `test` block)
- Create: `src/web/src/ui/tokens.css`, `src/web/src/ui/base.css`, `src/web/src/ui/palette.ts`, `src/web/src/ui/contrast.ts`
- Modify: `src/web/src/main.tsx` (import `@fontsource-variable/familjen-grotesk`, `./ui/tokens.css`, `./ui/base.css`),
  delete `src/web/src/index.css` (its rules move into `base.css`)
- Test: `src/web/src/ui/contrast.test.ts`

**Interfaces:**
- Produces: CSS custom properties from the spec table plus `--warn`, `--warn-soft`, `--error`, `--error-soft`,
  `--focus`, `--font` (`'Familjen Grotesk Variable', system-ui, sans-serif`), type sizes `--text-xs` 11.5px,
  `--text-s` 12.5px, `--text-m` 13.5px, `--text-base` 14px, `--text-l` 16px, `--text-xl` 19px, `--text-2xl` 22px.
- Produces: `export const palette: Record<string, string>` in `palette.ts` (same names without `--`, camelCase:
  `fir`, `onFir3`, `terra`, `sceneTop`, …) and `export function contrastRatio(a: string, b: string): number`
  in `contrast.ts` (WCAG relative luminance).

- [ ] **Step 1:** `npm --prefix src/web install @fontsource-variable/familjen-grotesk@5.3.0 lucide-react@1.76.0` and
  `npm --prefix src/web install -D @testing-library/react @testing-library/user-event @testing-library/jest-dom jsdom`.
  Component test files opt in with `// @vitest-environment jsdom` as their first line, so existing tests stay in node.
- [ ] **Step 2: Write the failing test** `contrast.test.ts`:
  - `contrastRatio('#ffffff', '#000000')` ≈ 21.
  - Every pair in Global Constraints ≥ 4.5, using `palette`.
  - `contrastRatio(palette.onFir3, palette.fir2) < 4.5`, which documents the forbidden pair.
  - `palette.warn` and `palette.error` as text on `palette.linen` ≥ 4.5.
- [ ] **Step 3:** Run `npm --prefix src/web test -- contrast`. Expected: FAIL (module not found).
- [ ] **Step 4:** Implement `palette.ts`, `contrast.ts`, `tokens.css` (values identical to `palette`) and `base.css`:
  `body` uses `--font`, `--ink`, background `--fir`, antialiasing; a global `:focus-visible` ring; a
  `prefers-reduced-motion` rule. Choose `warn` and `error` so the test passes, in the palette's muted register.
- [ ] **Step 5:** Run the test. Expected: PASS. Then run `npm --prefix src/web run build`. Expected: build succeeds and
  `../Api/wwwroot/assets` contains a `familjen-grotesk` woff2.
- [ ] **Step 6:** Ask Josef, then commit `feat(web): Plantskolan tokens, font and test setup`.

### Task 2: Shared components

**Files:**
- Create in `src/web/src/ui/`: `Button.tsx`, `IconButton.tsx`, `SegmentedControl.tsx`, `Tabs.tsx`, `SearchField.tsx`,
  `Chip.tsx`, `Card.tsx`, `Badge.tsx`, `Field.tsx`, `Notice.tsx`, `ui.css`, `index.ts` (re-exports)
- Test: `src/web/src/ui/components.test.tsx`

**Interfaces:**
- Produces:
  - `Button({ variant?: 'primary'|'secondary'|'quiet'|'danger', icon?: LucideIcon, ...ButtonHTMLAttributes })`
  - `IconButton({ label: string, icon: LucideIcon, active?: boolean, caption?: string, ...ButtonHTMLAttributes })`,
    where `label` → `aria-label`, and `caption` is the small text under the icon used by the rail.
  - `SegmentedControl<T extends string>({ label: string, value: T, options: {value: T, label: string, icon?: LucideIcon}[], onChange(v: T): void, disabled?: boolean })`
  - `Tabs<T extends string>({ label: string, value: T, tabs: {value: T, label: string, icon?: LucideIcon}[], onChange(v: T): void })`,
    using roles `tablist` / `tab`.
  - `SearchField({ label: string, value: string, onChange(v: string): void, placeholder?: string })`
  - `Chip({ selected: boolean, onClick(): void, children })`
  - `Card({ as?: 'section'|'div'|'aside', elevated?: boolean, children, className? })`
  - `Badge({ tone: 'new'|'neutral'|'warn'|'cut', children })`
  - `Field({ label: string, hint?: string, error?: string, children })`
  - `Notice({ tone: 'info'|'warn'|'error', children, action?: ReactNode })`
  - `Notice` with tone `warn` or `error` renders `role="alert"`.

- [ ] **Step 1: Write the failing tests** (jsdom):
  - `Tabs`: ArrowRight from the last tab wraps to the first and calls `onChange`. Only the selected tab has
    `tabIndex=0`.
  - `SegmentedControl`: ArrowLeft and ArrowRight change the value. When `disabled`, a click does not call `onChange`.
  - `IconButton` has the accessible name of `label`.
  - `Button` with `disabled` does not fire `onClick`.
  - `SearchField` calls `onChange` with the typed text. A clear button (aria-label "Rensa sökning") appears only
    when the value is non-empty, and empties the value.
  - `Notice tone="error"` has `role="alert"`.
- [ ] **Step 2:** Run `npm --prefix src/web test -- components`. Expected: FAIL.
- [ ] **Step 3:** Implement the components and `ui.css`. Measures come from the reference HTML:
  - buttons 42 px high with 12 px radius;
  - rail buttons 56 px with 12 px radius;
  - tabs 32 px inside a 3 px `--linen-2` track;
  - chips 26 px pill;
  - search 38 px with 11 px radius;
  - cards `--radius-l` with `--lift`.
  - Lucide icons 16–21 px with `strokeWidth` 1.75.
- [ ] **Step 4:** Run the tests. Expected: PASS. Run `npm --prefix src/web run lint`. Expected: clean.
- [ ] **Step 5:** Ask Josef, then commit `feat(web): shared Plantskolan components`.

### Task 3: Editor shell, rail, view switch and dev menu

**Files:**
- Create: `src/web/src/editor/editorUiStore.ts`, `src/web/src/editor/shell/EditorHeader.tsx`, `shell/ToolRail.tsx`,
  `shell/ViewSwitch.tsx`, `shell/ShapingStrip.tsx`, `shell/StatusLine.tsx`, `shell/DevMenu.tsx`, `shell/shell.css`
- Modify: `src/web/src/editor/EditorPage.tsx` (layout + keyboard handler), `src/web/src/editor/editor.css` (old header,
  panel and status rules removed as their replacements land)
- Test: `src/web/src/editor/shell/shell.test.tsx`, `src/web/src/editor/editorUiStore.test.ts`

**Interfaces:**
- Produces: `useEditorUiStore` with state
  `{ panel: 'catalog'|'photos'|'proposals', priceOpen: boolean, query: string, category: string | null }` and setters
  `setPanel`, `setPriceOpen`, `setQuery`, `setCategory`.
- Produces: `viewLocked(mode: Tool['mode']): boolean`, which is true for `shape`, `draw` and `pin`.
- Produces: `handleEscape(): void` in `EditorPage.tsx`, the single Esc path. If `priceOpen`, it closes the details
  and stops. Otherwise it does today's behaviour (finish shaping or pinning, else select tool, then `select(null)`).
- Consumes: `IconButton`, `SegmentedControl`, `Button` (Task 2), `useSceneStore` (`view`, `setView`, `tool`,
  `undo`, `redo`, `past`, `future`), `startShaping`, `startDrawing`, `finishShaping`, `addStressTest`, `SaveStatus`.

Layout:
- `.editor` grid with header 80 px on `--fir`.
- The scene window: inset 16 px, radius `--radius-xl`, background the scene gradient.
- The shelf: 156 px, inset 16 px, `--lift-dark`, built in Task 5. Until then the old `CatalogPanel` sits in the
  shelf slot.
- The side panel slot for Foton and Förslag sits inside the window, left of centre, 380 px wide, beside the rail.

Rail and view switch:
- The rail has Välj, Forma tomt, Forma hus, a separator, Ångra and Gör om.
  - Captions: "Välj", "Tomt", "Hus", "Ångra", "Gör om".
  - Icons: `MousePointer2`, `Spline`, `House`, `Undo2`, `Redo2`.
  - Disabled until the design has loaded, as today.
- The view switch "Ovanifrån" / "3D" calls `setView`. It is disabled while `viewLocked(tool.mode)`.

Dev menu and status line:
- The dev menu sits behind an `IconButton` labelled "Utvecklarverktyg" (`Wrench`) at the right end of the status line.
- The menu holds:
  - "Lägg till 300 växter" and "Rensa nya växter";
  - the checkboxes "Mät prestanda" and "Detaljerade modeller";
  - the fps line while measuring.
- The status line holds:
  - "{n} element";
  - "{n} markerade för borttagning" when > 0;
  - the crowding warning, the missing-article warning and `notice`, each as a `Badge tone="warn"` with today's text.

- [ ] **Step 1: Write the failing tests:**
  - `editorUiStore`: `setPriceOpen(true)` then `handleEscape()` makes `priceOpen` false and keeps `selectedId`.
    A second `handleEscape()` clears `selectedId`.
  - `viewLocked`: true for `shape`, `draw` and `pin`; false for `select`, `place` and `line`. The view switch renders
    disabled with tool `{mode:'shape', id:'plot'}`.
  - ViewSwitch: clicking "Ovanifrån" sets `view` to `'top'`.
  - DevMenu: closed by default, with no "Lägg till 300 växter" in the document. Clicking "Utvecklarverktyg" shows all
    four controls. Esc closes the menu.
  - ToolRail: "Ångra" is disabled when `past` is empty and enabled after one `execute`.
- [ ] **Step 2:** Run `npm --prefix src/web test -- shell editorUiStore`. Expected: FAIL.
- [ ] **Step 3:** Implement the store, the components and the new `EditorPage` layout. `ShapingStrip` shows today's
  hint texts and "Rita om" / "Klar" as a floating `Card` top-centre in the window.
- [ ] **Step 4:** Run all web tests: `npm --prefix src/web test`. Expected: PASS (all existing tests too).
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): Plantskolan shell, tool rail, view switch and dev menu`.

### Task 4: Price in the header and price details

**Files:**
- Create: `src/web/src/editor/price/PriceSummary.tsx`, `price/PriceDetails.tsx`, `price/price.css`
- Delete: `src/web/src/editor/PricePanel.tsx` (its content moves to `PriceDetails`)
- Test: `src/web/src/editor/price/price.test.tsx`

**Interfaces:**
- Consumes: `usePriceStore` (`estimate`, `loading`, `error`), `formatRange`, `formatMissing`, `useEditorUiStore.priceOpen`.
- Produces: `PriceSummary`, a button in the header, `aria-expanded={priceOpen}`, `aria-controls="price-details"`, with
  three lines:
  - "Ungefärligt pris";
  - the range;
  - "inkl. moms · uppskattade mått".
  - While `loading` with an old estimate, the range is faded and the summary says "Räknar…".
  - On `invalid-scene` it says "Priset kan inte räknas, eftersom designen har fel."
  - On `unavailable` it says "Priset kunde inte hämtas. Försök igen om en stund."
- Produces: `PriceDetails`, a `Card` with `id="price-details"` under the header on the right. It holds today's
  content:
  - Säkerhet, the hint, the subtotals and varav moms;
  - missing articles;
  - "Visa alla rader" / "Dölj rader", with the lines in a scrolling list of max-height 50vh;
  - "Priset är ungefärligt. Den bindande offerten kommer från säljaren."
  - It closes on a click outside, on a second click on the summary, and on Esc via `handleEscape`.

- [ ] **Step 1: Write the failing tests:**
  - A summary with an estimate of 48 600–71 200 shows `formatRange(48600, 71200)` and "inkl. moms · uppskattade mått".
  - Clicking the summary opens the details, which show "Säkerhet: medel". A second click closes them.
  - Error states show the two texts above.
  - A pointerdown outside the details closes them.
  - **Review Focus 1:** with an element selected and details open, `keydown Escape` on `window` closes the details
    and `useSceneStore.getState().selectedId` is unchanged.
- [ ] **Step 2:** Run `npm --prefix src/web test -- price.test`. Expected: FAIL.
- [ ] **Step 3:** Implement, and wire `PriceSummary` into `EditorHeader` with a fixed `min-width` so it never moves.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): price summary in the header with details card`.

### Task 5: Catalogue shelf with search and categories

**Files:**
- Create: `src/web/src/editor/shelf/shelfCards.ts`, `shelf/Shelf.tsx`, `shelf/ArticleCard.tsx`, `shelf/shelf.css`
- Modify: `src/web/src/editor/CatalogPanel.tsx` (shrinks to the selection part, which moves out in Task 6; the article
  list moves here), `EditorPage.tsx`
- Test: `src/web/src/editor/shelf/shelfCards.test.ts`, `src/web/src/editor/shelf/shelf.test.tsx`

**Interfaces:**
- Consumes: `groupForPanel` (panelGroups.ts), `useEditorUiStore` (`query`, `category`, `panel`), `Tabs`, `SearchField`,
  `Chip`, `Badge`.
- Produces: `type ShelfCard = { article: CatalogArticle; title: string; detail: string | null }`. `title` is the variety
  name when the variety has several sizes, otherwise the article name. `detail` is the size part
  (`article.name` minus the variety, trimmed) or null.
- Produces: `shelfCards(categories: PanelCategory[], category: string | null, query: string): ShelfCard[]`. It keeps
  catalogue order, matches the trimmed query case-insensitively (`toLocaleLowerCase('sv-SE')`) against the name and
  the variety, and an empty query matches all.
- Produces: `formatUnitPrice(price: number, unit: CatalogArticle['unit']): string`, giving "3 450 kr", "420 kr/m" or
  "640 kr/m²".

The shelf:
- **Left column (276 px):**
  - the Katalog/Foton/Förslag `Tabs` (icons `LayoutGrid`, `Images`, `Layers`);
  - `SearchField` labelled "Sök i katalogen", placeholder "Sök bland {n} artiklar";
  - the chips "Alla" plus one per category.
- **Right:** a horizontally scrolling row of `ArticleCard`s (150 px wide). Each card has:
  - a thumbnail slot: a colour swatch until Task 7;
  - the title, the detail and the unit price;
  - an `IconButton` "Lägg till {title}" (`Plus`) that starts placing, or a "Klar" state when that article is being
    placed (today's logic in `ArticleRow`);
  - "Slut i lager" as a `Badge` with no add button;
  - for surface articles, no add button but the text "Välj en yta i trädgården för att byta".
- With panel `photos` or `proposals`, the shelf shows only the tabs, and the side panel slot shows `PhotoPanel` or
  `ProposalPanel`.
- Loading and error keep today's texts ("Laddar katalogen…", "Katalogen kunde inte hämtas." + "Försök igen") as a
  `Notice`.

- [ ] **Step 1: Write the failing tests:**
  - `shelfCards`: query "LÖNN" finds "Skogslönn 'Crimson King'".
  - A query matching only a variety name returns all its sizes, with `detail` set.
  - The category "Träd" with an empty query returns only trees, in order.
  - Whitespace-only queries match all.
  - `formatUnitPrice(420, 'meter')` is "420 kr/m"; `formatUnitPrice(640, 'square-meter')` is "640 kr/m²";
    `formatUnitPrice(3450, 'piece')` is "3 450 kr" (narrow no-break space as `Intl` gives).
  - Shelf (jsdom): typing "xyz" shows "Inga artiklar matchar ”xyz”" (**Review Focus 2**). Clicking "Lägg till Rönn"
    sets `tool` to `{mode:'place', articleId}`. An out-of-stock article shows "Slut i lager" and no add button.
- [ ] **Step 2:** Run `npm --prefix src/web test -- shelf`. Expected: FAIL.
- [ ] **Step 3:** Implement. The card row is a native horizontal scroll with `scroll-snap-type: x proximity` and the
  reference's fade mask. Vertical wheel over the row scrolls it horizontally.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): catalogue shelf with search and categories`.

### Task 6: Plant label for the selected element

**Files:**
- Create: `src/web/src/editor/label/labelFacts.ts`, `label/PlantLabel.tsx`, `label/label.css`
- Modify: `src/web/src/editor/CatalogPanel.tsx` (deleted once empty; `selectionName` and `optionsFor` move to
  `label/PlantLabel.tsx`), `EditorPage.tsx`
- Test: `src/web/src/editor/label/labelFacts.test.ts`, `src/web/src/editor/label/label.test.tsx`

**Interfaces:**
- Consumes: `findArticle`, `replaceOptions`, `keepSelected`, `removeSelected`, `replaceSelected`, `rotateSelected`,
  `formatUnitPrice` (Task 5), `Card`, `Badge`, `Button`, `IconButton`.
- Produces: `labelFacts(article: CatalogArticle): { label: string; value: string }[]`, in this order and only when
  the value is set:
  - "Växtzon" → "I–{roman(zone)}";
  - "Höjd vid plantering" → cm, rounded, e.g. "200 cm";
  - "Fullvuxen höjd" → m with at most one decimal and a comma, e.g. "12 m", "1,5 m";
  - "Fullvuxen bredd" → same format;
  - "Läge" → the light values joined with "–", where sun → "Sol", part-shade → "Halvskugga", shade → "Skugga".

The label is a `Card` top-right in the window, 296 px wide, with the reference's punched hole. Status badges:
- "Ny i designen" for `new`;
- "Befintlig" for `existing-keep`;
- "Tas bort" for `existing-remove`.

Then:
- the name (`selectionName`);
- the facts with dotted leaders;
- the price row (`formatUnitPrice`), only when there is an article;
- the actions: Vrid ⟲ and Vrid ⟳ (`IconButton`s with "Vrid åt vänster (Q)" and "Vrid åt höger (E)", for points only),
  "Byt" (toggles "Byt mot", collapsed by default, only when there are options) and "Ta bort".
- "Byt mot" shows today's hint text and rows with their own "Byt". It scrolls inside the label at max-height 40vh.
- For `existing-remove` it shows only the name and "Behåll".
- With no selection the label is absent. Today's tool hint ("Klicka på ett element…", place and line hints) shows as
  quiet text bottom-centre in the window.

- [ ] **Step 1: Write the failing tests:**
  - `labelFacts` for an article with zone 5, height 2.0, matureHeight 12, matureWidth 1.5 and light
    [sun, part-shade] gives exactly
    `[Växtzon I–V, Höjd vid plantering 200 cm, Fullvuxen höjd 12 m, Fullvuxen bredd 1,5 m, Läge Sol–Halvskugga]`.
  - An article with all nulls and `[]` light gives `[]`.
  - **Review Focus 4:**
    - an existing lawn surface without an article shows "Befintlig yta (gräs)" and no price row;
    - an `existing-remove` element shows "Behåll", and clicking it calls `keepSelected` (status becomes
      `existing-keep`);
    - a surface article shows "Byt" and no Vrid.
  - "Byt mot" is collapsed until "Byt" is clicked.
- [ ] **Step 2:** Run `npm --prefix src/web test -- label`. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): plant label for the selected element`.

### Task 7: Article thumbnails

**Files:**
- Create: `src/web/src/editor/shelf/thumbnails.ts`, `shelf/ArticleThumb.tsx`
- Modify: `shelf/ArticleCard.tsx`, `label/PlantLabel.tsx` (label hero uses the same thumb at 256 × 118)
- Test: `src/web/src/editor/shelf/thumbnails.test.ts`

**Interfaces:**
- Produces: `thumbnailKey(article: CatalogArticle): string`, which is `${typeModel}|${color}|${accentColor ?? ''}`.
- Produces: `createThumbnailCache(render: (article: CatalogArticle) => Promise<string>)`. It returns
  `{ get(article): Promise<string> }` and calls `render` once per key, even for concurrent calls. A rejected render
  is not cached, so a later `get` retries.
- Produces: `renderThumbnail(article): Promise<string>`, a PNG data URL drawn with one lazily created offscreen
  `WebGLRenderer` (not the scene's), 300 × 136 px. It uses the same model the scene uses, via `modelLibrary` and
  `GLB_URLS` with the generated shape as fallback, in Plantskolan light on a transparent background. Renders run one
  at a time.
- Produces: `ArticleThumb({ article, width, height })`. It shows the colour swatch, starts `get` when an
  `IntersectionObserver` reports the thumb visible, then fades the image in. A failure keeps the swatch.

- [ ] **Step 1: Write the failing tests:**
  - Two articles with the same model and colour share a key; a different `accentColor` gives a different key.
  - Three concurrent `get`s for one key call `render` once.
  - A rejected render is retried on the next `get`.
- [ ] **Step 2:** Run `npm --prefix src/web test -- thumbnails`. Expected: FAIL.
- [ ] **Step 3:** Implement. Dispose geometries, materials and textures after each render. Call
  `renderer.forceContextLoss()` only on page unload.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS. In the browser, check the catalogue shows rendered
  trees and shrubs, and that fps with 300 plants and the shelf open is unchanged (see Task 11).
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): rendered article thumbnails`.

### Task 8: Warm scene light and terra selection

**Files:**
- Modify: `src/web/src/editor/SceneCanvas.tsx` (background, lights, shadows, selection colour),
  `src/web/src/editor/ShapeHandles.tsx` (handle colours), `src/web/src/editor/PointInstances.tsx` (only if the
  selection colour lives there)
- Test: `src/web/src/editor/sceneLook.test.ts`

**Interfaces:**
- Consumes: `palette` (Task 1).
- Produces: `sceneLook` in `src/web/src/editor/sceneLook.ts`:
  `{ background: string, hemisphere: [sky: string, ground: string, intensity: number], sun: { color: string, intensity: number, position: [number, number, number] }, selection: string, handle: string }`.
  `SceneCanvas` reads every value from it.

Values:
- background `palette.sceneTop`;
- hemisphere sky `#FFF3E0`, ground `#7A6A4F`;
- a low golden sun `#FFD9A8` from the south-west;
- selection `palette.terra`;
- shadows `PCFSoftShadowMap`.

Tune the intensities in the browser until models keep their article colours, and record the final values in
`sceneLook.ts`.

An element marked `existing-remove` keeps today's look and gets a small `--cut` label "Tas bort" above it (drei
`Html`, `pointer-events: none`). Do this only when it stays readable and does not cost frame rate with 300 plants;
otherwise leave it out and note that in the ledger, as the spec allows.

- [ ] **Step 1: Write the failing test:**
  - `sceneLook.selection === palette.terra`.
  - `sceneLook.background === palette.sceneTop`.
  - The sun position has y > 0 and a south-west direction (x < 0, z > 0 in three.js, since north is -Z).
- [ ] **Step 2:** Run `npm --prefix src/web test -- sceneLook`. Expected: FAIL.
- [ ] **Step 3:** Implement and wire.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS. In the browser, take before and after screenshots
  of the demo scene in 3D and Ovanifrån.
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): warm Plantskolan light in the scene`.

### Task 9: Foton and Förslag panels in the new look

**Files:**
- Modify: `src/web/src/editor/photos/PhotoPanel.tsx`, `photos/PhotoMarks.tsx` (marker colours only),
  `proposals/ProposalPanel.tsx`, `editor.css` (remaining photo and proposal rules move to `photos/photos.css` and
  `proposals/proposals.css`, both token-only), `src/web/src/editor/SaveStatus.tsx` (`SaveConflict` and `DesignLoading`
  as centred `Card`s)
- Test: `src/web/src/editor/sidePanel.test.tsx`

**Interfaces:**
- Consumes: `Card`, `Field`, `Button`, `Notice`, `Badge`, `useEditorUiStore.panel`.

The side panel:
- a `Card` in the window's side slot (380 px wide, full window height minus 32 px) that scrolls internally;
- a header row with the panel name and a close `IconButton` "Stäng", which sets the panel to `catalog`.

Inside it, the existing behaviour and texts are unchanged:
- Foton's upload, list, pinning and review;
- Förslag's wishes form with Field-wrapped inputs, proposal cards, Prova, Välj det här and messages.

- [ ] **Step 1: Write the failing tests:**
  - Clicking the shelf tab "Förslag" renders the side panel with heading "Förslag", and "Stäng" returns to the
    catalogue cards.
  - **Review Focus 5:** with the side panel open, the label for a selected element still renders (both visible,
    neither unmounted).
- [ ] **Step 2:** Run `npm --prefix src/web test -- sidePanel`. Expected: FAIL.
- [ ] **Step 3:** Implement. Existing `photoStore` and `proposalStore` tests stay untouched and green.
- [ ] **Step 4:** Run `npm --prefix src/web test`. Expected: PASS. In the browser at 1280 × 800, open Förslag with a
  selected tree and a long "Byt mot" list, and check nothing overlaps.
- [ ] **Step 5:** Ask Josef, then commit `feat(editor): photo and proposal panels in the Plantskolan look`.

### Task 10: Admin, staff login and start page

**Files:**
- Modify: `src/web/src/admin/AdminPage.tsx`, `admin/StaffLogin.tsx`, `admin/AiPanel.tsx`, `admin/admin.css`
  (rewritten token-only), `src/web/src/app/CustomerFlowPage.tsx`, create `app/start.css`
- Test: `src/web/src/admin/adminLook.test.tsx`

**Interfaces:**
- Consumes: `Card`, `Field`, `Button`, `Notice`, `Badge`.

Admin, login and start page:
- **Admin:** fir header 64 px with "Drömträdgård · Admin", the signed-in user, `Button variant="quiet"` "Logga ut",
  and content on `--linen` cards in a max-width 1040 px column.
- **AI panel:** the settings with `Field` for every number input and switch, and the budget as `Notice`.
- **Staff login:** a centred 400 px `Card` on fir. Every step keeps its texts. The QR code sits on white with 16 px
  padding, and keys and backup codes are monospace on `--linen-2`.
- **Start page:** fir background with the wordmark, "Från foto till offertförfrågan i sex steg.", the six steps as an
  ordered list on a linen card, and two `Button`-styled links: "Öppna 3D-editorn" (primary) and "Admin" (secondary).

- [ ] **Step 1: Write the failing tests** (jsdom):
  - The staff login's first step renders its inputs with accessible labels (via `Field`).
  - The admin logout failure renders as `role="alert"`.
  - The start page links have hrefs `/editor` and `/admin`.
- [ ] **Step 2:** Run `npm --prefix src/web test -- adminLook`. Expected: FAIL.
- [ ] **Step 3:** Implement. Login and admin must work at 390 px wide (no horizontal scroll).
- [ ] **Step 4:** Run `npm --prefix src/web test` and `dotnet test --solution GardenBuilder.slnx`. Expected: both PASS.
- [ ] **Step 5:** Ask Josef, then commit `feat(web): admin, staff login and start page in the Plantskolan look`.

### Task 11: Verification, Impeccable finish review and DESIGN.md

**Files:**
- Create: `DESIGN.md` (repo root) and `.impeccable/design.json`, both written by the documenter; the surface brief via
  `impeccable surface-brief write`
- Modify: `docs/superpowers/plans/2026-11-13-roadmap.md` (a "Plantskolan" row marked Klar), `CLAUDE.md` (one line under
  Frontend rules: "UI uses the tokens and components in `src/web/src/ui`; see DESIGN.md")

- [ ] **Step 1:** Write the editor's surface brief with the direction contract:
  - THESIS, OWN-WORLD, STORY, FIRST VIEWPORT;
  - FORM `seed: none (pinned)` "C Plantskolan";
  - the FINISH line verbatim from Impeccable's new-work.md.
- [ ] **Step 2:** Run the app (`dotnet run --project src/AppHost`, via the preview tool) and capture:
  - the editor at 1440 × 900 and 1280 × 800: catalogue, a selected tree, price details, shaping, Förslag open,
    300 plants;
  - admin, login and the start page at 1440 and 390 wide.
  - Save to `.impeccable/review/`.
- [ ] **Step 3:** Measure fps with "Mät prestanda" and 300 plants, on main (before) and on the branch (after), with
  both "Detaljerade modeller" settings. Expected: ≥ 60 fps on the branch, or no worse than main. Record the numbers.
- [ ] **Step 4:** Run `sh .claude/skills/impeccable/scripts/impeccable detect --json --changed`. Fix the mechanical
  findings.
- [ ] **Step 5:** Spawn the shipped Impeccable finish reviewer with:
  - the screenshots;
  - the direction contract;
  - the reference image as the critique reference;
  - the craft-floor path;
  - the line "code-led, no image generation".
  - Act on its disposition per new-work.md §7: one fix batch, then a verdict pass.
- [ ] **Step 6:** Spawn the Impeccable documenter to write `DESIGN.md` and `.impeccable/design.json` from the built
  system. Then confirm `impeccable context` resolves `DESIGN.md` at the root, not `docs/design.md`. If it resolves the
  wrong one, record it as a finding.
- [ ] **Step 7:** Run all tests: `npm --prefix src/web test`, `npm --prefix src/web run lint`,
  `npm --prefix src/web run build`, `dotnet test --solution GardenBuilder.slnx`. Expected: all PASS.
- [ ] **Step 8:** Ask Josef, then commit `docs: Plantskolan DESIGN.md, surface brief and roadmap`. Also ask whether to
  commit `.claude/skills/impeccable/`, `skills-lock.json` and `PRODUCT.md`.
