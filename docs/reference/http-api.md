---
title: HTTP API
---

# HTTP API

The complete surface of `Sated.Api`. Everything not listed here does not exist yet.

!!! note "State"
    Five endpoints. Three of them read the catalogue from PostgreSQL; the other two answer from
    `calibration.json` alone. There is still **no authentication and nothing is written** — every
    request is a read, and the same request produces the same answer (NFR2). Epic 2 adds accounts.

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

All five endpoints are covered by `server/Sated.Api.Tests`, which runs the real application in
memory through `WebApplicationFactory` — the same validation, the same container, the same engine.
The two catalogue endpoints run against a real PostgreSQL in a second database, `sated_test`, for
the reasons in [0007](../decisions/0007-test-the-foods-query-against-a-real-database.md).

## `GET /api/lenses`

The goal profiles a user can choose between (FR-23, FR-25). This is what the onboarding screen of
Story 2.2 offers, and it is the only place those three lenses come from — the client must not
carry its own copy.

**Request** — no parameters, no body.

**Response** — `200 OK`, always. The lenses are read from `calibration.json` at startup, so a
malformed file stops the server rather than failing this call.

```json
[
  { "id": "weight-loss", "name": "Weight Loss", "satiety": 50, "density": 35, "proteinQuality": 15 },
  { "id": "fitness",     "name": "Fitness",     "satiety": 25, "density": 25, "proteinQuality": 50 },
  { "id": "glp-1",       "name": "GLP-1",       "satiety": 50, "density": 35, "proteinQuality": 15 }
]
```

| Field | Type | Meaning |
|---|---|---|
| `id` | string | Stable identifier. This is what a client sends back and what a database stores |
| `name` | string | Display name, exactly as `calibration.json` writes it. Never store it |
| `satiety` | number | Percentage weight of the satiety component |
| `density` | number | Percentage weight of the nutrient density component |
| `proteinQuality` | number | Percentage weight of the protein quality component |

The three weights sum to 100. `Lens` refuses to construct otherwise.

### Why the id and the name are two fields

A display name is a product decision: it gets reworded, and the international addendum will have it
translated. An identifier is a promise that nothing else can change.

Collapsing the two means every stored preference points at a label. Rename `Weight Loss` to
`Fat Loss` and every user who chose it now refers to a lens that does not exist — and nothing fails
loudly, because a lens that cannot be found simply is not found.

