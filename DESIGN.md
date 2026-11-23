---
name: Drömträdgård
description: Plantskolan, a nursery afternoon. A warm 3D garden in a rounded window inside a fir-green frame, with linen tools laid around it.
colors:
  fir: "#1C392D"
  fir-2: "#244536"
  fir-3: "#2F5241"
  on-fir: "#F3EEE4"
  on-fir-2: "#C3CEC2"
  on-fir-3: "#97A99B"
  linen: "#F8F4EC"
  linen-2: "#EFE9DE"
  linen-3: "#E4DBCC"
  ink: "#1E2923"
  ink-2: "#48544D"
  ink-3: "#626C65"
  terra: "#BC522B"
  terra-ink: "#9C3F1E"
  terra-soft: "#F6E2D6"
  cut: "#2A2420"
  warn: "#7E520C"
  warn-soft: "#F5E6C4"
  error: "#A3392B"
  error-soft: "#F7E0DA"
  white: "#FFFFFF"
  scene-top: "#EDE3D3"
  scene-mid: "#E2D5C1"
  scene-bottom: "#D5C4AA"
  hairline: "rgba(30, 41, 35, 0.1)"
  hairline-strong: "rgba(30, 41, 35, 0.18)"
typography:
  display:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "34px"
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: "-0.03em"
  headline:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "22px"
    fontWeight: 650
    lineHeight: 1.15
    letterSpacing: "-0.02em"
  title:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "19px"
    fontWeight: 650
    lineHeight: 1.2
    letterSpacing: "-0.015em"
  title-small:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "16px"
    fontWeight: 650
    lineHeight: 1.2
    letterSpacing: "-0.015em"
  body:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.45
  body-small:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "13.5px"
    fontWeight: 400
    lineHeight: 1.45
  label:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "12.5px"
    fontWeight: 600
    lineHeight: 1.3
  caption:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "11.5px"
    fontWeight: 600
    lineHeight: 1
  numeric:
    fontFamily: "Familjen Grotesk Variable, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "22px"
    fontWeight: 650
    lineHeight: 1.15
    letterSpacing: "-0.02em"
    fontFeature: "tnum"
rounded:
  s: "8px"
  m: "12px"
  l: "18px"
  xl: "22px"
  pill: "999px"
spacing:
  "1": "4px"
  "2": "8px"
  "3": "12px"
  "4": "16px"
  "5": "20px"
  "6": "24px"
  "7": "32px"
  "8": "40px"
