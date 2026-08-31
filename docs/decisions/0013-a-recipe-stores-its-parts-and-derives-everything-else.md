---
title: 0013 — A recipe stores its parts and derives everything else
---

# 0013 — A recipe stores its parts and derives everything else

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-12 lets a person save a composition of foods as a reusable recipe, with a derived nutrient
profile and a grade computed by aggregation.

The aggregation half already exists. `PortionAggregate` (Story 1.10) turns portions into one
profile per 100 g so that the formula grading a single food grades a plate unchanged, and it carries
two rules that were measured rather than assumed: a grade is never the average of its parts'
letters, and a nutrient absent from one ingredient is absent from the whole. What Story 3.4 adds is
persistence and an API.

## Decision

`Recipes` and `RecipeIngredients`, holding a name, an owner, and food ids with weights in grams.
Nothing nutritional is stored. The profile and the grade are computed on every read.

## Alternatives considered

**A recipe is a `Food` with an ingredient list.** Tempting, because both end up as a nutrient
profile per 100 g and a meal could then point at one kind of thing. Rejected: `Food.Nutrients` is
`required` and stored, and a recipe's is neither. Making it nullable to fit would weaken the
guarantee for the 1,933 rows that do have measured numbers, to accommodate rows that never will.
[0011](0011-a-food-belongs-to-one-account-or-to-the-catalogue.md) drew the line the same way —
same kind of thing shares a table, different kinds do not, and a list of ingredients is the
difference.

**Store the derived profile alongside the ingredients.** It would have to be invalidated on every
edit to an ingredient, and on every correction to a catalogue food. `PortionAggregate` is a sum over
a short list; there is nothing to save.

**Store a serving count or yield.** Not asked for by FR-12, and the engine normalises to 100 g so it
needs none. It becomes real when a meal logs "one portion of my soup", which is Epic 4.

## Consequences

**Two of FR-12's four criteria are not built, and that is not an oversight.** "Adding a recipe to a
meal is one action" and "editing a recipe does not rewrite meals already logged" both need `Meal`.
The second is a meal-side property in any case: the architecture freezes grams at logging time, so
what protects a logged meal is that it recorded its own numbers, not anything a recipe does.

**`RecipeIngredients.FoodId` cascades from `Foods`, which was measured, not chosen.** `Restrict` was
the first choice, on the reasoning that a food in use should not disappear. Deleting an account then
returned `500`: foods and recipes both cascade from `AspNetUsers`, and `Restrict` on the path to
`RecipeIngredients` blocks the cascade. The consequence to carry forward is that a future
delete-a-food endpoint will silently shorten a recipe. There is no such endpoint, and the catalogue
is never deleted from ([0006](0006-load-the-catalogue-once-then-own-it.md)).

**`RecipeIngredient` needed its own query filter**, matching `Food`'s. Without it EF warns that a
filtered entity sits on the required end of a relationship with an unfiltered one — a real hazard:
an ingredient whose food is filtered out would come back with a null required navigation.

**Ingredients are grams only.** The architecture asks for `DisplayAmount`/`DisplayUnit` beside them,
so that "2 eggs" survives editing, and the reasoning is sound — you cannot recover "2 eggs" from
100 g. It cannot be built yet: converting "2 eggs" needs `Food.Servings`, which does not exist. This
is a known gap, not a rejected requirement.

**`PUT` replaces every ingredient rather than patching one.** A recipe is short, and a partial edit
would need ingredient ids to be stable and addressable for no benefit anyone has asked for.

**A food that is not yours and a food that never existed get the same `400`.** Deliberate, and the
same reasoning as the identical `401` for a wrong password and an unknown email
([0008](0008-keep-the-session-in-an-httponly-cookie.md)): the difference between the two answers is
a way to find out what exists.
