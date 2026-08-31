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
{ "weightKg": 82, "activeLensId": "weight-loss", "healthDataConsentGiven": true }
```

Both values are `null` for an account that has not finished onboarding. That is not an error state
and does not need one: registering and onboarding are two moments, and the gap between them is
ordinary.

## `PUT /api/profile`

```json
{ "weightKg": 82, "activeLensId": "weight-loss" }
```

Returns the stored profile. Weight is in kilograms and must be between 20 and 500 — a sanity check
on typing, not a statement about who may use the product.

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