components:
  button-accent:
    backgroundColor: "{colors.terra}"
    textColor: "{colors.white}"
    typography: "{typography.body}"
    rounded: "{rounded.m}"
    padding: "0 16px"
    height: "42px"
  button-accent-hover:
    backgroundColor: "{colors.terra-ink}"
  button-primary:
    backgroundColor: "{colors.fir}"
    textColor: "{colors.on-fir}"
    rounded: "{rounded.m}"
    padding: "0 16px"
    height: "42px"
  button-primary-hover:
    backgroundColor: "{colors.fir-2}"
  button-secondary:
    backgroundColor: "{colors.linen-2}"
    textColor: "{colors.ink}"
    rounded: "{rounded.m}"
    padding: "0 16px"
    height: "42px"
  button-secondary-hover:
    backgroundColor: "{colors.linen-3}"
  button-quiet:
    textColor: "{colors.ink-2}"
    rounded: "{rounded.m}"
    padding: "0 16px"
    height: "42px"
  button-quiet-hover:
    backgroundColor: "{colors.linen-2}"
    textColor: "{colors.ink}"
  button-danger:
    backgroundColor: "{colors.linen-2}"
    textColor: "{colors.error}"
    rounded: "{rounded.m}"
    padding: "0 16px"
    height: "42px"
  button-danger-hover:
    backgroundColor: "{colors.error-soft}"
  button-small:
    rounded: "10px"
    padding: "0 12px"
    height: "34px"
  icon-button:
    textColor: "{colors.ink-2}"
    rounded: "10px"
    size: "36px"
  icon-button-hover:
    backgroundColor: "{colors.linen-2}"
    textColor: "{colors.ink}"
  icon-button-active:
    backgroundColor: "{colors.fir}"
    textColor: "{colors.on-fir}"
  icon-button-fir:
    textColor: "{colors.on-fir-2}"
  icon-button-fir-hover:
    backgroundColor: "{colors.fir-2}"
    textColor: "{colors.on-fir}"
  rail-button:
    textColor: "{colors.ink-2}"
    typography: "{typography.caption}"
    rounded: "{rounded.m}"
    size: "56px"
  tabs:
    backgroundColor: "{colors.linen-2}"
    rounded: "{rounded.m}"
    padding: "3px"
  tab:
    textColor: "{colors.ink-2}"
    typography: "{typography.label}"
    rounded: "9px"
    height: "32px"
  tab-selected:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink}"
  segmented:
    backgroundColor: "{colors.linen}"
    rounded: "14px"
    padding: "5px"
  segment:
    textColor: "{colors.ink-2}"
    typography: "{typography.body-small}"
    rounded: "10px"
    padding: "0 13px"
    height: "36px"
  segment-checked:
    backgroundColor: "{colors.fir}"
    textColor: "{colors.on-fir}"
  search-field:
    backgroundColor: "{colors.linen-2}"
    textColor: "{colors.ink}"
    typography: "{typography.body-small}"
    rounded: "11px"
    padding: "0 36px 0 37px"
    height: "38px"
  search-field-focus:
    backgroundColor: "{colors.white}"
  chip:
    backgroundColor: "{colors.linen-2}"
    textColor: "{colors.ink-2}"
    typography: "{typography.label}"
    rounded: "{rounded.pill}"
    padding: "0 10px"
    height: "26px"
  chip-hover:
    backgroundColor: "{colors.linen-3}"
    textColor: "{colors.ink}"
  chip-selected:
    backgroundColor: "{colors.fir}"
    textColor: "{colors.on-fir}"
  card:
    backgroundColor: "{colors.linen}"
    textColor: "{colors.ink}"
    rounded: "{rounded.l}"
  badge-new:
    backgroundColor: "{colors.terra-soft}"
    textColor: "{colors.terra-ink}"
    rounded: "7px"
    padding: "0 8px"
    height: "22px"
  badge-neutral:
    backgroundColor: "{colors.linen-2}"
    textColor: "{colors.ink-2}"
    rounded: "7px"
    padding: "0 8px"
    height: "22px"
  badge-warn:
    backgroundColor: "{colors.warn-soft}"
    textColor: "{colors.warn}"
    rounded: "7px"
    padding: "0 8px"
    height: "22px"
  badge-cut:
    backgroundColor: "{colors.cut}"
    textColor: "{colors.on-fir}"
    rounded: "7px"
    padding: "0 8px"
    height: "22px"
  field-input:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "10px"
    padding: "9px 12px"
    height: "40px"
  notice-info:
    backgroundColor: "{colors.linen-2}"
    textColor: "{colors.ink-2}"
    rounded: "{rounded.m}"
    padding: "10px 12px"
  notice-warn:
    backgroundColor: "{colors.warn-soft}"
    textColor: "{colors.warn}"
    rounded: "{rounded.m}"
    padding: "10px 12px"
  notice-error:
    backgroundColor: "{colors.error-soft}"
    textColor: "{colors.error}"
    rounded: "{rounded.m}"
    padding: "10px 12px"
  editor-header:
    backgroundColor: "{colors.fir}"
    textColor: "{colors.on-fir}"
    height: "80px"
  scene-window:
    backgroundColor: "{colors.scene-mid}"
    rounded: "{rounded.xl}"
  tool-rail:
    backgroundColor: "{colors.linen}"
    rounded: "{rounded.l}"
    padding: "6px"
    width: "68px"
  plant-label:
    backgroundColor: "{colors.linen}"
    rounded: "{rounded.l}"
    padding: "26px 20px 20px"
    width: "296px"
  shelf:
    backgroundColor: "{colors.linen}"
    rounded: "{rounded.xl}"
    height: "156px"
  article-card:
    textColor: "{colors.ink}"
    width: "150px"
  article-card-media:
    backgroundColor: "{colors.linen-3}"
    rounded: "{rounded.m}"
    height: "68px"
  side-panel:
    backgroundColor: "{colors.linen}"
    rounded: "{rounded.l}"
    width: "380px"
  price-summary:
    textColor: "{colors.on-fir}"
    typography: "{typography.numeric}"
    rounded: "{rounded.m}"
    padding: "6px 10px"
