---
title: 0020 — Compare every lens in one request
---

# 0020 — Compare every lens in one request

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-18 asks that a user be able to see what letter a food would earn under the other lenses
**without switching the one they are on**. Story 9.1 asks for the same comparison on the public food
page, which has no account behind it at all.

`GET /api/foods/{id}/grade?lensId=…` already answers the question for one lens. Nothing stopped a
client from calling it three times, so the first question was whether a second endpoint earns its
place.

### The measurement that decided it

`tools/LensAgreementQuery` grades all 1 933 catalogue foods under all three lenses. Of the 1 852
that carry a letter under every lens:

| | |
|---|---|
| same letter under all three | 28.4% |
| one letter apart | 36.5% |
| two letters apart | 33.4% |
| three letters apart | 1.7% |

**71.6% of the catalogue changes letter when the lens changes.** The comparison has something to
show. Had the number come out near zero, the honest answer would have been to drop the feature
rather than ship three identical cells — which is why the tool measures letters and not scores:
scores always differ, and a measurement that cannot come out flat proves nothing.

Two pairs behave very differently:

| pair | disagree on |
|---|---|
| Fitness vs Weight Loss | 69.8% |
| Fitness vs GLP-1 | 68.6% |
| GLP-1 vs Weight Loss | **8.5%** |

GLP-1 carries the Weight Loss weighting on purpose — what makes it a different lens is which
nutrients density counts, not the three numbers. `Lens.cs` has said so since Story 1.6. The 8.5% is
the size of that difference, measured.

## Decision

**A second, plural endpoint**, taking no lens at all:

```
GET /api/foods/{id}/grades
```

```json
[
  { "lensId": "weight-loss", "name": "Weight Loss",
    "grade": { "grade": "B", "score": 67.77, "isPartial": false,
               "satiety": { "score": 83.14, "isEstimated": false }, "…": "…" } },
  { "lensId": "fitness", "name": "Fitness", "grade": { "grade": "C", "…": "…" } },
  { "lensId": "glp-1",   "name": "GLP-1",   "grade": { "grade": "B", "…": "…" } }
]
```

**Each entry nests the body of `GET /api/foods/{id}/grade` unchanged.** No second shape to keep in
step, and `GradeResponseDto` was not touched.

**The order is the order `GET /api/lenses` returns**, which is the order of `calibration.json`. A
client renders the cells in the order it receives them and never sorts.

**A lens with no letter keeps its entry**, with `grade: null` — the same meaning it has everywhere
else in the API. A nutritionally empty food answers with three entries and three nulls, not with an
empty list.

**No cookie.** Like `/grade`, it reads the catalogue and nothing about the caller, which is what
Story 9.1 needs and is also why the comparison cannot accidentally change anything: there is no
active lens in the request to change.

## Alternatives considered

**Three calls to `/grade?lensId=…`.** Three round trips for one screen, and the client has to fetch
`GET /api/lenses` first to know what to ask for. On the public page that is four requests to render
one panel.

**A flag on the existing endpoint — `/grade?allLenses=true`.** One endpoint returning two different
shapes depending on a query parameter. The response type would have to be a union, and every client
would branch on the flag it sent.

**An object keyed by lens id** — `{ "weight-loss": {…}, "fitness": {…} }`. Loses the display name,
and JSON objects carry no guaranteed order, so the render order would come back from a second call.

**Letters only, no breakdown.** UX-DR8 shows only the letter, so this is the smaller answer. Rejected
because Story 9.1 wants the breakdown on the same page, and carrying it costs nothing to build — it
is the DTO that already exists. A comparison that shows only letters is a client choosing to ignore
fields, not a server choosing to withhold them.

## Consequences

**The letter reads `grade.grade`.** The outer object is the lens, the inner one is the grade under
it, and both are called what they are. The alternative was renaming `GradeResponseDto.Grade` to
`Letter` across three endpoints for a cosmetic gain, on a shape no client consumes yet.

**The GLP-1 column will often be a copy of the Weight Loss one** — measured, 91.5% of the time. That
is the product being honest about what that lens is, not a bug to hide. Worth knowing before someone
looks at the screen and assumes the endpoint is broken.

**The food is read once and graded three times.** Grading three lenses through
`Grade(int foodId, Lens lens)` would have re-read the same row three times, which is the open item
already recorded against `Meals.GradeOf`. **No test distinguishes the two**, because both return the
same body — it is a query count, not a result. Written down here rather than left to be rediscovered.
