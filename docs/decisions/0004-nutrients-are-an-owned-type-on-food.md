---
title: 0004 — Store nutrients as an owned type on Food
---

# 0004 — Store nutrients as an owned type on `Food`, in the food's own row

**Status:** accepted
**Date:** 2026-08-30

## Context

`Foods` existed with four columns — `Id`, `FdcId`, `Description`, `Category` — and no nutrients.
The engine cannot grade a row in that state.

The engine's input record, `FoodInput`, fixes the shape of what has to be stored: **sixteen
nutrients**, of which six are non-nullable (`Calories`, `Protein`, `Fat`, `Fiber`, `SaturatedFat`,
`Sodium`) and ten are optional (nine micronutrients plus leucine). That set is enforced at compile
time — the record has sixteen parameters, and two of them carry no default on purpose after a build
in which they were dropped silently.

## Decision

Nutrients live in a class `NutrientAmounts`, mapped as an **EF Core owned type** on `Food` and
stored in the `Foods` table itself. It has no key and no table of its own; its properties become
columns named `Nutrients_*` in the food's own row.

## Alternatives considered

**Sixteen flat properties on `Food`.** Produces byte-for-byte the same table. It lost on the C#
side only: `Food` would carry twenty properties at one level, and every conversion to `FoodInput`
would be sixteen hand-written assignments with nothing checking that `Fat` was not typed where
`Fiber` belongs.

**A separate table, one row per nutrient** (`FoodId`, `Nutrient`, `Amount`). The real contender,
and the one that pays off when the set of stored attributes changes without the code changing —
USDA publishes over 150 nutrients, and this shape absorbs a new one with an INSERT.

It lost on a fact, not a preference: **the engine names its sixteen nutrients at compile time.**
Adding a seventeenth means changing `FoodInput`, the scores that read it, and the tests — a
migration is the cheapest part of that day. The flexibility never gets used, while the cost is paid
on every read, which becomes a join plus a pivot from rows back into an object.

## Consequences

- **One table, no join.** Reading a food to grade it is a single-row read.
- **Column names carry the prefix** — `Nutrients_Protein`, not `Protein`. EF's default was kept
  rather than configured away: the prefix says which object the column belongs to, which matters
  once `Food` gains fields that are not nutrients.
- **A nutrient added later is a migration**, and one that touches the engine at the same time.
- **`NutrientAmounts` cannot be queried on its own or shared between foods.** That is what owned
  means. If nutrients ever need to exist independently — a measured profile reused across
  branded products — this becomes an entity, with a key, and the migration is not free.
- **The six required nutrients are `required` in C# and `NOT NULL` in Postgres, with no database
  default.** EF's generated migration proposed `DEFAULT 0.0`; it was removed by hand. Zero is a
  legitimate amount of fibre, so a default would let an incomplete INSERT — from catalogue
  loading, not from EF — record a food that reads as measured. FR-7 says zero never means absent.
  The cost is that this migration only applies cleanly to an empty or fully-populated table.
- **Units are not in the column names.** Every amount is per 100 g; that is recorded once, in
  [the database reference](../reference/database.md), and nowhere else.