---

# Design System: Drömträdgård

## Overview

**Creative North Star: "A Nursery Afternoon"**

The customer's own garden is the subject, shown as a warm, lit model inside a rounded window. Around that window sits a deep fir-green frame, and every tool is a linen object laid on top of it or floating over the scene: a tool rail like a slat of wood, a shelf along the bottom like a nursery bench, a plant label that hangs from a punched hole. The world deliberately refuses the category default of a grey CAD sidebar with a blue accent.

Density is that of a working tool, not a brochure: type runs small (11.5 to 22px in the editor), controls are compact (34 to 42px tall), and the scene gets nearly all the room. Warmth comes from material, not decoration: linen surfaces, soft layered shadows tinted brown, a golden low sun in the scene, and a single terracotta voice that marks only what matters now.

Everything the customer reads is Swedish. Money and measures are always tabular numerals, and prices are always shown as a span ("38 400 – 46 900 kr"), never as one exact figure.

**Key Characteristics:**
- Fir frame, linen objects, warm scene window. Three materials, nothing else.
- Terracotta appears once per view: the single primary action, or the current selection.
- One typeface (Familjen Grotesk, variable) carrying every role through weight and size.
- Soft, warm, layered lift on linen; no hard borders, hairlines only.
- Lucide icons at 1.75 stroke (1.9 inside buttons and notices).
- Every visible text/ground pair holds WCAG AA, enforced by a unit test.

## Colors

A three-material palette: dark fir for the frame, warm linen for objects, ink for text, with terracotta as the one accent.

### Primary
- **Nursery Terracotta** (terra): the single most important action on a surface (the `accent` button: "Ge mig förslag", "Tolka foton", "Öppna 3D-editorn") and selection, both in the DOM (inset 2px ring on the card being placed, the tried proposal, the photo being pinned) and in the 3D scene (selection ring, shaping handles). Also the focus ring.
- **Fired Terracotta** (terra-ink): hover of the accent button; terracotta text on linen (links, the "new" badge text, "Här börjar editorn i dag").
- **Clay Wash** (terra-soft): ground of the "new in design" badge, the photo-pinning frame, and text selection.

### Secondary
- **Fir** (fir): the frame every page sits on (body background), the header, the `primary` button, and every "on" state of a choice: active tool, checked segment, selected chip.
- **Fir Shade** (fir-2): hover on fir (primary button, fir icon buttons, the open price summary).
- **Fir Moss** (fir-3): the "high confidence" dot in price details.
- **Bleached Linen on Fir** (on-fir), **Lichen** (on-fir-2), **Old Lichen** (on-fir-3): text on fir, from primary to tertiary.

