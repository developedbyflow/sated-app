---
title: 0018 — The day is one plate
---

# 0018 — The day is one plate

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-21 describes the Day Grade twice, and the two halves do not agree:

> a quantity-weighted average of the **Grades of the day's Meals**, **by the same method as FR-8**

The first clause says to average the meals. The second names a method, and FR-8's method is
precisely the one that is *not* an average of the parts:

> A Meal's Grade is not the average of its components' Grades. Aggregation sums the nutrients by
> quantity, then renormalises to 100 g of total mass.

Read literally, the first clause reintroduces at the day level exactly what FR-8 forbids at the
meal level.

The two readings are not close. Measured by `tools/DayGradeMethodQuery` over 20,000 simulated days
built from FNDDS under the Weight Loss lens, they hand the day **a different letter 27.45% of the
time** — 26.4% one letter apart, 1.0% two, and a handful three or four. The score gap runs a median
of 2.7 points, 12.1 at p95, and 60.5 at its worst. Whichever is chosen, it is not a rounding
difference.

One day off the running API, with two FNDDS foods:

| | grams | score | letter |
|---|---|---|---|
| Cod, cooked | 100 | 80.02 | A |
| Butter, stick | 200 | 12.29 | E |
| **weighted average of the two** | | **34.87** | **D** |
| **the day as one plate** | 300 | **19.93** | **E** |

The plate is the honest answer. That day carries about 1,530 kcal, roughly 95% of them butter fat.
Calling it a D is the averaging artefact FR-8 was written to prevent, arriving one level higher up.

## Decision

**The day is one plate.** Every entry of every meal is pooled into a single `FoodInput` through
`PortionAggregate`, and the formula is rerun on it — the same call `Meal` already makes, one level
up.

The observable consequence is the plain statement of the decision, and it is what the test asserts:
**how you group your food into meals cannot change the day's grade.** A day holding one meal is
graded byte for byte like that meal.

A day with no entries at all has **no grade** — `null`, never `E`. That includes a day whose meals
exist but are all empty. A day nobody logged is not a bad day.

## Alternatives considered

**Average the meals' scores, weighted by grams.** The literal first clause. Two things break.

It **cannot apply the density floor**, and it cannot apply the nutritionally-empty rule either. Both
read the *components* of a `CombinedScore`, and averaging scores throws the components away — there
is nothing left for the rule to look at. Measured: the floor drops 0.31% of days to `E`, and under
averaging every one of them would keep a letter it did not earn. A rule that silently stops firing
is worse than one that was never written.

It also disagrees with `PortionAggregate`'s own stated reason for existing, which is not about days
at all: *100 g of spinach (A) with 100 g of butter (E) is not a C.* Nothing about that argument
becomes untrue when the two are eaten four hours apart.

**Average the meals' letters.** Ruled out by FR-8 before this story started.

## Consequences

**One hand-typed food marks the whole day estimated.** Measured: stripping vitamin C and magnesium
from a single entry makes the day's density `isEstimated` in **100%** of simulated days, where the
per-meal method would have left 66% of that day's meals measured. That is the price, and it is
paid on purpose — `PortionAggregate` already says *absent stays absent all the way up*, and SM-C4
counts a hidden guess as a failure of the product rather than a detail of the engine. FR-17 exists
to show the mark, not to avoid earning it.

Catalogue foods cannot cause this: measured against the live database, **0 of 1,933** are missing a
micronutrient. The only route is `POST /api/foods`, where they are optional.

**`Days.Summarise` reads the user once** and answers both the protein and the grade. `Meals.GradeOf`
still reads the user per meal, so a day of three meals costs four reads of the same row. Not fixed
here; it is a query, not a decision.

**The day carries a grade that no meal of it carries.** The aggregate of a whole day belongs to no
WWEIA category, so it takes `Mixed portions` and no category rule fires on it — the same thing that
already happens to any mixed meal. Nothing new, but it is the reason a day of nothing but oils is
graded by the general formula and then floored, rather than by the oils rule.

**FR-21's first clause is not implemented as written**, and this is the decision to override it.
Recorded here rather than quietly satisfied by the clause that agrees with the code.
