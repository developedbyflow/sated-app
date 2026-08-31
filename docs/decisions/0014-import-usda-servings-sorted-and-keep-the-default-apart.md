---
title: 0014 — Import USDA servings sorted, and keep the default apart
---

# 0014 — Import USDA servings sorted, and keep the default apart

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-13 lets a person give a quantity "in grams or in servings defined on the Food". Nothing on `Food`
defined a serving, so half of that requirement had no data behind it, and recipes could only take
grams for the same reason.

The data was already available and already measured. The USDA coverage report (2026-08-21) found
**100% of FNDDS foods carry at least one household measure with a gram weight**; the catalogue
loader simply never read the field.

Two facts recorded in the product brief and the PRD had to be checked before importing, because
both are about exactly this field:

> *USDA marks no portion as default, and the first in the list gives "one egg = 243 g" — a cup with
> 4.86 eggs.* — Decision G

Measured against the files:

- **`foodPortions` is not ordered in the JSON.** In SR Legacy, `Egg, whole, raw, fresh` has array
  element `[0]` = `243 g, cup (4.86 large eggs)`, carrying `sequenceNumber: 5`. The row with
  `sequenceNumber: 1` is `50 g, large`.
- **In FNDDS — the catalogue actually shipped ([0005](0005-fndds-is-the-catalogue.md)) — all
  seventeen egg entries give `1 egg = 50–55 g` first.** The 243 g figure is not in that file.
- **USDA does mark a default**, in a row described `Quantity not specified`: the amount assumed when
  a survey respondent did not say how much. **1,932 of our 1,933 foods carry one.**

## Decision

Import every portion with a gram weight into `FoodServings`, **ordered by `sequenceNumber`**, and
store the `Quantity not specified` weight separately as `Foods.TypicalGrams`.

## Alternatives considered

**Take `foodPortions[0]` as the food's serving.** This is the thing Decision G was warning about,
and the warning is correct — it produces 243 g for an egg. The fix is to sort, not to distrust the
data.

**Drop the `Quantity not specified` row.** It is useless as something to pick off a list, which is
why it is not in `FoodServings`. But it is a measured answer to a question FR-14 has to ask —
what someone ate when the sentence carried no amount — and it is not a copy of anything else:
measured, it equals one of the named servings only **40.7%** of the time. Discarding it would mean
re-reading a 66 MB file two stories later to get it back.

**Write servings by hand.** What the coverage report's Q1 was open about. The 100% coverage closed
it: this is an import, not a data-entry job.

## Consequences

**A second loader exists.** `tools/CatalogueLoad` refuses to run on a non-empty table by design
([0006](0006-load-the-catalogue-once-then-own-it.md)), so `tools/ServingsLoad` fills the gap for
catalogues loaded before today, with the same refusal if servings already exist. A fresh
`CatalogueLoad` now does both in one pass. Two tools where one would do, for as long as any
database predates this change.

**Two documents now hold a fact that measurement contradicts.** The product brief and the PRD both
state that USDA marks no portion as default. It does. Decision G's *conclusion* — the engine is
handed the quantity a person logged, never one it deduced — is untouched and still right; only the
supporting fact was an artefact of reading an unsorted array from a different dataset. The
correction is recorded here and in [database.md](../reference/database.md), not by editing the
originals.

**`TypicalGrams` is written and not read**, the same shape the architecture gives `EngineVersion`.
It is exposed on `GET /api/foods/{id}` anyway rather than kept write-only, because a field no test
can see is a field nobody knows is right.

**Two tables were renamed in passing.** `RecipeIngredient` and `FoodServing` came out singular,
because neither has a `DbSet` and EF names the table after the entity. Every other table in this
database is plural, and `database.md` had already documented them as plural — the documentation was
describing tables that did not exist. Fixed with `ToTable` and a rename migration.

**A food someone typed in has no servings.** `POST /api/foods` accepts none, so a hand-entered food
is logged in grams only. FR-13 does not distinguish, so this is a gap rather than a decision, and
it is the obvious next thing if logging a home-made food by the piece turns out to matter.
