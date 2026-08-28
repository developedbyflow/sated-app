---
title: Why scores are relative
---

# Why scores are relative

A raw density of 184 has no unit and no ceiling. It cannot be shown to anyone. `PercentileScale`
turns it into a position: **not "how good is this food" but "how many foods in the catalogue does it
beat".**

That single choice decides several things about the product, so it is worth stating plainly.

## A linear range does not work here

The obvious approach is to stretch the worst and best foods across 0–100. Measured on the shipped
calibration, here is what that produces:

| Food | Raw | Linear range | Percentile |
|---|---|---|---|
| a food with no nutrients at all | 0 | **62.3** | 9.7 |
| the catalogue's median food | 18.6 | **63.6** | 50.0 |
| broccoli, raw | 184 | 75.2 | 97.4 |
| spinach, raw | 447 | 93.8 | 99.7 |

On a linear range, **a food containing nothing and the median food are 1.3 points apart.** On the
percentile scale they are 40.3 apart.

The cause is the shape of the distribution. Raw density spans −884 to +536, a width of 1,420 — but
half the catalogue sits between 0.22 and 18.64. A linear range crushes half the foods into a single
point of the scale and spends the rest of it on a handful of extremes.

## The breakpoints are measured, not chosen

101 values, written by `tools/GradeDistributionQuery` from the whole catalogue and stored in
`calibration.json` with the date they were taken.

```
p0     -884.55     the worst food in the catalogue
p10       0.22
p50      18.64     the median food
p90      78.95
p100    535.66     the best
```

The gap from p0 to p10 is 885 points. From p40 to p50 it is 4. The scale is dense exactly where the
foods are, which is the property a linear range cannot have.

## Three consequences

**A Sated score is not comparable to anyone else's.** 97 means "beats 97% of this catalogue", not
"97 points of goodness". Comparing it to a Nutri-Score number, or to another app's out-of-100, is a
category error.

**The letter must be frozen.** If the breakpoints were recomputed whenever the catalogue grew, every
food's position would shift, and chicken breast would change letter because somebody added a soup.
The 101 values are read from a file, never recalculated at runtime.

**The catalogue is part of the definition.** A score means "against FNDDS 2021-2023". Changing the
catalogue changes what every score means, which is why the source of the catalogue is a decision in
its own right rather than an implementation detail.
