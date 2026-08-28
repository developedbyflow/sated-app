---
title: Why scores are relative
---

# Why scores are relative

## What a percentile is

If a student is **in the 90th percentile**, 90% of the people who sat the exam scored lower.

That is the whole idea. It does not say what they scored. It says how many they beat.

Food works the same way. Raw broccoli sits at percentile 97.4 of the density scale, which means it
beats roughly 5,290 of the catalogue's 5,431 foods, and about 141 beat it.

## Why the engine uses one

A raw density of 184 has no unit and no ceiling. *184 out of what?* Out of nothing — it cannot be
shown to anyone.

`PercentileScale` answers a different question: **not "how good is this food" but "how many foods
does it beat".** The result is a position, not a value.

## A linear range does not work here

The obvious alternative is to stretch the worst and best foods across 0–100. Measured on the shipped
calibration:

| Food | Raw | Linear range | Percentile |
|---|---|---|---|
| a food with no nutrients at all | 0 | **62.3** | 9.7 |
| the catalogue's median food | 18.6 | **63.6** | 50.0 |
| broccoli, raw | 184 | 75.2 | 97.4 |
| spinach, raw | 447 | 93.8 | 99.7 |

On a linear range, **a food containing nothing scores 62.3** — more than half the scale — and lands
1.3 points below the median food.

The cause is the shape of the data. Raw scores run from −884 to +536, but almost every food is
bunched near the top of that span: half the catalogue sits between 0.22 and 18.64. A linear range
divides a scale evenly when the foods on it are not evenly divided.

## The breakpoints are measured, not chosen

101 cut points — p0 through p100 — written by `tools/GradeDistributionQuery` from the whole
catalogue and stored in `calibration.json` with the date they were taken.

```
p0     -884.55   the worst food in the catalogue
p10       0.22   10% of the catalogue is below this
p50      18.64   the median food
p90      78.95   only 10% are above
p100    535.66   the best
```

From p0 to p10 is 885 points. From p40 to p50 it is 4. **The scale is dense exactly where the foods
are**, which is the property a linear range cannot have.

## Three consequences

**The letter must be frozen.** If the breakpoints were recomputed whenever the catalogue grew, every
food's position would shift, and chicken breast would change letter because somebody added a soup.
They are read from a file, never recalculated at runtime.

**A Sated score is not comparable to anyone else's.** 97 means "beats 97% of this catalogue", not
"97 points of goodness". Placing it beside a Nutri-Score is a category error.

**A percentile tells you where you stand in the queue. It does not tell you the queue is any good.**
If the catalogue held nothing but crisps, the best crisps would score A, because they beat 97% of
crisps. An A means "at the top of this catalogue", not "good in absolute terms" — which is why the
source of the catalogue is a decision in its own right rather than an implementation detail.
