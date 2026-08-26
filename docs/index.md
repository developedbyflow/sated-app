---
title: Sated Engineering Docs
---

# Sated

**A food's letter grade depends on why you are eating it.**

Every other app — Nutri-Score, Yuka, Noom, MyFitnessPal — prints one letter per food. Sated prints
one letter *per food, per goal*. Measured across the whole catalogue, **61.5% of foods get a
different letter** under "lose weight" than under "build muscle", and 22.6% move by two letters or
more. That is not a marketing claim; it is a number produced by
[`tools/CatalogueSnapshotQuery`](https://github.com/developedbyflow/sated-app/tree/main/tools/CatalogueSnapshotQuery)
over 5,431 foods.

## Where to start

| If you want to | Read |
|---|---|
| Understand what the engine actually sees | [What a food is](explanation/what-a-food-is.md) |
| See the system's shape and why it is shaped that way | [Explanation](explanation/architecture.md) |
| Change something without breaking the grades | [How-to](how-to/change-the-engine-safely.md) |
| Know why a decision went the way it did | [Decisions](decisions/index.md) |

## What exists today

The **scoring engine is complete and measured**. Everything else is not built yet.

| Part | State |
|---|---|
| `Sated.Scoring` — the grading engine | 26 files, 1,726 lines, 152 tests |
| `calibration.json` — the measured constants | 678 lines, every tunable number |
| The four gates — tests, G0, snapshot, audit | all green |
| `Sated.Api` — the HTTP layer | 14 lines |
| `Sated.Parsing`, `client/` | empty |
| Database, auth, catalogue storage | none |

The engine grades 5,431 foods across 3 lenses — **16,293 letters** — and every one of them has been
walked by the catalogue audit. See [The four gates](explanation/the-four-gates.md).

## What this documentation is not

It does not claim the formulas are *true*. Nobody can demonstrate that. It claims something
narrower and checkable:

> No grade the product can show violates any of the four audit criteria. The catalogue is finite,
> and all of it was walked.

The formulas themselves are public: the satiety term is the Fullness Factor from patent
US 7,620,531 B1 (2005), density is NRF9.2 (Drewnowski, 2009), and the leucine threshold is
published literature. **The engine is not the moat.** The measurement is.
