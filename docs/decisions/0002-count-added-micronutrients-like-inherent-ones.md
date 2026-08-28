---
title: 0002 — Count added micronutrients like inherent ones
---

# 0002 — Count added micronutrients exactly like inherent ones

**Status:** accepted
**Date:** 2026-08-28

## Context

The Nutri-Score comparison (`tools/NutriScoreCompareQuery`) found the sharpest disagreement between
the two scales on fortified products. Under Weight Loss the engine grades meal-replacement shakes
and protein powders **A**, some of them above raw broccoli:

| Food | Our grade | Nutri-Score |
|---|---|---|
| Nutritional powder mix, sugar free (Carnation) | A 86.0 | E |
| Nutritional drink, ready-to-drink (Muscle Milk) | A 85.1 | E |
| Nutritional drink, ready-to-drink (Slim Fast) | A 80.6 | E |
| Broccoli, raw | A 82.5 | A |

Three explanations were proposed and all three were checked and rejected:

1. **"The density score is broken."** No. The raw NRF9.2 scores are Slim Fast 144 against broccoli
   184 and spinach 447. Density ranks these foods below vegetables; it is the percentile scale that
   places both in the top few percent, because most of the catalogue scores far lower.
2. **"It is dry powders measured per 100 g."** No. The ready-to-drink versions of the same products
   grade A as well.
3. **"Detect fortification and discount it."** Not possible with this data. In FNDDS these products
   resolve to a single ingredient — the branded product itself — so there is no ingredient list in
   which added vitamins could be recognised.

What remains is not a defect. **NRF9.2 counts a milligram of added vitamin exactly like a milligram
of inherent vitamin, and the engine implements NRF9.2 faithfully.** Nutri-Score deliberately does
the opposite, to keep manufacturers from fortifying their way to a better letter. Two published
standards disagree, and we adopted one of them.

## Decision

Keep NRF9.2 as published. Added micronutrients count at full weight.

Document the consequence rather than hide it, and treat which products a user is *shown* as a
catalogue question (D1/D4), not a formula question.

## Alternatives considered

**Discount fortified nutrients.** Blocked, not rejected: FNDDS carries no signal to key it on.
Revisiting requires a catalogue that flags fortification, which is an input to D1.

**Cap the encouraged sum.** Rejected. The cap would bind on spinach at 447 and liver long before it
bound on a shake at 144, which inverts the intent.

**A category rule for meal replacements.** Rejected on precedent. Every switch in this engine has
produced the same defect — two foods a nutrient cannot tell apart, graded far apart because one fell
on the far side of a boundary somebody drew. See [0001](0001-fat-quality-is-a-component-not-a-category-rule.md).

## Consequences

- **A fortified shake can outrank a vegetable, and that will be screenshotted.** The defence is that
  it is NRF9.2's answer, published, and that we say so first. It is a presentation risk we accept
  knowingly, not a calculation we failed to check.
- The same property is why the engine ranks liverwurst, caviar and chicken livers far above
  Nutri-Score's **E**. The behaviour that flatters a protein shake is the behaviour that correctly
  recognises organ meat. **They cannot be separated without a fortification signal.**
- Anyone who disagrees can point at this record. That is the point of writing it.
