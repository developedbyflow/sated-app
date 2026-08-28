---
title: Validation and its limits
---

# Validation and its limits

What has been checked against something outside this repository, what has not, and what is planned.
This page exists so nobody has to find these gaps for us.

## What is checked, and how

| Property | How it is checked | Result |
|---|---|---|
| The formulas still compute | 152 unit tests | green |
| Foods we have an opinion about grade correctly | gate G0 — 60 benchmark foods, 8 traps, 7 ordering pairs | 30/30 · 29/30 · 7/8 · 7/7, on all three lenses |
| No grade moved that we did not intend to move | committed snapshot of 5,431 foods × 3 lenses | unchanged |
| No grade is indefensible on its face | catalogue audit, 4 criteria over 16,293 grades | 877 → **0** |

All four run on every push. See [The four gates](the-four-gates.md).

## External validation

This is the honest table, and it is mostly empty.

| Score | Validated against | Result |
|---|---|---|
| Satiety | Holt 1995 — 21 foods | Spearman **0.853** |
| Density | *nothing yet* | — |
| Protein quality | *nothing yet* | — |
| Fat quality | no external source exists | — |

One study, 21 foods, covering one of four components. That is the current state.

## Calibration

**Only the Weight Loss lens is fitted on data.** The benchmark's required grades are Weight Loss
grades, so nothing in it measures the other two.

| Lens | Weights | Status |
|---|---|---|
| Weight Loss | 50 / 35 / 15 | fitted on a split benchmark — 60 foods to fit, traps and pairs held out |
| Fitness | 25 / 50 / 25 | **not fitted.** A stated structure, not a measurement |
| GLP-1 | 50 / 35 / 15 | carries Weight Loss weights by declaration. What defines this lens is its nutrient set, not its weights |

The fit does say something useful about how much the weights matter: **21 of 171 candidate
weightings reach the same maximum**, spanning satiety 40–80 and density 10–55, but protein quality
only 5–15. So the protein weight is the one this evidence actually constrains; the other two sit on
a wide plateau.

## What is estimated rather than measured

The engine marks every estimated component and never lets one read as measured. **A product that
hides the marker fails this, regardless of what the engine did.**

- **Protein quality is estimated for essentially the whole catalogue.** FNDDS carries no amino acid
  data, so leucine comes from measured per-category shares: boiled potato 6.02%, dairy 9.47%,
  median 7.52%.
- **Density is estimated** when a food is missing any nutrient the set counts, or when it sits below
  the calorie floor.

## Planned

| Gap | Plan |
|---|---|
| Density has no external validation | measure convergence with **Nutri-Score** over the whole catalogue. The algorithm is public; agreement is validation, and disagreement is the differentiator |
| Protein quality has no external validation | **DIAAS** |
| Fitness weights are not fitted | a separate benchmark whose required grades are Fitness grades |

## What may not be claimed

Not *"the formula is correct."* That cannot be demonstrated, by us or by anyone else in this
category.

What can be stated, and defended:

> No grade out of the 16,293 the product can show violates any of the four audit criteria. The
> catalogue is finite, and all of it was walked.

The strength of that claim comes from the catalogue being **finite and frozen**. It is not a sample
and not a model's confidence — it is every grade the product is capable of displaying, each one
checked.
