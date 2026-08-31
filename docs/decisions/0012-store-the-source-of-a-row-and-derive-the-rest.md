---
title: 0012 — Store the source of a row and derive the rest
---

# 0012 — Store the source of a row and derive the rest

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-10 says every food carries the source of **each nutritional field**, and lists the sources it
expects one day: USDA (SR Legacy, Foundation, FNDDS, Branded), Open Food Facts, user-entered,
manual-curated.

Two of those exist today. Counted in the development database, across all 1,933 catalogue rows:

| | |
|---|---|
| rows missing any of the fifteen nutrients | **0** |
| rows missing leucine | **1,933 — all of them** |

There is no variation inside a row. Every field of a catalogue food came from FNDDS 2021-2023, and
leucine is absent everywhere and estimated by the engine from the category. Sixteen source columns
would hold the same constant about 29,000 times.

## Decision

One column, `Foods.Source`, holding where the **row** came from: `UsdaFndds` or `UserEntered`.
Per-field provenance is computed at the boundary from data already present — a value that is there
came from `Source`, a null leucine is `estimated`, any other null is `absent`.

## Alternatives considered

**A source per nutrient column**, which is what FR-10 asks for literally. Rejected on the count
above: sixteen columns with no variation to record, and sixteen more places to keep right on every
import. If a row ever does mix sources — a hand-corrected FNDDS row is the realistic case — this
becomes the right shape and the column here becomes its row-level default. It is not that yet.

**Derive the source too**, from `FdcId` and `OwnerId`: a row with an FdcId came from USDA, a row
with an owner was typed in. It works today by coincidence. [0006](0006-load-the-catalogue-once-then-own-it.md)
says the catalogue is ours after the first load and corrections live in it, so a corrected row keeps
its `FdcId` and stops being FNDDS — and the inference becomes wrong without anything failing. The
international addendum adds Open Food Facts and Branded, neither of which changes `FdcId`'s shape.

**A boolean `IsUserEntered`.** Enough for the two sources that exist and wrong the moment a third
arrives, which the addendum already names.

## Consequences

**`Food.Source` is `required`, so the compiler named every place that builds one** — the importer,
the create endpoint, and three test fixtures. That was the point of choosing `required` over a
default.

**The column has no database default.** The migration filled the 1,933 existing rows with
`UsdaFndds` and then dropped the default, matching the rule the six required nutrient columns
already follow: an incomplete INSERT should fail loudly rather than record a food that reads as
measured.

**The backfill has no test behind it.** Migrations run against an empty database in the suite, so
there were no rows for it to fill. It was verified by counting the development database after
`database update` — 1,933 `UsdaFndds`, nothing else. Any future data migration is in the same
position and should be checked the same way, and said so out loud.

**`FoodListItemDto` grew a field, and a test that guarded its shape failed.** That test was doing
its job; the shape changed on purpose and the assertion was updated with it. The list still carries
no nutrients.

**A client can now rank sources, and must not.** `source` exists so a person can see where a number
came from, and FR-10 is explicit that a user-entered food is not marked as inferior. Nothing in the
API expresses a hierarchy; if one appears, it will have been invented in the client.

**Open, not blocking:** `estimated` is hard-coded to leucine, because leucine is the only value the
engine replaces. If a second estimate ever appears, this list and the engine will disagree silently
— the API computes it from a null check, not from asking the engine what it filled in.
