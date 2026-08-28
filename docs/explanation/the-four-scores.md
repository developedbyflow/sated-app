---
title: The four scores
---

# The four scores

Three of the four formulas are borrowed from published work. **One is ours** — and only because it
could not be borrowed.

None of them produces a grade. Each produces a raw number, and
[the combiner](architecture.md#level-3-components-inside-the-engine) turns four of those into one.

| Score | Measures | Source | Raw range |
|---|---|---|---|
| Satiety | how filling, per calorie | Fullness Factor, patent US 7,620,531 B1 (2005) | 0.5 – 5 |
| Density | nutrients per 100 kcal | NRF9.2, Drewnowski 2009 | −884 – +536 |
| Protein quality | share of the leucine threshold | published MPS literature | 0 – 100 |
| Fat quality | how good the fat is | none — see below | 0 – 100 |

The first two are raw quantities and mean nothing until ranked against the catalogue. The last two
arrive as percentages and skip the percentile scale entirely.

---

## Satiety

```
41.7 / calories^0.7  +  0.05·protein  +  0.000617·fibre³  −  0.00000725·fat³  +  0.617
```

The five coefficients were fitted together and must never be tuned individually. The patent is not
peer-reviewed, which is stated here rather than hidden: it is the weakest provenance of the four.

The four limits — calories ≥ 30, protein ≤ 30, fibre ≤ 12, fat ≤ 50 — are the boundary of the
measured data. Past them the cubed terms diverge.

**What it cannot read.** The first term pays a food for carrying few calories per 100 g, and a soft
drink collects the whole payment: cola scores **91.0** where chicken breast scores 86.4. That is why
sugar-water categories take a rule that sets satiety to zero.

It is equally blind at the other end. A food that is almost entirely fat hits the floor, so olive
oil and butter are indistinguishable to it. That is why the fourth score exists.

## Density

```
sum of the 9 ENCOURAGED nutrients (%DV, each capped at 100)
  −  sum of the 2 LIMITED nutrients (%DV, uncapped)
```

NRF9.2 counts protein, fibre, vitamins A, C and E, calcium, iron, magnesium and potassium as
encouraged; saturated fat and sodium as limited. Daily Values are the FDA 2016 labelling reference
for a 2,000 kcal diet.

**The 10 kcal floor.** The score is stated per 100 kcal, so the scale factor is `100 / calories`.
Below 10 kcal that factor passes 10 and trace amounts become a full score: diet cola carries 2 kcal,
scaling by 50, and residual sodium and iron alone scored it 85 out of 100 — an **A**.

**A missing nutrient is not counted as zero.** The score is rescaled over the nutrients the food
actually carries. Zero would be a claim that the food contains none, and measured across the gate's
68 foods that claim costs a letter on twenty of them; rescaling recovers thirteen. The component is
then reported as **estimated**, never as measured.

**Zero calories returns null, not 0.** "Nutrients per calorie" has no answer there. The component
leaves the calculation rather than entering it at its worst possible value.

`NRF11.2`, the GLP-1 lens's set, is written in code as **NRF9.2 plus two** — vitamin D and
thiamine — rather than as eleven nutrients copied out. That is precisely the claim being made: the
lens does not disagree with NRF9.2 about anything, it counts two more things. Two hand-written lists
would drift apart in silence.

## Protein quality

```
min(100,  leucine in a 300 g reference meal / 3 g × 100)
```

2.5–3 g of leucine in a single meal triggers muscle protein synthesis, and the response **saturates**
— more leucine in the same meal does not trigger more — so the score stops at the top of the range
rather than rewarding excess.

The 300 g is a fixed reference meal from `calibration.json`, never the portion someone logged. The
question is *"if the whole meal were this food, would it reach the threshold?"* That keeps the score
per 100 g, so a food, a recipe, a meal and a day are all read on one scale.

**It is almost always estimated.** FNDDS carries no amino acid data at all, so leucine is estimated
from measured per-category shares: boiled potato 6.02%, dairy 9.47%, median 7.52%.

Without this component, Fitness and Weight Loss would return the same letter for **87.6%** of the
catalogue. It is the only axis separating them.

## Fat quality

```
clamp( (fat − saturated) / fat × 100  −  sodium as %DV,  0, 100 )
```

**This one has no source.** Neither a published formula nor a Daily Value exists for "good fat", and
inventing both would have shipped our guess dressed as a borrowed standard.

The way out is that **a share needs no reference value**. That is the whole trick.

**Sodium is per 100 g, not per calorie.** Per calorie a fat reads as low-sodium precisely because it
is fatty: mayonnaise carries 635 mg against olive oil's 2, and the per-calorie penalty separated them
by five points, 81 against 86. Per 100 g it is 56 against 84, which is the gap the two foods actually
have.

FNDDS reports no trans fat, so "unsaturated" here means "not saturated" and **margarine is
flattered**. Accepted knowingly: the data postdates the FDA ban on partially hydrogenated oils, so a
margarine reading 80% unsaturated in FNDDS 2021-2023 really is.

It carries no weight of its own — it takes satiety's, on a ramp from 60% to 100% of the food's energy
being fat. See [decision 0001](../decisions/0001-fat-quality-is-a-component-not-a-category-rule.md).

---

## Two kinds of limit

Every formula here has caps and floors, and they are not all the same thing.

A floor changes the food the formula sees: `Math.Max(10, 2)` computes as though a 2 kcal drink
carried 10. That is not a repair of a broken formula — it is a statement about **where the formula's
question stops making sense.** Density divides by calories, and dividing by a number close to zero
turns trace amounts into full scores. Each limit still has to be justified separately.

**Limits that come from the source.** The formula is not allowed past a point, and the point was
never ours to choose.

| Limit | Where it comes from |
|---|---|
| calories ≥ 30 · satiety | the patent was fitted on real data, and the cubed terms diverge outside it |
| protein ≤ 30 · fibre ≤ 12 · fat ≤ 50 | the same edge of the measured data |
| max 100 · protein quality | biology — past roughly 3 g of leucine, synthesis does not increase |
| calories ≤ 950 · both | physics — nothing carries more than 9 kcal per gram, so 100 g of anything tops out near 900. This one catches a unit error, not a food: European data reports kJ, and 4.184 times a real number passes every other check in this engine |

**Limits added after a real food came out wrong.** There are exactly two.

| Limit | The food that demanded it |
|---|---|
| calories ≥ 10 · density | diet cola — 2 kcal, trace nutrients, graded **A** |
| 0–100 · fat quality | fat-free dressing, whose sodium penalty drove it to **−17.48** |

### Why the density floor is legitimate

The fair objection to a floor is: *you just claimed a 2 kcal drink carries 10 — how do you know you
did not break the vegetables?*

That was measured, and the answer is in the code: **70 foods out of 5,403** sit below the floor, and
**the 41 vegetables under 40 kcal are untouched.** The floor catches flavoured water, not cucumbers.

The rule generalises to any cap anyone adds later: **a cap is acceptable only when you can show what
it does not step on.**
