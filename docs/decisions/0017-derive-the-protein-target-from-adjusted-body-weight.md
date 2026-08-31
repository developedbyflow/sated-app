---
title: 0017 — Derive the protein target from adjusted body weight
---

# 0017 — Derive the protein target from adjusted body weight

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-20 asks for a daily protein target that is "an interval (g/kg), derived from weight, with the
interval dictated by the Lens". It gives no numbers, and `calibration.json` carried none.

Reading the literature for them turned up two ranges that look like a disagreement and are not.
The clinical branch says 1.2–1.6 g/kg; the sports branch says 1.8–2.2, and
[Helms 2014](https://pubmed.ncbi.nlm.nih.gov/24864135/) says 2.3–3.1 for a lifter in a deficit.
They differ in what they multiply, not in what they claim. The high numbers are stated per kg of
**lean mass**, or per kg of bodyweight in resistance-trained subjects — people for whom the two are
nearly the same. The low ones are per kg of **actual bodyweight** in a general population.

The same person, both ways: 100 kg at 30% fat carries 70 kg of lean mass. 2.0 g/kg of lean mass is
140 g, which is 1.4 g per kg of actual weight. One number, two denominators.

Which denominator we pick is not a detail, because the product's own user is the case where they
diverge most. Reported per kg of actual bodyweight, the requirement
[is overestimated in 78–100% of people with obesity](https://pubmed.ncbi.nlm.nih.gov/35331517/). On
actual weight at 2.2 g/kg, a 130 kg user would be told to eat 286 g of protein a day. That is not a
stretch goal, it is a number nobody can act on, shown to exactly the person the Weight Loss lens
exists for.

## Decision

**The target is derived from adjusted body weight, and the profile now stores height to compute it.**

```
idealKg    = 22 × height²                       (BMI 22, in metres)
adjustedKg = weight ≤ ideal ? weight : ideal + 0.25 × (weight − ideal)
target     = adjustedKg × the lens's g/kg range
```

The ranges live in `calibration.json`, one per lens, next to the weights:

| Lens | g/kg of adjusted weight |
|---|---|
| Weight Loss | 1.6 – 2.2 |
| Fitness | 1.4 – 2.0 |
| GLP-1 | 1.2 – 2.0 |

`AppUser` gains a nullable `HeightCm`; `PUT /api/profile` requires it alongside weight, in the range
100–250 cm. `GET /api/days/{date}` grows a `protein` block carrying the grams logged and the two
ends of the target. Both ends are null when weight or height is missing — the day still counts and
reports the protein.

**Weight Loss sits above Fitness on purpose.** A deficit raises the per-kg requirement for the same
body; it is not a claim that losing weight needs more protein than training does.

**Ideal weight is computed from BMI, not from a Devine-style formula.** Devine needs sex, which the
profile does not store and Story 2.2 deliberately did not ask for. BMI 22 needs only height.

## Alternatives considered

**Keep actual bodyweight and use 1.2–1.6.** No new field, no migration, and right on average. It
undershoots a lean user by roughly a third, and it would have put Weight Loss *below* Fitness — the
wrong direction for the same body.

**Keep actual bodyweight and use 1.8–2.2**, the number a reader of the sports literature expects.
This is the one that produces 286 g for a 130 kg user.

**Ask for body-fat percentage** and target lean mass directly. The most accurate denominator by a
distance, and the one the literature prefers. Rejected because most people do not know the number,
and a field most users leave empty is a target most users do not get.

**Cap the target in absolute grams.** Hides the symptom at a threshold nobody measured.

## Consequences

**Height is required to save a profile, and the profile was already two fields.** Story 2.2 asked
for weight and lens; it now asks for three. Height rides the consent that already covers weight —
both are health data under the same document, so no new consent is opened.

**A user without weight or height gets no target and still gets their protein.** That is FR-20's
second criterion, extended: the product runs degraded rather than not at all. The invitation to
fill in the measurements is the client's, not the API's — the API returns nulls.

**The target is not rounded.** 118.336 g crosses the wire as it is computed, the way every other
number in the API does. Rounding is a display decision.

**Exceeding the top of the range is not an error anywhere.** The API returns the two ends and the
grams logged, and says nothing about their relationship. That is FR-20's third criterion, and it
took no code to satisfy — only the discipline of not adding a verdict field.

**Adjusted weight is a stand-in for lean mass, not a measurement of it.** It uses a fixed 22 for a
healthy BMI and a fixed quarter of the excess, both conventions rather than measurements of this
person. A user who is heavy because they are muscular is under-targeted by it. Body composition is
what would fix that, and it is not in the profile.

**`ProteinTarget` lives in `Sated.Scoring`, not in the services.** It is a pure function of weight,
height and lens, calibrated by the same file as everything else in there, and it is tested without
a database. The day's protein total lives in a new `Sated.Services/Days`, which is where the Day
Grade of FR-21 will land next to it.
