---
title: 0019 — The calorie target is its own resource
---

# 0019 — The calorie target is its own resource

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-31 gives the Day Ring a third axis the user may set, and states three things about it at once:
it is completely optional, **onboarding does not ask for it**, and the system never derives it from
weight, age or activity. FR-32 adds that a target under 1,200 kcal/day earns a warning that does not
block.

The first two are a shape constraint, not a validation rule. A calorie target that lives as a field
on `ProfileRequestDto` is asked for on every profile save by construction, and worse: a client
updating only the weight would silently wipe it, because a missing field and an explicit `null`
arrive identically in a `double?`.

## Decision

**Its own resource**, beside the profile rather than inside it:

```
PUT    /api/profile/calorie-target   { "kcal": 2000 }
DELETE /api/profile/calorie-target
```

`AppUser` gains `CalorieTargetKcal`, an `int?`. `GET /api/days/{date}` grows a `calories` block that
is **absent entirely** — `null`, not an object with a null target — when no target is set.

This is the argument [0009](0009-consent-is-a-document-and-a-signature.md) already makes about
consent: *it is its own request against its own resource, never a field inside the request that
carries the data, because then it would not be a separate act.* Setting a calorie goal is a separate
act from saying how much you weigh.

**The warning is returned by the write that causes it**, and nowhere else:

```json
{ "kcal": 1100, "warning": "Below 1,200 calories a day. Consider talking to a doctor." }
```

Never stored, never repeated on the Day Ring. FR-32 asks for it once, at setting, not daily — so
the place that carries it is the response to the setting.

**The threshold is below 1,200, not below-or-equal.** A target of exactly 1,200 does not warn.

**No consent is required to set one**, and withdrawing consent does not clear it. It is a preference
you type, like the active lens — not a measurement of your body, and not something you ate. That
puts it on the same footing as `ActiveLensId`, which 0009 already leaves in place.

## Alternatives considered

**A field on `PUT /api/profile`.** Fails the criterion directly and carries the wipe-on-partial-update
bug described above.

**Return `consumed` always, with `targetKcal: null`.** More uniform with protein, which does report
absolute grams with no target. Rejected because FR-22 is explicit that the third axis *disappears
completely*, with no placeholder and no invitation to set one. The asymmetry is the product's
position: protein is what Sated is about, calories are what it will count if asked. **If you have
not asked for calories, Sated does not show you calories.**

**Block a target under 1,200.** Ruled out by FR-32 before this story started: the value is accepted,
always.

**Put the warning on `GET /api/days/{date}`.** That is the daily repetition FR-32 rules out.

## Consequences

**`[Range(500, 20000)]` rejects a target below 500 kcal.** A hard floor exists to catch a typo, and
sits far enough below 1,200 that the FR-32 warning can never be turned into a block by it. A number
between the two is always accepted with the warning; below 500 is not a diet anybody chose.

**The warning text ships from the API in English**, next to the threshold that produces it, rather
than as a flag the client renders. That matches every other user-facing string the API already
returns, and FR-32 names the exact wording. It is also the thing to revisit first if the product is
ever translated — C3 in addendum 6.

## The consent document was fixed here too

Two promises the document made and the code did not keep, both found while building this story and
both proven by a failing test before being fixed:

**Height was not covered and not erased.** [0017](0017-derive-the-protein-target-from-adjusted-body-weight.md)
added `HeightCm` an hour earlier and did not touch the consent text, which still said Sated needed
*two* kinds of health data. Withdrawal nulled the weight and left the height behind. The text now
names three, and `Consents.Erase` clears both.

**Logged food was not erased.** The document has said *"Withdrawing deletes the data it covers: your
weight, and everything you have logged"* since Story 2.3 — written before `Meal` existed. Story 4.1
added the logging and never came back to the erasure, so withdrawal left every meal in place.
`Erase` now removes the user's `Days`, and the cascade takes meals and entries with them.

**The document text was amended in place rather than published as a new version.** A signed text
must never change under the signature, and this one has no signatures: the product has no users.
The moment it has one, any change to this text is a new `ConsentDocument` row and a re-consent flow
that does not exist yet. Written down here so that is a decision next time, not a habit.

**Withdrawal still leaves your own foods and recipes.** The document names the weight, the height
and what you have logged. A food you typed and a recipe you composed are neither — they are things
you made, and `DELETE /api/account` is what removes those. Recorded so the difference is deliberate.
