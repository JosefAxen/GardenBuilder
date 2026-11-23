# AI evaluation

20 real garden photos with expected results. Run before any prompt or model change. Not part of CI.
Photos are personal data: keep them out of git (store in Blob Storage) and commit only the expected results.

## Photo interpretation trial (2026-11-18)

The first trial used five photos in `evals/photos/` (git-ignored) and a throwaway script that sent each photo to
Haiku 5.5, Sonnet 5.5 and Opus 5.5 with the same question as `PhotoPrompt` and drew the answers as maps seen from above.

| Model | Cost per photo | Time per photo | Result |
| --- | --- | --- | --- |
| Haiku 5.5 | 0.02 kr | ~14 s | Roughly right, sometimes messy |
| Sonnet 5.5 | 0.15 kr | ~9 s | Good, the fastest (chosen) |
| Opus 5.5 | 0.57 kr | ~24 s | Best, sees corners and details |

What it showed: all three find the lawn, beds, hedges, fences and the house colour; the scale is uncertain by a few
meters; the hard part is placing several photos on the plot, which is why the customer shows where each photo was taken.

A run through the app on one photo (the yellow wooden house) gave 11 proposals: lawn, two beds, a hedge, a railing, a
trellis, four shrubs and two birches, plus the house (yellow walls, red-brown gable roof). Three things were left out:
two outside the plot and a shed (not supported yet).

To rerun before changing `PhotoPrompt` or the model: put the photos in `evals/photos/`, upload them in the editor's
"Foton" panel, show where each was taken and compare the proposals with the photos. Raise `PhotoPrompt.Version` with
every change.

## Design proposal trial (2026-11-19)

One round in the app on a 24 × 18 m test garden with the example catalog (style Naturlig, budget 50 000–150 000 kr,
care Medel, zone III), Sonnet 5.5:

- Six calls (both proposals used all three attempts), 27 000 input and 14 000 output tokens, 2.09 kr, about 97 s of model
  time and 2.5 minutes in all.
- "Vild äng och trädäck" survived: a deck against the south facade with a bench and a pot, three perennial beds, a
  gravel path, a hedge, new lawn and a tree; 71 new elements, 88 600–140 900 kr. The fruit trees were dropped (no room).
- The bolder proposal still had errors after its retries and was left out.

What to improve before launch: the model often asks for more than fits. Telling it each zone's free area and the
largest patio per facade in the prompt should cut retries. Keep the failing errors of dropped proposals in the result
so they can be studied.

To rerun before changing `ProposalPrompt` or the model: shape a test garden, open "Förslag", run a round with fixed
wishes and compare the cards and the 3D view. Raise `ProposalPrompt.Version` with every change.

## Dream image: choosing the image service (not run yet)

The dream image runs in test mode (`Images:Provider = "test"`) until the keys exist. To compare Google and Black Forest
Labs once they do:

1. Josef sets the keys as user secrets on the Worker, never in appsettings or git:
   `Images:GoogleApiKey` and `Images:BflApiKey` (the planned names; the two provider adapters are not written yet).
2. Give every model a price under `Ai:Prices` (`PerImageUsd`) in both the Api and the Worker, or the Worker refuses to
   start.
3. Shape a test garden with plants, upload and place two photos, and save the same three views (from the street, from
   the patio, from above the lawn).
4. For each provider, set `Images:Provider` and `Images:Model`, restart, and make one image per view with photos and one
   without. That is 6 images per provider; reset the quota between runs by using a new garden.
5. Compare side by side: are the plants where the design has them, does the house keep its shape and colour, is
   anything added that is not in the design, and how close is it to the photos. Note time and cost from the AI call log.

Raise `DreamPrompt.Version` with every prompt change, and keep the photos in `evals/photos/` (git-ignored).