### Neutral
- **Linen** (linen): cards, the tool rail, shelf, plant label, side panel, segmented control.
- **Washed Linen** (linen-2): inset grounds inside linen: tab track, search field, chips, secondary buttons, info notices, hover for quiet controls.
- **Raw Linen** (linen-3): hover of washed-linen controls, thumbnail backdrops, the label hole, scrollbar thumb.
- **White** (white): the raised selected tab, form inputs, photo and proposal cards inside a panel, text on terracotta.
- **Ink** (ink), **Ink Grey** (ink-2), **Ink Mist** (ink-3): text on linen, primary to tertiary.
- **Hairline** / **Hairline Strong**: dividers, inset 1px outlines on secondary buttons and cards, dotted fact leaders. Never a solid grey border.
- **Scene Dusk** (scene-top, scene-mid, scene-bottom): the warm gradient behind the 3D canvas, with a soft radial light at 42% / 58%. `scene-top` is also the three.js clear colour.

### States
- **Cut** (cut): elements marked for removal (badge, status dot).
- **Ochre Warning** (warn on warn-soft) and **Brick Error** (error on error-soft): notices, field errors, the danger button's text.

### Named Rules
**The One Terracotta Rule.** Terracotta (`terra`) is reserved for the single primary action on a surface and for selection. It is never a decorative colour, never a second button, never a heading. If two terracotta things compete in one view, one of them is wrong.

**The Fir Means On Rule.** Every pressed, checked or selected choice in a control group (tool, segment, chip) turns fir with on-fir text. Selection of an *object* in the garden is terracotta; selection of a *mode* is fir.

**The Lichen Floor Rule.** `on-fir-3` is allowed only on `fir`. On `fir-2` it drops below AA (a test documents the failure).

**The Tested Pairs Rule.** These pairs are verified at ≥ 4.5:1 in `contrast.test.ts` and are the only text/ground pairs to use: ink, ink-2, ink-3 on linen; ink-2, ink-3 on linen-2; on-fir, on-fir-2, on-fir-3 on fir; white on terra; terra-ink on terra-soft; on-fir on cut; warn and error on linen; warn on warn-soft; error on error-soft. `tokens.css` and `palette.ts` must hold identical values (also tested).

## Typography

**Display Font:** Familjen Grotesk Variable (with system-ui, -apple-system, Segoe UI, sans-serif)
**Body Font:** the same family
**Label/Mono Font:** the same family, tabular numerals for numbers

**Character:** A warm, slightly quirky grotesk with nursery-sign personality, used for every role. Hierarchy comes from weight (400 / 500 / 600 / 650, and 700 for the wordmark) and a tight negative tracking on large sizes, never from a second family. Self-hosted through `@fontsource-variable/familjen-grotesk`.

### Hierarchy
- **Display** (700, 34px, -0.03em): the Drömträdgård wordmark on the start page only.
- **Headline** (650, 22px, line-height 1.15, -0.02em): the price span in the header, the total in price details, login and admin page titles.
- **Title** (650, 19px, -0.015em): side panel titles (Foton, Förslag), admin card titles; the editor wordmark at 700.
- **Title Small** (650, 16px): proposal card titles, sub-headings in panels.
- **Body** (400, 14px, line-height 1.45): base text, buttons (at 650), form inputs.
- **Body Small** (400, 13.5px): panel hints, status line, facts on the plant label, segments, search, notices.
- **Label** (600, 12.5px): tabs, chips, field hints, header sub-lines (save state, "Uppskattat pris").
- **Caption** (600, 11.5px, line-height 1): captions under tool rail icons, badges (at 650).

### Named Rules
**The Tabular Money Rule.** Every price, measure and count uses tabular numerals (the `.num` class, `font-variant-numeric: tabular-nums`). Prices are a span in SEK including VAT.

**The One Family Rule.** No second typeface, no system display face. Contrast is made with weight and size.

## Layout

The editor is a fixed, full-viewport grid on fir (`100dvh`, minimum width 1024px, no page scroll): an **80px header**, the **scene window** taking the rest, and a **156px shelf**, separated by a 16px gap and inset 16px from the sides and bottom.

