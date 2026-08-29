---
title: HTTP API
---

# HTTP API

The complete surface of `Sated.Api`. Everything not listed here does not exist yet.

!!! note "State"
    One endpoint. There is **no authentication, no database and no catalogue** behind this API —
    it serves what the scoring engine can answer from `calibration.json` alone. Epic 2 adds
    accounts; Epic 3 adds the catalogue.

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

Route paths derive from controller class names via `[Route("api/[controller]")]`.

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

## Open: lenses have no stable identifier

A lens is identified by its display name. Story 2.2 will store a user's choice, and storing
`"Weight Loss"` means a later rename in `calibration.json` orphans every stored preference.

The fix is a stable slug separate from the label, which touches `calibration.json`, `Lens.cs` and
`Calibration.cs` — the engine. It is deliberately not done yet: the cost only buys something once
a choice is actually persisted. Decide it in Story 2.2, before the first write.
