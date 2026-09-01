---
title: HTTP API
---

# HTTP API

The complete surface of `Sated.Api`. Everything not listed here does not exist yet.

!!! note "State"
    Fourteen endpoints. Three read the catalogue from PostgreSQL, two answer from
    `calibration.json` alone, four handle accounts and five handle onboarding. The catalogue endpoints are still open to anyone and still
    only read: the same request produces the same answer (NFR2). `POST /api/auth/register` is the
    first endpoint in the API that **writes**.

## Running it

```bash
dotnet run --project server/Sated.Api --launch-profile https
```

Base address: `https://localhost:7245`, with `http://localhost:5227` still answering.

**Use the `https` profile for anything under `/api/auth`.** The session cookie is marked `Secure`,
so a client on plain http is sent the cookie and discards it: registering answers `200` and the
next request is still `401`, with nothing in the logs to say why. The catalogue endpoints do not
care and work on either address.

The `http` profile declares no HTTPS port, so `UseHttpsRedirection` logs
`Failed to determine the https port for redirect` at startup and passes requests through. That
warning is expected on that profile.

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
  "slug": "milk-whole",
  "category": "Milk, whole",
  "nutrients": {
    "calories": 61, "protein": 3.27, "fat": 3.2, "fiber": 0,
    "saturatedFat": 1.86, "sodium": 38,
    "vitaminA": 32, "vitaminC": 0, "vitaminD": 1.1, "vitaminE": 0.05,
    "thiamine": 0.056, "calcium": 123, "iron": 0, "magnesium": 12,
    "potassium": 150, "leucine": null
  },
  "provenance": { "source": "UsdaFndds", "estimated": ["leucine"], "absent": [] },
  "typicalGrams": 244,
  "servings": [
    { "description": "1 cup", "grams": 244 },
    { "description": "1 fl oz", "grams": 30.5 },
    { "description": "1 individual school container", "grams": 244 },
    { "description": "Guideline amount per fl oz of beverage", "grams": 2.5 },
    { "description": "Guideline amount per cup of hot cereal", "grams": 61 }
  ]
}
```

| Field | Type | Meaning |
|---|---|---|
| `id` | number | The stable key |
| `fdcId` | number \| null | The USDA row these numbers came from. `null` for a food typed in by hand |
| `description` | string | The name, as the catalogue carries it. This is the field a translation replaces |
| `slug` | string \| null | The name this food's public page is at. `null` for a food somebody added for themselves |
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

## `GET /api/foods/by-slug/{slug}`

The same food as `GET /api/foods/{id}`, named by its slug instead of its id. It exists because a
public food page is reached from a search engine, which knows a name and not a number.

**Request** — `slug` is the description in lowercase, with every run of characters that are not
letters or digits turned into one hyphen. `Rice, wild, 100%, cooked, NS as to fat` becomes
`rice-wild-100-cooked-ns-as-to-fat`.

The slug is **stored on the row**, not computed per request, so it can be indexed and looked up.
It is set once, when the food is imported, and never changes — the description never changes
either.

**Response** — `200 OK`, byte for byte the body `GET /api/foods/{id}` returns for the same food.
Anonymous: the page has to work before anyone signs up.

**`404 Not Found`** covers four cases, and they are the same answer on purpose:

| | |
|---|---|
| no food carries that slug | there is no page |
| the slug in another letter case | `Milk-Whole` is not `milk-whole`; the lookup is exact |
| the description instead of the slug | `Milk,%20whole` is not a slug |
| the slug of a food somebody added for themselves | those never get a public page (FR-11) |

Only catalogue rows carry a slug. A food created through `POST /api/foods` comes back with
`slug: null`, and that is what keeps it off the public web
([0022](../decisions/0022-a-public-page-is-reached-by-a-slug.md)).

### Two requests build the page, not one

The page needs the food, its grade under every lens, and the breakdown. That is this endpoint for
the food, then `GET /api/foods/{id}/grades` with the id it returns. Both are anonymous, and both
are the endpoints the signed-in app already calls — Story 9.1 asks for no parallel path to the
same data.

### The slug is not truncated

The longest slug in the catalogue is 110 characters. Measured by `tools/SlugQuery`, cutting slugs
to a maximum length is what *creates* collisions: none at 100 characters, 10 at 80, 25 at 60, 223
at 40. The two longest slugs are both 110 characters and first differ at character **100**.

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

## `GET /api/foods/{id}/grades`

The same food under every lens at once, so the comparison can be shown without switching the lens
the user is on (Story 5.3). Plural, and it takes no `lensId`: asking for all of them is the point.

**Request** — `id` in the path. Nothing else, and no cookie.

**Response** — `200 OK`, one entry per lens, in the order `GET /api/lenses` returns.

```json
[
  { "lensId": "weight-loss", "name": "Weight Loss",
    "grade": { "grade": "B", "score": 67.77, "isPartial": false,
               "satiety":        { "score": 83.14, "isEstimated": false },
               "density":        { "score": 61.55, "isEstimated": false },
               "proteinQuality": { "score": 31.04, "isEstimated": true  },
               "fatQuality":     { "score": 40.22, "isEstimated": false } } },
  { "lensId": "fitness", "name": "Fitness", "grade": { "grade": "C", "score": 51.69, "…": "…" } },
  { "lensId": "glp-1",   "name": "GLP-1",   "grade": { "grade": "B", "score": 71.01, "…": "…" } }
]
```

Each `grade` object is the body of `GET /api/foods/{id}/grade`, unchanged — so **the letter reads
`grade.grade`**. The outer object is the lens, the inner one is the grade under it.

**`404 Not Found`** when no food carries that id, and when the id is not a number. A missing food is
never an empty list.

A food with no letter keeps its entry, with `grade: null` — tap water answers with three entries and
three nulls.

### What the comparison is worth

Measured over the whole catalogue by `tools/LensAgreementQuery`: **71.6% of foods change letter
between lenses.** Weight Loss and Fitness disagree on 69.8% of them; GLP-1 and Weight Loss on only
8.5%, because those two carry the same weights and differ solely in which nutrients density counts.
Expect the GLP-1 column to repeat the Weight Loss one most of the time. See
[0020](../decisions/0020-compare-every-lens-in-one-request.md).

## `GET /api/foods/{id}/swap`

Three foods from the same category that carry a better letter under that lens (Story 6.1). Returned
only when asked for: nothing else in the API offers a swap.

**Request** — `id` in the path, `lensId` in the query. Both required. No cookie.

**Response** — `200 OK`, best score first.

```json
{
  "alternatives": [
    { "id": 6410, "description": "Nectarine, raw", "grade": "A", "score": 75.67 },
    { "id": 6417, "description": "Peach, canned, juice pack", "grade": "A", "score": 75.19 },
    { "id": 6414, "description": "Peach, raw", "grade": "A", "score": 75.03 }
  ],
  "message": null
}
```

When nothing in the category carries a better letter, the same `200` comes back with an empty list
and the message instead:

```json
{ "alternatives": [], "message": "No higher-graded foods in this category." }
```

**`400 Bad Request`** when `lensId` is missing or names no lens, exactly as `/grade` answers.

**`404 Not Found`** when no food carries that id, and when the id is not a number. Having nothing to
suggest is never a 404.

### The two rules that pick the three

**A candidate must beat the food on the letter.** An A is offered to a B; another B is not, however
much better its score. The letter is the product's public claim, and a swap that leaves it unchanged
contradicts it.

**Among candidates, the order is by score**, descending — an A at 95 really is a better answer than
an A at 71. An exact tie goes to the lower id, which is what makes the same request answer the same
way every time.

A food you entered yourself is never suggested, to you or to anybody: only catalogue rows are
candidates, so a signed-in caller and a stranger get the same three.

### What the empty answer means

Measured over the catalogue: **44.0% of foods have no better letter in their category**. The empty
answer is the normal case, not a failure — and the same measurement is the reason the selection rule
is worth reading before changing. See
[0021](../decisions/0021-a-swap-beats-the-letter-and-is-ranked-by-score.md).

## Breaking change: `lens` became `lensId`

`POST /api/grades` used to take the display name in a field called `lens`. It now takes the slug in
a field called `lensId`, and the old name is not accepted.

The field was renamed rather than left in place because the value changed underneath it. A client
still sending `{"lens": "Weight Loss"}` gets `lensId is required`, which is the truth, instead of
`no lens is named 'Weight Loss'`, which reads like a broken server.

---

## `POST /api/auth/register`

Creates an account and signs the caller in, in one request.

### Request

```json
{ "email": "florin@sated.test", "password": "abcdefghijkl" }
```

Both fields are required. `email` must parse as an address.

**The password rule is length alone: at least 12 characters.** No digit, no capital letter and no
symbol is required, and adding one buys nothing. Composition rules push people towards
`Parola1!` — short, predictable, and annoying to type. The reasoning and the source are in the
private `02_planning/6.prd-addendum-account-security` §3.2.

The rule lives in `Program.cs`, in Identity's options, and nowhere else. The DTO deliberately does
not repeat it as an annotation: two copies of one number drift.

### Response

```json
{ "id": "ae0f05fd-c297-43eb-bcf7-49760614b83c", "email": "florin@sated.test" }
```

`200`, plus a `Set-Cookie` header carrying `sated.session`. **The caller is signed in already** —
there is no second call to log in after registering.

The cookie is marked `HttpOnly`, `Secure` and `SameSite=Lax`. `HttpOnly` is the one that matters:
JavaScript cannot read the cookie at all, so a script that gets onto the page cannot carry the
session away with it. See [0008](../decisions/0008-keep-the-session-in-an-httponly-cookie.md).

### Errors

| Case | Status | Body |
|---|---|---|
| Missing or malformed email | `400` | The automatic `[ApiController]` validation problem |
| Password under 12 characters | `400` | `PasswordTooShort` |
| Email already registered | `400` | `DuplicateEmail` **and** `DuplicateUserName` |

The last row is a **deliberate leak**, and the only one in the API. Everything else refuses to say
whether an address has an account; registration has to say it, because otherwise nobody can tell
why they are unable to sign up. It disappears once the answer can be sent by email instead
(FR-33).

Both error codes appear because the username is set to the email. There is no separate username
today — see `UQ4` in the private UX decision log.

## `POST /api/auth/login`

```json
{ "email": "florin@sated.test", "password": "abcdefghijkl" }
```

`200` with the same body as register, plus a fresh cookie.

### Every failure answers the same

| Case | Status |
|---|---|
| Wrong password | `401` |
| No account with that email | `401` |
| Account locked after five wrong attempts | `401` |
| More than ten attempts in a minute from one address | `429` |

The three `401`s are identical down to the body — only the trace id differs, and that changes on
every request anyway. **In a nutrition product, who has an account is itself health information**,
so the API does not confirm an address from the outside.

The locked account answering `401` rather than saying so is a conscious trade: it costs a confused
user five minutes, and it keeps the third case from becoming a way to test whether an address is
registered.

### Two limits, two different attacks

**The lockout** — five wrong attempts, then five minutes — protects **one account** from having
its password guessed.

**The rate limit** — ten attempts a minute from one address — protects **every other account**.
Trying one common password against ten thousand accounts gives each account a single attempt, so
no lockout ever triggers. Only the rate limit sees that. Neither measure covers the other's case.

The limit is `RateLimits:LoginPerMinute` in configuration, default 10, partitioned by remote
address.

## `POST /api/auth/logout`

No body. `204`, and the cookie is cleared.

`401` when there is no session — logging out of nothing is not success.

## `GET /api/auth/me`

`200` with `id` and `email` for the signed-in caller. `401` otherwise.

The `401` carries **no `Location` header**. Identity's cookie handler would name `/Account/Login`
there, a page that does not exist in an API; a client that followed it would get a `404` instead of
reading the authentication error. The redirect event is overridden in `Program.cs` to stop that.

### The session is a cookie, not a row

Signing out clears the cookie in that browser. A copy taken somewhere else stays valid until it
expires, unless the user's security stamp changes — which is what happens on a password change or
account deletion. `SecurityStampValidationInterval` decides how fast that takes effect and is set
to zero here, so it is checked on every request. The cost is one lookup by primary key.

---

## `GET /api/consents/{purpose}`

Requires a session. `purpose` is `HealthData`; it is the only one so far.

```json
{
  "purpose": "HealthData",
  "version": "2026-08-31",
  "text": "Sated needs two kinds of information about you that count as health data...",
  "givenAt": null
}
```

`text` is the wording in force, in full. The onboarding screen shows exactly this and nothing it
composes itself, because the version the user signs has to be the version they read.

`givenAt` is `null` until this user consents, and the timestamp afterwards. One call tells the
screen both what to display and whether to display it at all.

## `POST /api/consents/{purpose}`

```json
{ "version": "2026-08-31" }
```

**The version is required, and it is the point.** A client cannot consent to a text it never
fetched: a version that was never published is a `400`. Without it, a stale screen could record
agreement to wording nobody had seen.

`200`, with `givenAt` filled in. Sending it again returns the **same** `givenAt` — one standing
consent per purpose, not a row per click.

## `DELETE /api/consents/{purpose}`

`204`. `404` when there is nothing standing to withdraw.

**Withdrawing erases what the consent covered** — today the weight; food logs when they exist. That
sentence is in the consent text itself, before anyone accepts it, because it is the real
consequence: Sated cannot work without that data, so withdrawing empties the product. The account
survives and can still sign in.

One `POST` and one `DELETE`, deliberately symmetric. Withdrawal has to be as easy as consent, and
"delete your entire account" is not as easy.

`ActiveLensId` is left in place — see [0009](../decisions/0009-consent-is-a-document-and-a-signature.md).

## `GET /api/profile`

```json
{
  "weightKg": 82, "heightCm": 180, "calorieTargetKcal": 2000,
  "activeLensId": "weight-loss", "healthDataConsentGiven": true
}
```

All three values are `null` for an account that has not finished onboarding. That is not an error state
and does not need one: registering and onboarding are two moments, and the gap between them is
ordinary.

## `PUT /api/profile`

```json
{ "weightKg": 82, "heightCm": 180, "activeLensId": "weight-loss" }
```

Returns the stored profile. Weight is in kilograms and must be between 20 and 500 — a sanity check
on typing, not a statement about who may use the product. Height is in centimetres, between 100 and
250, and is required for the same reason weight is: the protein target of
[0017](../decisions/0017-derive-the-protein-target-from-adjusted-body-weight.md) is computed from
both, and weight alone gives a target that is wrong for exactly the people this product is for.

`activeLensId` is the slug from `GET /api/lenses`, matched case-insensitively; an unknown one is a
`400` naming the field, the same shape `GET /api/foods/{id}/grade` uses.

### Why this can answer 403

| | |
|---|---|
| No session | `401` |
| Session, no consent on file | **`403`** |
| Consent, bad lens or bad weight | `400` |

The `403` is the consent rule expressed as behaviour. Weight is health data; storing it without a
recorded basis is not something the API will do, and a rule that only exists in documentation is
not a rule. The body names the request to make first.

`403` rather than `400`: the request is well formed and the caller is known. What is missing is
permission.

### Changing the lens regrades everything, including the past

A grade is computed when it is read, never stored
([0018](../decisions/0018-the-day-is-one-plate.md)), so switching the lens here changes every
letter the product will show from the next request on — today's meals, a day logged in March, the
day ring. There is no migration, no backfill and no invalidation step, because there is nothing
holding an old letter.

**What is stored is untouched.** A meal keeps its id, its entries keep theirs, and the grams stay
what they were. The letter moves; the food you ate does not.

The protein target does move, because it is per lens: Weight Loss asks 1.6-2.2 g per kg of
adjusted body weight, Fitness 1.4-2.0, GLP-1 1.2-2.0. The protein you actually ate is the same
number under all three.

Switching back gives the first letters back, exactly. That is the same property said differently:
a letter is a function of the food and the lens, and of nothing that happened in between.

## `POST /api/meals/parse`

A sentence in, a proposal out. **Nothing is saved.**

```json
{ "text": "chicken burrito bowl with rice and beans" }
```

Text is required, between 2 and 500 characters. A session is required too: the call costs money and
it reads the foods that person added for themselves.

**Response** — `200 OK`.

```json
{
  "items": [
    { "foodId": 6315, "description": "Rice, wild, 100%, cooked, no added fat",
      "rawText": "rice", "grams": 150, "quantityEstimated": true }
  ],
  "unrecognised": ["burrito bowl"]
}
```

`rawText` is the part of the sentence this item came from, so the screen can show what was matched
to what. `quantityEstimated` marks a quantity the model guessed rather than read.

**To log it**, post each item to `POST /api/meals/{id}/entries` — the endpoint that already exists.
Carry `quantityEstimated` with it, and the entry remembers that the number was a guess. Typing over
the quantity later clears it.

### `unrecognised` is never a substitution

Three different things land there, and none of them is a food nobody asked for
([0023](../decisions/0023-a-parsed-meal-is-a-proposal-nobody-saved.md)):

| | |
|---|---|
| the sentence named something the catalogue does not carry | the model says so itself |
| the model answered with an id no row carries | rejected here, against the catalogue |
| the model answered with somebody else's food | it was never in the prompt, and is rejected the same way |

A schema constrains the shape of an answer, never its content. An invented id passes the schema.

### `503` is the documented way for this to fail

```json
{ "title": "Reading a sentence is unavailable",
  "detail": "Nothing was logged and nothing was lost. Search for each food instead: GET /api/foods?search=…" }