- **Header (on fir):** wordmark with save state underneath on the left (a 6px dot before the state text); on the right, the price summary button (label, price span, note), right-aligned, which opens the price details card anchored under it at the right edge (340px wide).
- **Scene window:** 22px radius, warm scene gradient behind the canvas. Floating inside it with 16px inset:
  - top left, the **tool rail** (68px wide: Välj, Tomt, Hus, separator, Ångra, Gör om), with the **Ovanifrån / 3D** segmented control beside it and the **side panel** (380px, Foton, Förslag or Drömbild) below that control, all in one column that stops 64px above the bottom;
  - top centre, the **shaping strip** while editing the plot or house outline;
  - top right, the **plant label** (296px) when something is selected;
  - bottom centre, the pill **tool hint** when nothing is selected;
  - along the bottom, the **status line** (removed-count, dev wrench at the far right).
- **Shelf:** a linen bench with a 344px left side (wide enough for the four tabs Katalog, Foton, Förslag, Drömbild) (tabs, search, scrolling category chips that fade out at 85%) separated by a hairline from a horizontally scrolling row of 150px article cards (gap 10px, scroll-snap).

Spacing follows a **4px rhythm** (4, 8, 12, 16, 20, 24, 32, 40). Floating objects sit 16px from the window edges; panels pad 16–20px; control groups gap 2px inside a 3–6px track.

Admin, staff login and the start page share the frame: fir page ground, centred linen cards (`min(400–440px, 100%)`), padded 40px 16px. These are the only responsive surfaces; admin collapses its header and card padding below **600px**. The editor is desktop-only by design.

## Elevation & Depth

Depth is the material story: fir is the floor, the scene window is cut into it, and linen objects float above both. Shadows are soft, layered and warm-tinted (brown, not black) on linen; only the shelf, which sits directly on fir, uses a darker shadow. Inside a linen surface, hierarchy is tonal (linen → linen-2 inset → white raised) with hairline insets, not more shadow.

### Shadow Vocabulary
- **Lift** (`0 1px 2px rgba(40,30,15,.08), 0 6px 16px -6px rgba(40,30,15,.16), 0 18px 40px -16px rgba(40,30,15,.24)`): every floating linen object over the scene: elevated cards, tool rail, side panel, plant label, segmented control, price details.
- **Lift Dark** (`0 1px 2px rgba(0,0,0,.25), 0 12px 32px -12px rgba(0,0,0,.5)`): the shelf, which rests on fir.
- **Lift Small** (`0 1px 2px rgba(40,30,15,.12), 0 2px 6px -2px rgba(40,30,15,.12)`): small raised parts within linen: the selected tab, the "add" button on article cards.
- **Accent press** (`inset 0 1px 0 rgba(255,255,255,.2), 0 6px 14px -8px rgba(20,15,10,.45)`): the terracotta button only.

### Named Rules
**The Floating Linen Rule.** Anything laid over the scene is linen with Lift and an 18px radius. Nothing floats without its shadow, and nothing inside a floating object gets Lift again.

**The Hairline Rule.** Edges are drawn with an inset 1px hairline box-shadow or a 1px hairline rule, never a solid opaque border.

## Shapes

Gently rounded, object-like corners that grow with the size of the object: **8px** small clear buttons, **10–12px** controls (buttons 12px, small buttons and icon buttons 10px, inputs 10px, search 11px), **12px** media, notices and inner cards, **18px** floating objects (cards, rail, label, side panel), **22px** the two biggest shapes (scene window, shelf). Chips, the tool hint and status dots are full pills or circles. Badges are 7px.

Signature forms: the **punched hole** at the top centre of the plant label (12px linen-3 circle with an inner shadow), **dotted leaders** between fact name and value on the label (1.5px dotted hairline-strong), and surface swatches drawn as a tile in perspective.

## Components