So `id` is the only thing that crosses the boundary in either direction, and it is the only thing
the engine keys on: the letter cutoffs and the category rules are both looked up by id. `name`
exists to be printed.

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
  "lensId": "weight-loss",
  "calories": 165, "protein": 31, "fat": 3.6, "fiber": 0,
  "saturatedFat": 1.0, "sodium": 74, "carbohydrate": 0,
  "potassium": 256, "magnesium": 29, "iron": 0.7, "calcium": 15
}
```

| Field | Required | Notes |
|---|---|---|
| `lensId` | yes | One of the ids from `GET /api/lenses`, matched without case. Not the display name |
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
| `lensId` names a lens `calibration.json` does not carry | 400 | `LensId` |
| The energy does not follow from the macronutrients | 400 | `Calories` |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "LensId": ["No lens has the id 'keto'. GET /api/lenses lists the ones that exist."] }
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

## `GET /api/foods`

The catalogue, paged. 1 933 foods loaded once from FNDDS 2021-2023
([0005](../decisions/0005-fndds-is-the-catalogue.md)) and owned by Sated since
([0006](../decisions/0006-load-the-catalogue-once-then-own-it.md)).

**Request** — four optional query parameters, all combinable.

| Parameter | Type | Default | Meaning |
|---|---|---|---|
| `search` | string | none | Case-insensitive "contains" on the description. PostgreSQL runs it as `ILIKE` |
| `category` | string | none | Exact match on the FNDDS category, letter case included |
| `page` | int | `1` | 1-based. Below 1 is a `400` |
| `pageSize` | int | `25` | At most `100`. Above that is a `400` |

A raw space ends the URL in an HTTP request line. Categories that contain one must be written
`Chicken,%20whole%20pieces`.

**Response** — `200 OK`, an envelope rather than a bare array.

```json
{
  "items": [
    { "id": 6626, "description": "Broccoli, raw", "category": "Broccoli" }
  ],
  "page": 1,
  "pageSize": 25,
  "total": 1
}
```

| Field | Type | Meaning |
|---|---|---|
| `items` | array | The rows on this page, ordered by description |
| `page` | number | Echo of the page asked for |
| `pageSize` | number | Echo of the size asked for |
| `total` | number | How many rows match the filters, **before** paging cuts them |

A page past the last one is `200` with an empty `items` and the real `total`, not a `404`. Asking
for a page that does not exist is not an error; asking for one that cannot exist is.

### Why a list row carries no nutrients

A row is `id`, `description`, `category` and nothing else. The query projects three columns, so the
sixteen nutrient columns never leave the database for a list. The nutrients are what
`GET /api/foods/{id}` is for.

### Why the order is fixed

Without an `ORDER BY`, PostgreSQL owes no promise that two requests return rows in the same order —
so page 2 could repeat or skip what page 1 showed. The order is by description, and the database
resolves it with its own collation (`en_US.utf8`), not with .NET's string comparison.

## `GET /api/foods/{id}`

One food, with the nutrients the list leaves out.

**Request** — `id` is the database key. It is stable for the life of the row
([0006](../decisions/0006-load-the-catalogue-once-then-own-it.md)), which is what makes it safe to
store in a URL, a log entry or a foreign key.

Ids in the loaded catalogue run from 5347 to 7279. They do not start at 1: the identity sequence
kept counting through the reloads that happened before 0006 stopped them.

**Response** — `200 OK`.

```json
{
  "id": 5348,
  "fdcId": 2705385,
  "description": "Milk, whole",
  "category": "Milk, whole",
  "nutrients": {
    "calories": 61, "protein": 3.27, "fat": 3.2, "fiber": 0,
    "saturatedFat": 1.86, "sodium": 38,
    "vitaminA": 32, "vitaminC": 0, "vitaminD": 1.1, "vitaminE": 0.05,
    "thiamine": 0.056, "calcium": 123, "iron": 0, "magnesium": 12,
    "potassium": 150, "leucine": null
  }
}
```

| Field | Type | Meaning |
|---|---|---|
| `id` | number | The stable key |
| `fdcId` | number \| null | The USDA row these numbers came from. `null` for a food typed in by hand |
| `description` | string | The name, as the catalogue carries it. This is the field a translation replaces |
| `category` | string | One of the 71 FNDDS categories. Read by the scoring rules, never translated |
| `nutrients` | object | Per 100 g. Six always present, ten nullable |

**`404 Not Found`** covers both an id with no row and anything that is not a number. The route is
declared `{id:int}`, so `/api/foods/milk` matches no route at all — it never reaches the action and
never becomes a `400`.

### null is not zero

A nullable nutrient comes back as `null` when the catalogue has no value for it. That is a
different statement from `0`, and the scoring engine treats it differently: a missing value is
estimated and the result is marked `isEstimated`, while a zero is a measurement.

`leucine` is `null` on all 1 933 rows. The engine does not read it from here — `ProteinCompleteness`
derives it from the food's protein and category, and marks the result estimated. The column is
carried for the day a food arrives with a measured value.

### Why the nutrients are nested

The response has two kinds of field: what identifies the food, and what was measured about it.
Sixteen numbers flattened into the root would bury `id` and `description` among them. The nesting
also matches the database, where the nutrients are an owned type on `Food`
([0004](../decisions/0004-nutrients-are-an-owned-type-on-food.md)).

Note that `POST /api/grades` takes its nutrients **flat**, in the request root. The two shapes are
not inconsistent: one is a food we hold, the other is a measurement someone hands us.

## `GET /api/foods/{id}/grade`

The letter of a catalogue food. The database supplies the food, the engine supplies the letter, and
nobody has to send nutrients.

**Request** — `id` in the path, `lensId` in the query. Both are required.

| Parameter | Where | Meaning |
|---|---|---|
| `id` | path | The food's stable key, as `GET /api/foods` returns it |
| `lensId` | query | The slug of a lens: `weight-loss`, `fitness`, `glp-1`. Matched ignoring case |

**Response** — `200 OK`, in **exactly** the shape of `POST /api/grades`, so a client can handle
both the same way.

```json
{
  "grade": "B",
  "score": 67.77,
  "isPartial": false,
  "satiety":        { "score": 83.14, "isEstimated": false },
  "density":        { "score": 61.55, "isEstimated": false },
  "proteinQuality": { "score": 31.04, "isEstimated": true  },
  "fatQuality":     { "score": 40.22, "isEstimated": false }
}
```

**`400 Bad Request`** when `lensId` is missing or names no lens — the same `ProblemDetails` body
that `POST /api/grades` returns, with the error on the `lensId` field. A missing lens and an
unknown one are the same failure, so they read the same.

**`404 Not Found`** when no food carries that id, and when the id is not a number.

### Protein quality is always estimated here

`proteinQuality.isEstimated` is `true` for **every** catalogue food. FNDDS carries no amino acid
data, so `leucine` is null on all 1 933 rows and the engine derives it from the food's protein and
category. The flag is not a defect — it is the engine refusing to let a guess read as a measurement.

Compare with `POST /api/grades`, where a client that sends a measured leucine gets
`isEstimated: false`.

### What connects the two halves

The database holds a `Food`; the engine reads a `FoodInput`. `ScoringInput.From` in
`Sated.Services` translates one into the other, and it is the only place that does. Three things it
decides:

- **The category passes through unchanged.** A category no rule knows is not turned into `null` —
  in the engine, `null` means "this food has no category at all", which is what a hand-typed food
  is. Those are different states.
- **Leucine passes through as it is**, marked *not* estimated. A value the catalogue carries is a
  measurement; a missing one leaves the engine to estimate and to mark its own result.
- **Carbohydrate does not appear**, because the engine never scores it. It is required by
  `POST /api/grades` only so `NutrientPlausibility` can check the energy — a check that exists for
  numbers a client typed, not for numbers loaded from USDA.

### grade: null is not a bad grade

Tap water (`id` 7242) answers `200` with `"grade": null`, `"density": null` and
`"isPartial": true`. There is nothing to grade: no energy means the density score has nothing to
divide by. The product shows no letter at all — never an E.

## Breaking change: `lens` became `lensId`

`POST /api/grades` used to take the display name in a field called `lens`. It now takes the slug in
a field called `lensId`, and the old name is not accepted.

The field was renamed rather than left in place because the value changed underneath it. A client
still sending `{"lens": "Weight Loss"}` gets `lensId is required`, which is the truth, instead of
`no lens is named 'Weight Loss'`, which reads like a broken server.