```

No provider configured, a timeout, a refusal, a `429` or a `5xx` all answer this. Logging never
depends on it: `GET /api/foods?search=…` and `POST /api/meals/{id}/entries` are the path that always
works.

**Today every call answers `503`** — the provider is not wired yet.

## `PUT /api/profile/calorie-target` and `DELETE`

```json
{ "kcal": 2000 }   ->   { "kcal": 2000, "warning": null }
{ "kcal": 1100 }   ->   { "kcal": 1100, "warning": "Below 1,200 calories a day. Consider talking to a doctor." }
```

`DELETE` answers `204` and removes it.

**Its own resource, not a field on `PUT /api/profile`.** Onboarding must not ask for a calorie
target, and as a field it would be asked for on every profile save — worse, a client updating only
the weight would wipe it, because a missing field and an explicit `null` are the same thing once
bound. See [0019](../decisions/0019-the-calorie-target-is-its-own-resource.md).

**Under 1,200 kcal the response warns and stores the value anyway.** It never blocks, never asks for
a second confirmation, and never appears again — not on the Day Ring, not daily. Exactly 1,200 does
not warn. Below 500 is a `400`: that is a typo, not a choice.

**Nothing derives this number** from weight, height, age or activity, and nothing ever will — it is
a requirement to not build something.

**No consent needed, and withdrawing consent does not clear it.** A calorie goal is a preference you
type, like your active lens; it is neither a measurement of your body nor something you ate.

## `POST /api/account/export`

```json
{ "password": "..." }
```

Returns the whole account as one JSON document, sent as an attachment named
`sated-export-YYYY-MM-DD.json`.

```json
{
  "exportedAt": "2026-08-31T09:22:56.951492+00:00",
  "email": "florin@sated.test",
  "weightKg": 82,
  "activeLensId": "weight-loss",
  "consents": [
    {
      "purpose": "HealthData",
      "version": "2026-08-31",
      "givenAt": "2026-08-31T09:22:49.888733+00:00",
      "withdrawnAt": null,
      "text": "Sated needs two kinds of information about you..."
    }
  ]
}
```

**A `POST`, not a `GET`, because a `GET` has nowhere to carry a password.** The verb is the price of
the guard; see [0010](../decisions/0010-ask-for-the-password-before-export-and-deletion.md).

**Every consent carries the full text that was signed**, not a reference to it. A row saying
"document 1" means nothing outside this database, and the point of exporting a consent is to show
what was agreed to.

**Withdrawn consents are in the export**, with `withdrawnAt` filled in. The export is an archive,
not a snapshot of the present: both facts are the account holder's.

## `DELETE /api/account`

```json
{ "password": "..." }
```

`204`, and the response clears `sated.session`. Everything belonging to the account goes with it —
the profile row, every consent, and the Identity tables — in one transaction, because PostgreSQL
cascades from the user row.

**No soft delete, no grace period, no undo.** FR-29 asks for complete and irreversible.

A copied cookie does not survive the deletion either: the security stamp is validated on every
request (`SecurityStampValidatorOptions.ValidationInterval` is zero in `Program.cs`), so a session
whose user row is gone is refused on its next call, wherever it is held.

### Why both endpoints answer 403

| | |
|---|---|
| No session | `401` |
| Session, wrong password | **`403`** |
| Session, right password, account locked | **`403`** |

`403` rather than `401`: the caller is known and the request is well formed. What is missing is
permission for this particular action.

**A wrong password here counts towards the same lockout as a failed login** — five attempts, then
five minutes locked. Without that, a stolen session could be used to guess the password at leisure
against an endpoint with no counter. A locked account gets the identical `403`, and the body never
says which of the two happened.

## `GET /api/foods/categories`

An array of the category names the catalogue uses, sorted. Anonymous, like the rest of the read
side. It is read from the catalogue rows themselves, so it cannot drift from what
`CategoryRules` will match — there is no second copy of the list to forget to update.

## `POST /api/foods`

Adds a food that belongs to the caller alone. Requires a session.

```json
{
  "description": "Telemea de oaie",
  "category": "Cheese",
  "calories": 250, "protein": 17, "fat": 20, "carbohydrate": 1,
  "fiber": 0, "saturatedFat": 12, "sodium": 900,
  "calcium": 450
}
```

`201` with the stored food and a `Location` header. Amounts are per 100 g, the same as the
catalogue — see [database.md](database.md#units--the-one-place-they-are-written).

**The required fields are the ones a nutrition label prints**, and no more. The four optional ones —
`vitaminD`, `calcium`, `iron`, `potassium` — are the only micronutrients a label carries. Vitamin A,
C and E, magnesium and thiamine are never printed, so they are not asked for; the density score
renormalises over what is present and reports `isEstimated`.

### `carbohydrate` is required and is not stored

The engine never reads carbohydrate. It is asked for because `NutrientPlausibility` needs it to
check that the declared energy follows from the macronutrients, and without it the check breaks on
exactly the foods it should pass: bread's protein and fat alone imply a quarter of its calories, so
omitting carbohydrate would reject bread as a typo.

Asked, checked, discarded. Nothing keeps it.

### What gets rejected, and why each one

| Body | Answer |
|---|---|
| a category the catalogue does not use | `400` on `category` |
| energy no 100 g of food can carry | `400` on `calories` — almost always kilojoules |
| energy that does not follow from protein, fat and carbohydrate | `400` on `calories` |
| no session | `401` |

The category has to be one the catalogue already uses because the name selects the calibrated
category rule (FR-6). A name from somewhere else would match no rule and fall to the general
formula in silence, which is the failure [ProfileRules](https://github.com/developedbyflow/sated-app/blob/main/server/Sated.Scoring/ProfileRules.cs)
exists to catch.

The two energy checks are unit checks, not quality checks. European labels print kilojoules —
4.184 times the number this engine wants — and that value passes every other check and simply
grades wrong. What the check cannot catch: European labels print **salt in grams** where this
engine wants **sodium in milligrams**, and 1.2 g of salt is 480 mg of sodium. Both are plausible
numbers. That conversion is the client's job.

### A hand-typed food is graded, not refused

`GET /api/foods/{id}/grade` answers for it like any other food. Measured on the telemea above:
`B`, 59.8, with `isPartial: false` — all four components computed.

**`isPartial` is not the flag that says data was missing.** It means fewer than three components
went into the score. The flag that carries the missing micronutrients is `isEstimated`, per
component, and for a label-only food `density` and `proteinQuality` both report `true`: density
renormalised over the four nutrients it had, and leucine was estimated from the category rather
than measured.

## Provenance — on the list and on the detail

Every food says where its numbers came from. `GET /api/foods` carries `source` on each row:

```json
{ "id": 5347, "description": "Milk, NFS", "category": "Milk, whole", "source": "UsdaFndds" }
```

That is what tells your own foods apart from the catalogue's. FR-10 asks for the distinction to be
**in the data and not a mark of inferiority** — the field says where a food came from, and nothing
in the API ranks one source above another.

`GET /api/foods/{id}` adds `provenance`. A catalogue food:

```json
{ "source": "UsdaFndds", "estimated": ["leucine"], "absent": [] }
```

The same food typed in from a label:

```json
{
  "source": "UserEntered",
  "estimated": ["leucine"],
  "absent": ["vitaminA", "vitaminC", "vitaminD", "vitaminE", "thiamine", "iron", "magnesium", "potassium"]
}
```

**Every nutrient not named came from `source`.** `estimated` means the value is absent and the
engine fills it in — leucine, from the category. `absent` means the value is missing and nothing
fills it in; the density score renormalises over what is left and reports `isEstimated` on that
component.

`leucine` is never in both lists. It is absent on every row in the catalogue, and it is the one
nutrient the engine has a replacement for.

## `/api/recipes`

A recipe is a saved composition of foods with weights. Every route requires a session, and every
route only ever sees your own — the global query filter on `Recipe` is `OwnerId == you`, with no
catalogue half.

| | |
|---|---|
| `POST /api/recipes` | `201` + `Location` |
| `GET /api/recipes` | your recipes, with ingredient count and total weight |
| `GET /api/recipes/{id}` | the derived profile and the ingredients |
| `PUT /api/recipes/{id}` | replaces the name and **all** the ingredients |
| `DELETE /api/recipes/{id}` | `204`; the ingredients go with it |
| `GET /api/recipes/{id}/grade?lensId=` | the same shape `GET /api/foods/{id}/grade` returns |

```json
{
  "name": "Milk and the next thing along",
  "ingredients": [
    { "foodId": 5347, "grams": 200 },
    { "foodId": 5348, "grams": 100 }
  ]
}
```

**Ingredients are in grams.** `DisplayAmount`/`DisplayUnit` from the architecture wait on
`Food.Servings`, which does not exist yet — without it nothing can turn "2 eggs" into a weight.

**An ingredient can be a catalogue food or one of your own**, and the API cannot tell you which
foods exist: a food that is not yours and a food that never existed get the identical `400`.

### The profile is derived on every read

```json
{
  "totalGrams": 300,
  "nutrients": { "calories": 55.0, "protein": 3.31, "..." : null },
  "leucineIsEstimated": true
}
```

Nutrients are stated **per 100 g of total weight**, which is what makes the formula that grades one
food grade a recipe unchanged. Nothing is stored: a saved profile would be wrong the moment an
ingredient changed.

**A grade is never the average of its parts' letters.** 100 g of spinach (`A`) with 100 g of butter
(`E`) is not a `C` — the mixture carries about 740 kcal, 97% of them from the butter, so per
100 kcal it reads as butter.

**Absent stays absent all the way up.** One ingredient that does not know its vitamin C makes the
recipe not know it either: summing only the ingredients that do would spread the spinach's vitamin C
over the butter as well, a larger claim than any ingredient made.

`leucineIsEstimated` is true when any ingredient's leucine had to be estimated from its category.
One guessed ingredient makes the whole plate a guess.

### What FR-12 asks for that is not here

Two of the four acceptance criteria need `Meal`, which is Epic 4:

- **adding a recipe to a meal is one action** — there are no meals to add it to;
- **editing a recipe does not rewrite meals already logged** — solved on the meal side, where the
  architecture freezes grams at logging time. Nothing on the recipe can enforce it.

## Servings, on `GET /api/foods/{id}`

```json
{
  "typicalGrams": 50,
  "servings": [
    { "description": "1 egg", "grams": 50 },
    { "description": "1 cup", "grams": 135 },
    { "description": "1 slice", "grams": 5 }
  ]
}
```

**`servings` is the list a person picks from**, in USDA's own order — sorted by the file's
`sequenceNumber`, never by the order the array happens to be in. That distinction is not
bookkeeping: taking `foodPortions[0]` from the SR Legacy file gives `1 cup (4.86 large eggs) = 243 g`
for an egg. See [database.md](database.md#the-trap-and-it-is-not-where-the-prd-says-it-is).

**`typicalGrams` is a different question** — what USDA assumes was eaten when a survey respondent
did not say how much. It is not one of the servings and matches one exactly only 40.7% of the time.
It is here for FR-14, where a typed sentence may carry no quantity at all; nothing reads it yet.

Both are `null`/empty for a food someone typed in. `POST /api/foods` accepts no servings, so a
hand-entered food is logged in grams. That is a known gap, not a decision.

## Logging a meal

| | |
|---|---|
| `POST /api/meals` | `{ "date": "2026-08-31", "name": "Breakfast" }` → `201` + `Location` |
| `POST /api/meals/{id}/entries` | adds a food; returns the whole meal, grade included |
| `GET /api/meals/{id}` | the meal with its entries and aggregate grade |
| `GET /api/days/{date}` | every meal on that date, the day's protein against its target, and the Day Grade |

**The date comes from the client.** The server does not know your time zone, and "which day was it"
has to be settled when you log rather than worked out later — otherwise changing time zone
reshuffles history. It is also what lets you log yesterday's dinner.

### A quantity is grams **or** a serving, never both

```json
{ "foodId": 5943, "servingCount": 2, "servingDescription": "1 egg" }
{ "foodId": 5347, "grams": 150 }
```

Both together, or neither, is a `400`. The API will not choose which one you meant.

The serving must be one the food actually carries — `GET /api/foods/{id}` lists them. What comes
back records all three things:

```json
{ "quantityGrams": 100, "displayAmount": 2, "displayUnit": "1 egg" }
```

**`quantityGrams` is frozen at logging.** Correcting a serving definition later changes future
meals, never past ones. `displayAmount` and `displayUnit` exist because you cannot recover "2 eggs"
from 100 g, and an edit screen that shows 100 g has lost what you typed.

`quantityEstimated` is always `false` today; FR-14 is where a proposed quantity gets marked.

### The grade comes back with the entry

Adding an entry returns the meal already graded, **under your active lens** — no `lensId` parameter,
because "the grade appears immediately, without a further action" means the client should not have
to ask a second time.

**No active lens yet, and the meal still logs**, with `grade: null`. Onboarding sets the lens
(`PUT /api/profile`); until then the product works degraded, not not at all — the same stance the
Day Ring takes on a missing weight.

A meal with no entries has no grade either. There is nothing to aggregate.

### The day carries its protein against a target

```json
{
  "date": "2026-08-31",
  "protein": { "grams": 91.7, "targetMinGrams": 118.33, "targetMaxGrams": 162.71 },
  "grade": { "grade": "B", "score": 61.2, "isPartial": false, "satiety": { ... } },
  "meals": []
}
```

`grams` adds up every entry of every meal on the day, at the food's protein per 100 g times the
grams logged. It is always present, including on a day with nothing on it, where it is `0`.

The two ends of the target come from the **adjusted** weight — ideal weight at BMI 22, plus a
quarter of the excess over it — times the g/kg range the active lens carries in `calibration.json`.
Why adjusted rather than actual is
[0017](../decisions/0017-derive-the-protein-target-from-adjusted-body-weight.md), and it is the
whole decision: on actual weight the same ranges would tell a 130 kg user to eat 286 g a day.

| Lens | g/kg of adjusted weight | at 82 kg and 180 cm |
|---|---|---|
| Weight Loss | 1.6 – 2.2 | 118.3 – 162.7 g |
| Fitness | 1.4 – 2.0 | 103.5 – 147.9 g |
| GLP-1 | 1.2 – 2.0 | 88.8 – 147.9 g |

**Both ends are `null` when weight or height is missing**, and `grams` still comes back. The client
shows absolute grams and asks for the measurements; the API states the absence rather than guessing
past it.

**Nothing here says whether you are over or under.** Exceeding the top of the range is not an error
and gets no field — the API returns the three numbers and forms no opinion about them.

### The Day Grade is the whole day recomputed as one plate

`grade` has the same shape as a food's or a meal's: the letter, the score, and the three components.
It is produced by pooling **every entry of every meal**, summing the nutrients by grams,
renormalising to 100 g, and rerunning the formula — the call a meal already makes, one level up.

**It is not an average of the meals**, of their letters or of their scores. The observable form of
that is the property worth knowing:

> How you group your food into meals cannot change the day's grade.

A day holding one meal is graded byte for byte like that meal. Splitting it in two changes nothing.
Why, and what the alternative would have cost, is
[0018](../decisions/0018-the-day-is-one-plate.md).

**A day with no entries has `grade: null`** — never `E`. That covers a day with no meals and a day
whose meals are all empty. A day nobody logged is not a bad day.

**No active lens, no grade.** Same rule a meal follows: the letter needs a lens, and onboarding is
where it gets set.

### The third axis is absent unless you asked for it

```json
"calories": { "consumed": 1530.2, "targetKcal": 2000 }   // a target is set
"calories": null                                          // no target
```

`null`, not an object with a null target and not a zero. **If you have not set a calorie target,
Sated does not show you calories at all** — deliberately unlike protein, which reports absolute
grams with no target. Removing the target drops the axis and moves neither the protein nor the Day
Grade.

### `engineVersion`

Every meal stamps the `version` from `calibration.json` as it was when the meal was logged. Nothing
reads it: grades always render at the current version, so a recalibration moves old letters on
purpose. The stamp is what makes that detectable and explainable later, and the architecture is
explicit that adding it after months of history is a migration nobody performs.

### Logging a recipe

```json
{ "recipeId": 3, "grams": 300 }
```

One call, and the meal comes back with **one entry per ingredient**, each scaled by the share of the
recipe eaten. A 600 g recipe logged at 300 g halves every ingredient. Servings do not apply — grams
only, because a serving is defined on a food.

Either `foodId` or `recipeId`, never both and never neither.

Each resulting entry carries `fromRecipeId` and `fromRecipeName`, so a screen can fold them back
into one row:

```json
{ "quantityGrams": 200, "description": "Milk, NFS", "fromRecipeId": 2, "fromRecipeName": "Ciorba mamei" }
```

**Editing or deleting the recipe afterwards does not touch the meal.** Measured, end to end: a
recipe rewritten to a different name and a single different ingredient, then deleted — the meal
still reads 300 g across the same two entries, still labelled `Ciorba mamei`. That is FR-12's last
criterion, and it holds because the meal never referenced the recipe in the first place.

## Editing and deleting what was logged

| | |
|---|---|
| `PUT /api/meals/{id}` | rename it |
| `DELETE /api/meals/{id}` | `204` |
| `PUT /api/meals/{id}/entries/{entryId}` | a new quantity — grams or a serving |
| `DELETE /api/meals/{id}/entries/{entryId}` | remove one entry |
| `DELETE /api/meals/{id}/recipes/{recipeId}` | remove a logged recipe, every entry of it |

Everything that changes a meal returns the meal, regraded. "The grade recalculates immediately" is
not a feature here — it is what happens when nothing was cached in the first place.

A new quantity follows the same rule as adding one: grams **or** a serving, never both and never
neither. An entry that is not in this meal is a `404`, not a `400` — the id names nothing you can
reach.

### Changing how much you ate may not move the meal's grade

It moved nothing in this exchange, and that is correct:

```
100 g cheese                → B 71.29
same meal, cheese at 400 g  → B 71.29
```

A meal's grade is the quality of the mixture **per 100 g**, and 400 g of one food is the same
mixture as 100 g of it. Change one food in a meal of two and the grade does move, because the
mixture moved:

```
100 g egg + 100 g milk   → B 71.29
100 g egg + 900 g milk   → A 75.03
```

Quantity is not being ignored. It enters at the **day** level (FR-21), where the day's meals are
weighted by how much of each was eaten.

### A logged recipe is removed by its own route

Logging a recipe writes one entry per ingredient
([0016](../decisions/0016-unpack-a-recipe-when-it-is-logged.md)), so "undo that recipe" is not one
entry. `DELETE /api/meals/{id}/recipes/{recipeId}` removes exactly the entries that recipe unpacked
and leaves everything else in the meal alone.

Logging the same recipe twice into one meal makes both loggings share the id, so this removes both.

**Deleting a meal never touches the recipes it was logged from.** It could not — there is no link to
follow.
