---
title: 0016 — Unpack a recipe when it is logged
---

# 0016 — Unpack a recipe when it is logged

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-12 ends with two criteria that could not be built when recipes were:

- adding a recipe to a meal is one action;
- **editing a recipe does not rewrite meals already logged.**

Both needed `Meal`, which arrived with FR-13
([0015](0015-freeze-the-day-and-the-grams-when-a-meal-is-logged.md)). The second is the interesting
one: it is a statement about what must *not* happen, and the obvious implementation — a meal entry
pointing at a recipe — makes it false by default and then needs a rule to patch it.

The architecture already answers the shape of this: **derived values are recomputed, recorded inputs
are not.** From a meal's point of view, a recipe's composition is an input.

## Decision

Logging a recipe writes one `MealEntry` per ingredient, scaled by the share of the recipe eaten. The
entries carry `FromRecipeId` and `FromRecipeName` for display and grouping. `FromRecipeId` is **not
a foreign key**.

## Alternatives considered

**A meal entry that points at a recipe.** One row instead of several, and it reads well until
somebody edits the recipe: every meal ever logged with it changes retroactively, including its
grade. The criterion forbids exactly this. Patching it afterwards would mean versioning recipes, or
copying a nutrient profile onto the entry — storing derived numbers, which
[0013](0013-a-recipe-stores-its-parts-and-derives-everything-else.md) rejected for good reasons.

**`FromRecipeId` as a foreign key with `ON DELETE SET NULL`.** Keeps referential integrity, and
destroys the one thing worth keeping: when the recipe is deleted the id goes null, the entries stop
grouping, and "Ciorba mamei — 300 g" becomes three unrelated rows of milk. The column is a record of
what happened, not a link to something that must still exist.

**`ON DELETE CASCADE`.** Deleting a recipe would delete meals logged from it. Not worth arguing
against beyond stating it.

## Consequences

**One logged recipe is several rows.** Deleting "the recipe I logged" means deleting a group, which
Story 4.3 has to handle rather than assuming one entry per action.

**A dangling id exists by design.** `FromRecipeId` can name a recipe that no longer exists. It is
never dereferenced — `FromRecipeName` carries what the screen needs — but anyone writing a join
against it later will be surprised, which is why it is documented in
[database.md](../reference/database.md) and not only here.

**The recipe's name is frozen too.** Rename the recipe and old meals keep the old name. That is the
same principle as the grams, and it is correct: the meal records what you logged, not what the
recipe is called today.

**Scaling is by weight, not by servings.** A recipe has no serving count
([0013](0013-a-recipe-stores-its-parts-and-derives-everything-else.md) left it out), so "how much of
it did you eat" is answered in grams. If recipes ever gain a yield, "one portion" becomes possible
and this is where it lands.

**Verified end to end, not only in tests:** a recipe was logged at 300 g, then rewritten to a
different name with a single different ingredient, then deleted. The meal held 300 g across the same
two entries throughout, still labelled with the original recipe name.