### Buttons
Compact and confident, weight 650, 18px icon at 1.9 stroke with an 8px gap.
- **Shape:** gently rounded (12px), 42px tall, 16px side padding; size `s` is 34px, 10px radius, 12px padding, 13.5px text, 16px icon.
- **Accent:** terracotta with white text and its press shadow. One per surface (see The One Terracotta Rule).
- **Primary:** fir with on-fir text; hover fir-2. The default strong action that is not *the* action.
- **Secondary** (default): washed linen with hairline inset; hover raw linen.
- **Quiet:** no surface, ink-2 text; hover washed linen and ink.
- **Danger:** washed linen with error text and hairline inset; hover error-soft. Used for "Ta bort".
- **States:** press moves the button down 1px; disabled is 45% opacity with `not-allowed`; focus is the global terracotta ring.

### Icon buttons and the tool rail
- **Icon button:** 36px square, 10px radius, transparent, ink-2; hover washed linen. `active` (pressed) turns fir with on-fir text. On fir (`tone="fir"`) the idle colour is on-fir-2 and hover is fir-2.
- **Rail button (captioned):** 56px square, 12px radius, 21px icon above an 11.5px semibold caption ("Välj", "Tomt", "Hus", "Ångra", "Gör om"). The label is always Swedish and doubles as the tooltip.

### Segmented control
The Ovanifrån / 3D switch: a floating linen pill (14px radius, 5px padding, Lift) holding 36px segments with icons. The checked segment is fir. It is a real radio group with roving arrow-key focus; when locked it explains why in a tooltip.

### Tabs
A washed-linen track (12px radius, 3px padding) with 32px tabs at 12.5px semibold; the selected tab is a raised white chip with Lift Small. Roving arrow-key focus.

### Search field
38px, washed linen, 11px radius, search icon inset left, clear button right (26px, 8px radius). On focus the field turns white with a 2px terracotta ring.

### Chips
- **Style:** 26px pills, washed linen, ink-2, 12.5px semibold.
- **State:** hover raw linen; selected (`aria-pressed`) fir with on-fir. Used for catalogue categories; the row scrolls sideways without a scrollbar and fades at the end.

### Cards / Containers
- **Corner Style:** 18px.
- **Background:** linen, ink text.
- **Shadow Strategy:** Lift when elevated (the default); flat when nested.
- **Border:** none; nested cards (photo, proposal) are white with a hairline inset and 12px radius, and get a 2px terracotta inset ring when they are the current one.
- **Internal Padding:** 16–24px.

### Badges
22px, 7px radius, 11.5px at 650. Tones: **new** (clay wash, fired terracotta text, with a 7px terracotta dot), **neutral** (washed linen), **warn** (ochre on ochre wash), **cut** (cut ground, on-fir text).

### Inputs / Fields
- **Style:** white, 40px min height, 10px radius, inset 1px hairline-strong, 14px text; label above at 13.5px semibold, 6px gap; hint below at 12.5px ink-3. Selects use a custom chevron; textareas start at 84px.
- **Focus:** inset 1px plus outer 2px terracotta ring.
- **Error:** inset 1.5px error ring, the message in semibold error text, wired with `aria-invalid` and `aria-describedby`.

### Notices
Inline messages in the flow of a panel: 12px radius, 10px 12px padding, 17px Lucide icon (Info, TriangleAlert, CircleAlert). Info is washed linen; warn and error use their soft grounds and are announced with `role="alert"`. An optional action sits on the right.

### Navigation (editor header)
Fir band, 80px. Wordmark at 19px/700 in on-fir (white on hover); save state under it in 12.5px on-fir-2. The price summary on the right is a transparent button with a 12px radius that turns fir-2 on hover or while open; its chevron flips. While a new price is being calculated, the value fades to 50% instead of disappearing.

### Plant label (signature)
The selected element as a nursery tag, floating top right: linen, Lift, 18px radius, 296px wide, 26px top padding under the punched hole. Contents top to bottom: a 118px hero with the rendered model on a warm radial backdrop and a status badge; the name at 21px/650; facts as a definition list with dotted leaders; the price span (23px/650) over a hairline; one action row (for plants, two 42px washed-linen rotate icon buttons, then Byt as a fir primary button and Ta bort as danger; while marked for removal, only Behåll); then a "Byt mot" list of alternatives with colour swatches and prices. Enters with a 200ms fade-and-drop.

