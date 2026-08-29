---
title: HTTP API
---

# HTTP API

The complete surface of `Sated.Api`. Everything not listed here does not exist yet.

!!! note "State"
    Two endpoints. There is **no authentication, no database and no catalogue** behind this API —
    it serves what the scoring engine can answer from `calibration.json` alone. Nothing is stored:
    the same request always produces the same answer (NFR2). Epic 2 adds accounts; Epic 3 adds the
    catalogue.

## Running it

```bash
dotnet run --project server/Sated.Api --launch-profile http
```

Base address: `http://localhost:5227`.

The `http` launch profile declares no HTTPS port, so `UseHttpsRedirection` logs
`Failed to determine the https port for redirect` at startup and passes requests through. That
warning is expected locally. HTTPS is a deployment concern.

## Conventions

| | |
|---|---|
| **Content type** | `application/json; charset=utf-8` |
| **Property casing** | `camelCase`, converted from the C# `PascalCase` by `System.Text.Json` |
| **Errors** | [`ProblemDetails`](https://datatracker.ietf.org/doc/html/rfc9457), supplied by `[ApiController]` |
| **Versioning** | none — the API has no external consumers yet |
| **Auth** | none — see Epic 2 |

Route paths derive from controller class names via `[Route("api/[controller]")]`, lowercased by
`LowercaseUrls`. Matching ignores case either way; the setting fixes the form the OpenAPI document
declares, which is what a generated client copies.

Both endpoints are covered by `server/Sated.Api.Tests`, which runs the real application in memory
through `WebApplicationFactory` — the same validation, the same container, the same engine.

## `GET /api/lenses`

The goal profiles a user can choose between (FR-23, FR-25). This is what the onboarding screen of
Story 2.2 offers, and it is the only place those three names come from — the client must not carry
its own copy.

**Request** — no parameters, no body.

**Response** — `200 OK`, always. The lenses are read from `calibration.json` at startup, so a
malformed file stops the server rather than failing this call.

```json
[
  { "name": "Weight Loss", "satiety": 50, "density": 35, "proteinQuality": 15 },
  { "name": "Fitness",     "satiety": 25, "density": 25, "proteinQuality": 50 },
  { "name": "GLP-1",       "satiety": 50, "density": 35, "proteinQuality": 15 }
]
```

| Field | Type | Meaning |
|---|---|---|
| `name` | string | Display name, verbatim from `calibration.json` |
| `satiety` | number | Percentage weight of the satiety component |
| `density` | number | Percentage weight of the nutrient density component |
| `proteinQuality` | number | Percentage weight of the protein quality component |

The three weights sum to 100. `Lens` refuses to construct otherwise.

### What is deliberately absent

The engine's `Calibration` object also holds the letter cutoffs, the measured percentile scales,
the category dispatch table and the catalogue's category list. None of it is in this response.

That is the boundary doing its job: a client has no use for a cutoff, and returning the engine
object directly would make every field ever added to the engine public without anyone deciding so.

### Why GLP-1 and Weight Loss share their weights

They are not duplicates. What separates the GLP-1 lens is **which nutrients its density score
counts** — it adds vitamin D and thiamine — not how it weighs the three components. That field is
internal to the engine and does not appear in this response. See
[The four scores](../explanation/the-four-scores.md).

## `POST /api/grades`

The letter a food earns under one lens (FR-4, FR-5). The caller sends nutrients, not a food: this
API has no catalogue, so there is nothing to look a name up in.

### Request

Everything is **per 100 g**. Protein, fat, fibre, saturated fat, carbohydrate and alcohol are
grams; vitamin A and vitamin D are micrograms; every other micronutrient is milligrams.

```json
{
  "lens": "Weight Loss",
  "calories": 165, "protein": 31, "fat": 3.6, "fiber": 0,
  "saturatedFat": 1.0, "sodium": 74, "carbohydrate": 0,
  "potassium": 256, "magnesium": 29, "iron": 0.7, "calcium": 15
}
```

| Field | Required | Notes |
|---|---|---|
| `lens` | yes | One of the names from `GET /api/lenses`, matched without case |
| `calories` | yes | Kilocalories. Kilojoules are rejected — see below |
| `protein`, `fat`, `fiber` | yes | |
| `saturatedFat`, `sodium` | yes | The two limiters: an absent limiter would raise a score, so dropping one is never the safe direction |
| `carbohydrate` | yes | Never scored. It exists so the energy can be checked against the macronutrients |
| `category` | no | The catalogue's own category name. Absent means a food a person typed in, which selects a different set of rules |
| `alcohol` | no | Grams of ethanol. Absent for anything that is not a drink |
| `vitaminA`, `vitaminC`, `vitaminE`, `calcium`, `iron`, `magnesium`, `potassium`, `vitaminD`, `thiamine` | no | |

**A micronutrient left out is unknown, never zero.** Zero claims the food contains none of it, and
measured on the gate's 68 foods that claim costs exactly one letter on twenty of them. The response
says which components stood on estimates.

### Response

`200 OK`.

```json
{
  "grade": "A",
  "score": 88.03,
  "isPartial": false,
  "satiety":       { "score": 86.13, "isEstimated": false },
  "density":       { "score": 85.62, "isEstimated": true  },
  "proteinQuality":{ "score": 100,   "isEstimated": true  },
  "fatQuality":    { "score": 69.00, "isEstimated": false }
}
```

| Field | Type | Meaning |
|---|---|---|
| `grade` | `"A"`–`"E"` or `null` | The letter. Serialised as a letter, not as an enum position |
| `score` | number | 0–100 under this lens, before the letter cutoffs |
| `isPartial` | boolean | A component was unavailable and its weight went to the others (FR-7) |
| `satiety` | object | Never null — every gradeable food carries the four nutrients it needs |
| `density`, `proteinQuality`, `fatQuality` | object or `null` | Null means the component did not count at all |
| `isEstimated` | boolean | The number stands in for data the request did not carry |

**`grade: null` is not a bad grade.** Water and diet drinks carry neither energy nor any
macronutrient, so every score in this engine has nothing to divide by. Graded anyway, 89 such foods
spread across four letters and 77.7 points, with tap water inverting against every drink it is
nutritionally better than. The client must show no letter at all, never an E.

### Errors

All four use the same
[`ProblemDetails`](https://datatracker.ietf.org/doc/html/rfc9457) shape, so a client parses one
format. The keys in `errors` are the request's field names.

| Cause | Status | Key |
|---|---|---|
| A required field is missing | 400 | each missing field, all at once |
| A value is out of range — negative grams, more than 100 g per 100 g | 400 | the field |
| `lens` names a lens `calibration.json` does not carry | 400 | `Lens` |
| The energy does not follow from the macronutrients | 400 | `Calories` |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Lens": ["No lens is named 'Keto'. GET /api/lenses lists the ones that exist."] }
}
```

The first two come from the annotations on the request type and are checked before the action runs.
The last two need `calibration.json` and the engine, so they are checked inside it and pushed into
the same shape.

### Why kilojoules are rejected

European nutrition data reports energy in kilojoules — 4.184 times the number this engine wants.
That value passes every other check and simply grades wrong. `NutrientPlausibility` compares the
declared energy against what the macronutrients imply and refuses a ratio outside 0.5–2.0, a band
that rejects none of the 5,157 catalogue foods above its floor.

**What it cannot catch:** European labels print salt in grams where this engine wants sodium in
milligrams, and 1.2 g of salt is 480 mg of sodium. Writing `1.2` into `sodium` is perfectly
plausible and grades wrong. That conversion belongs to whoever imports the data.

## Open: lenses have no stable identifier

A lens is identified by its display name. Story 2.2 will store a user's choice, and storing
`"Weight Loss"` means a later rename in `calibration.json` orphans every stored preference.

The fix is a stable slug separate from the label, which touches `calibration.json`, `Lens.cs` and
`Calibration.cs` — the engine. It is deliberately not done yet: the cost only buys something once
a choice is actually persisted. Decide it in Story 2.2, before the first write.
