---
title: 0001 — Fat quality is a component, not a category rule
---

# 0001 — Make fat quality a component, not a category rule

**Status:** accepted
**Date:** 2026-08-25

## Context

The Fullness Factor cannot read a food that is mostly fat: its first term pays a food for carrying
few calories per 100 g, and olive oil collects none of it, so every nearly-pure fat bottoms out at
the same floor. Fat quality — the unsaturated share of the fat, minus a sodium penalty — was
introduced to say something the satiety formula cannot.

It was wired in as a **category rule**: for five fat categories it replaced satiety, and for nuts it
replaced density.

That made it a **switch**. A food inside a listed category was graded on its fat; a food outside it
kept the general formula. The catalogue audit counted **725 pairs** where a food better on every
nutrient the engine reads scored lower than its pair. The clearest: honey mustard dip read 10.5
against regular mayonnaise's 42.7 while dominating it on every input.

Every switch in this engine has produced the same defect — two foods a nutrient cannot tell apart,
graded far apart because one fell on the far side of a boundary somebody drew by hand.

## Decision

Fat quality is computed for **every** food that carries fat, and takes **satiety's weight** in
proportion to the share of the food's energy that is fat, ramping from 0.60 to 1.00.

No fourth weight was added to `calibration.json`.

## Alternatives considered

**A fourth weight in the lens.** Rejected: the lens weights are already the engine's biggest
unvalidated lever, and a fourth would be a number nobody measured. A share derived from the food's
own composition is a measurement; a new weight is a guess.

**A linear ramp from zero instead of from 0.60.** Measured and rejected. Rewarding the unsaturated
share at low fat shares rewards *frying* — potato chips, chicken nuggets, stuffed-crust pizza and
granola all rose from D/E to C, and G0's bottom thirty fell to 25 of 30. What a crisp is bad at is
calories, and satiety already reads that.

**Widening the category list.** Rejected: it does not remove the boundary, it moves it. The 725
pairs would become a smaller number of pairs at a new edge.

## Consequences

- **Every score in the catalogue moved**, so the letter cutoffs had to be refitted the same day
  (`tools/LetterThresholdQuery`). This is the standard cost of a formula change — see
  [Change the engine safely](../how-to/change-the-engine-safely.md).
- Fat quality can now be **absent** (a food with no fat), which means the combiner's renormalisation
  path is exercised by ordinary foods rather than by edge cases.
- FNDDS reports no trans fat, so "unsaturated" here means "not saturated", and **margarine is
  flattered** by this. Accepted knowingly: FNDDS 2021-2023 postdates the FDA ban on partially
  hydrogenated oils, so a margarine reading 80% unsaturated in this data really is.
- Revisiting requires trans fat data per food. SR Legacy has it, through the same recipe join as
  leucine, but its values are from 2018 — before the ban took full effect — so they would grade a
  product that no longer exists.