### Shelf and article card (signature)
The bench along the bottom. Each article card is 150px: a 68px media well (12px radius, warm radial backdrop) showing a rendered model thumbnail, a colour swatch for surfaces and lines, or a pulsing placeholder while it renders; then name (13.5px/650), detail (12.5px ink-3) and price (12.5px/600). A 28px white "add" button with Lift Small sits bottom right of the media; hover turns it fir, a just-added card turns it terracotta. While placing, the media gets a terracotta inset ring and a small terracotta "placing" tag; out-of-stock articles are desaturated.

### Side panel
380px linen panel with Lift for Foton and Förslag, with a sticky header (19px title, close button). Proposal cards are white inner cards with a title, a reason, a price span and two equal buttons; the tried proposal carries the terracotta ring.

The **Drömbild** panel holds, top to bottom: the saved views (name, an eye icon to show it, a bin to remove it, and an accent "Skapa drömbild" button), the name field with "Spara den här vyn", the photo consent checkbox on a linen-2 block, the quota line, and the gallery. Gallery cards are white inner cards with a 3:2 picture, badges (Inspirationsbild, Testbild, Skapas …), the view name and date, and a quiet note when the design has changed; a failed card shows the faded 3D render, the explanation and a danger "Ta bort". Clicking a finished image opens it large on a fir veil with "Offerten gäller designen, inte bilden.", Ladda ner and Stäng.

### 3D scene look (`sceneLook.ts`)
A nursery afternoon in the scene itself: clear colour `scene-top`; hemisphere light from a warm sky (#FFF3E0) to an umber ground (#7A6A4F) at 1.15; a low golden sun (#FFE2B8, intensity 2.1) from the south-west (three.js position −14, 20, 12) casting PCF shadows. Selection is terracotta (ring under a selected plant, glow on surfaces and lines with emissive #5A2410); shaping handles are terracotta with the first corner in fir. Model colours come from the articles and are not tinted by the world. Edge-length labels while shaping are small linen tags (12px semibold).

### Motion
One easing (`cubic-bezier(0.2, 0.7, 0.2, 1)`) and one duration (160ms) for state changes on colour, shadow and transform. Entrances are short fade-and-drop (180–240ms). `prefers-reduced-motion` reduces all transitions and animations to near zero.

## Do's and Don'ts

### Do:
- **Do** put every page on the fir frame and every tool on linen; the scene sits in a 22px-radius window inset 16px.
- **Do** use terracotta for exactly one primary action per surface and for selection of garden objects, nothing else.
- **Do** mark the "on" state of a mode, tool or filter with fir and on-fir text.
- **Do** use only the tested text/ground pairs; keep `on-fir-3` on `fir` only.
- **Do** keep the global focus ring: 2px solid terracotta, 2px offset (inputs draw it as a box-shadow ring).
- **Do** use Lift for anything floating over the scene and tonal steps (linen → linen-2 → white) inside it.
- **Do** write prices as a span in SEK including VAT, with tabular numerals, and keep every visible string Swedish.
- **Do** take colours from `tokens.css` in CSS and from `palette.ts` in the 3D scene; add a new colour to both and to the contrast test.

### Don't:
- **Don't** use a grey CAD sidebar with a blue accent; that is the category default this world replaces.
- **Don't** put two terracotta buttons in one view, or use terracotta for headings, decoration or warnings.
- **Don't** draw solid grey borders; use hairline insets and rules.
- **Don't** put `on-fir-3` on `fir-2` or any lighter ground; it fails AA.
- **Don't** add a second typeface or set money in proportional numerals.
- **Don't** light or colour the scene with values outside `sceneLook.ts` and `palette.ts`, and don't tint article models with world colours.
