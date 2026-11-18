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
