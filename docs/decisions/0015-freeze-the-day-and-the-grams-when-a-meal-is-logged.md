---
title: 0015 — Freeze the day and the grams when a meal is logged
---

# 0015 — Freeze the day and the grams when a meal is logged

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-13 logs a food into a meal with a quantity "in grams or in servings defined on the Food". Servings
arrived in [0014](0014-import-usda-servings-sorted-and-keep-the-default-apart.md), so both halves
were finally buildable.

The architecture had already settled the hard part and gave the reason in one line: **derived values
are recomputed, recorded inputs are not.** Two things follow from it, and both are easy to get
wrong by doing the obvious thing.

## Decision

`Days`, `Meals` and `MealEntries`. The local date lives on `Day` and comes from the client. An entry
records `QuantityGrams` alongside `DisplayAmount` and `DisplayUnit`. A meal stamps the engine
version in force when it was logged.

## Alternatives considered

**Derive the date from `LoggedAt`.** One less column, and wrong: `LoggedAt` is a UTC instant, and the
day a person means is local. Deriving it needs a time zone, and a time zone that changes reshuffles
history — a meal logged at 23:30 moves to the previous day after a flight. The client knows the time
zone; the server should be told the answer, not asked to guess it.

**Store grams only.** The engine wants nothing else, so this looks like the simple version. It
destroys "2 eggs" the moment it is saved: open the entry to edit and you see 100 g. The loss is
irreversible and happens on every single log.

**Store the serving definition instead of grams** — unit plus factor. Then correcting `1 egg` from
50 g to 55 g silently changes the weight of every meal already logged, and with it every historical
grade.

**Take the lens as a query parameter**, the way `GET /api/foods/{id}/grade` does. Rejected because
the criterion is that the grade appears *without a further action*. The account already carries
`ActiveLensId`; asking the client to send it again is the further action. Foods keep the parameter —
they are read by people who are not signed in.

## Consequences

**A meal logs without an active lens, and comes back with `grade: null`.** Registration and
onboarding are separate moments, and logging should not be blocked by the gap. The product works
degraded rather than not at all — the same stance the experience document takes on a missing weight.

**`QuantityEstimated` is written and never true.** It belongs to FR-14, where the parser proposes an
amount the sentence did not carry. It is in the schema now for the reason the architecture gives
about `EngineVersion`: a column added after months of history is a migration nobody performs.

**`EngineVersion` needed a version to stamp**, and `calibration.json` had none — only `catalogue`
and `measuredOn`. It now carries `version`, raised by hand when either the numbers or the engine's
code change. Nothing reads it.

**`Meal` carries a query filter that no test can distinguish.** Every path to a meal today also
includes its `Day`, and EF filters the meal out when the day it requires is filtered away —
measured: without the filter a stranger still gets `404`. It stays for the query nobody has written
yet, which is the argument for global filters in
[0011](0011-a-food-belongs-to-one-account-or-to-the-catalogue.md). Recorded rather than quietly
kept.

**`(OwnerId, Date)` is unique**, so a day exists once and meals attach to it. `POST /api/meals`
creates the day when it is the first meal on that date, which means logging never needs two calls.

**Two of FR-12's criteria are still open, and are now buildable.** Logging a recipe into a meal, and
editing a recipe without rewriting logged meals, both waited on `Meal`. The shape that satisfies
them is now visible: an entry that logs a recipe must **expand into that recipe's foods at logging
time**, so the meal holds foods and grams like any other and a later edit cannot reach it. Not built
here — it is Story 3.4's remainder, not 4.1's.
