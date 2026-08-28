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
| Satiety | Holt 1995 — 21 foods | Spearman **0.853** (0.903 excluding potato) |
| Density | Nutri-Score 2023 — 4,713 foods | Spearman **0.738** |
| Protein quality | *nothing yet* | — |
| Fat quality | no external source exists | — |

### Density against Nutri-Score

Measured by `tools/NutriScoreCompareQuery`, which reimplements the published 2023 algorithm and
compares its ranking of the catalogue to ours. Two components of four now have an external number.

The comparison is restricted to Nutri-Score's **general branch**. Beverages and the
fats/oils/nuts/seeds family are scored under different rules that the tool deliberately does not
reproduce, because there is no worked example to check them against. 716 foods in 39 categories are
excluded, and the tool prints the list so the exclusion can be argued with.

The general branch **is** checked: the tool refuses to print anything unless it reproduces cheddar
(FNDDS 2705709) at exactly **16, grade D** — the value verified against Santé publique France's own
calculator. Not within a tolerance; exactly.

**7.6% of foods disagree by more than 40 rank points.** Those splits are the interesting output, and
they fall into three kinds:

| Pattern | Example | Reading |
|---|---|---|
| We rank organ meats and roe far higher | liverwurst, caviar, chicken livers — all **E** under Nutri-Score | a known Nutri-Score weakness; genuinely nutrient-dense foods |
| We rank fortified powders near the top | meal-replacement mixes at 90–98 / 100 | **a defect of ours** — NRF9.2 counts added micronutrients exactly like inherent ones |
| Nutri-Score rewards the absence of bad things; we reward the presence of good ones | sugar substitutes: **A** for them, ~5 / 100 for us | a real difference in what the two scores are for, not an error in either |

The middle row is the finding that matters. It is recorded as an open question, not explained away.

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
| ~~Density has no external validation~~ | **done** — Spearman 0.738 against Nutri-Score, above |
| Fortified foods rank too high | decide whether NRF9.2 should count added micronutrients at the same weight as inherent ones. Nobody has measured what a discount would cost |
| Protein quality has no external validation | **DIAAS** |
| Fitness weights are not fitted | a separate benchmark whose required grades are Fitness grades |
| Beverages and added fats are not compared | needs a worked example for those two branches before the implementation can be trusted |

## What may not be claimed

Not *"the formula is correct."* That cannot be demonstrated, by us or by anyone else in this
category.

What can be stated, and defended:

> No grade out of the 16,293 the product can show violates any of the four audit criteria. The
> catalogue is finite, and all of it was walked.

The strength of that claim comes from the catalogue being **finite and frozen**. It is not a sample
and not a model's confidence — it is every grade the product is capable of displaying, each one
checked.
