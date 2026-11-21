---
version: 1
slug: "src-web-src-editor-editorpage-tsx"
primary_target: "src/web/src/editor/EditorPage.tsx"
related_targets: []
---

# Editor surface brief

Scope: the 3D garden editor (`/editor`), visitor mode Operate. Audience: Swedish homeowners planning a garden on a laptop; also sellers in admin later. Task: shape plot and house, add and swap stock articles, read an honest price span, try AI proposals. Constraints: Swedish UI, prices only from the server and always a span, no dummy controls, 60 fps with 300 plants.

## Direction contract

THESIS: A nursery afternoon: the garden is a warm, lit model in a rounded window inside a fir-green frame, and the tools are linen objects laid around it. Refuses the category default of a grey CAD sidebar with a blue accent.

OWN-WORLD: Fir #1C392D frame, linen #F8F4EC surfaces with soft layered lift, ink #1E2923 text, terracotta #BC522B only for the one most important action and for selection, warm scene ground #EDE3D3. Familjen Grotesk, tabular numerals for money and measures. Lucide icons at 1.75 stroke. Radii 12/18/22.

STORY: The visitor sees their own garden first, understands the price is a range with estimated measurements, finds articles on a shelf like a nursery bench, picks one up, places it, reads its plant label, swaps or removes it, and opens Förslag or Foton beside the tools when they want help.

FIRST VIEWPORT: 80 px fir header with wordmark + save state left and the price span right (opens details). Below, the scene window fills the width with 16 px inset and 22 px radius; tool rail floats top-left, Ovanifrån/3D beside it, plant label top-right when something is selected, status line and dev wrench along the bottom. A 156 px linen shelf along the bottom: tabs, search and category chips left, a sideways row of article cards with rendered model pictures right.

FORM: Plantskolan, chosen by Josef from three Opus-made directions after rejecting a first rolled round; seed: none (pinned) — "C Plantskolan".

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
