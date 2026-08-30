---
title: 0005 — FNDDS is the catalogue
---

# 0005 — Ship FNDDS 2021-2023 as the catalogue, and keep leucine estimated

**Status:** proposed
**Date:** 2026-08-30

This is the public, technical half of the decision the private planning repository tracks as **D1,
the catalogue source**. The commercial reasoning stays there; what a food row is made of belongs
here, because it is visible in the API.

## Context

`Foods` has twenty columns and no rows. Nothing can be graded until something decides what fills
them, and the choice is not free: the engine was calibrated against a specific catalogue, and parts
of it are keyed to that catalogue by name.

**Coverage was measured, not assumed** (Story 1.2, 2026-08-21, `tools/UsdaCoverageQuery`), over the
whole of each USDA set:

| | Foundation (363) | SR Legacy (7 793) | FNDDS (5 432) |
| --- | ---: | ---: | ---: |
| Satiety, 4 fields | 10,5% | 92,8% | **100,0%** |
| Density, 11 fields | 2,2% | 66,1% | **100,0%** |
| Leucine | 14,6% | **65,2%** | **0,0%** |
| Vitamin D, thiamine (GLP-1 lens) | 14,3% / 46,6% | 66,5% / 95,0% | **100,0%** |
| Portions with a gram weight | 78,5% | 96,7% | **100,0%** |
| **Complete end to end** | 4 (1,1%) | **3 423 (43,9%)** | 0 — leucine is the only gap |

Two sets are already out on their own numbers. **Foundation Foods** yields four complete foods out
of 363 — it is a collection of laboratory analyses, not a catalogue. **Branded Foods** is the only
USDA set carrying added sugar, but measured over 100 products it has 0/50 vitamin E and 1/50
magnesium on yoghurt: it cannot feed the density score at all.

That leaves SR Legacy against FNDDS, and one further fact settles it.

**The engine is not catalogue-neutral. It is keyed to FNDDS category names, exactly as FNDDS
writes them.**

- `calibration.json` records `"catalogue": "FNDDS 2021-2023"`. Every letter cutoff, every density
  and satiety percentile, and the lens weights are `p20/p40/p60/p80` of that population.
- The twenty-one category rules match on the catalogue's own category string (FR-6).
- `ProteinCompleteness` holds a leucine share per category under keys like `Burgers`, `Shellfish`,
  `Pears` — FNDDS category names.

Shipping a different catalogue does not mean loading different rows. It means refitting every
threshold and rewriting every rule key.

## Decision

**The catalogue is FNDDS 2021-2023.** The roughly 500 shipped foods are curated from its 5 431
scoreable rows, and `Foods.Category` stores the FNDDS category string unchanged, because the
category rules read it.

**Leucine stays estimated, and SR Legacy stays out of the shipped data.** SR Legacy 2018 remains
what it already is: an offline measurement input. `tools/LeucineJoinQuery` resolved 2 286 FNDDS
foods into SR Legacy amino acid data through the ingredient codes each survey food carries, and the
per-category leucine shares that came out of it live in `ProteinCompleteness`. `Nutrients_Leucine`
therefore stays null for FNDDS-sourced foods, and the engine's `IsEstimated` flag stays true.

## Alternatives considered

**SR Legacy as the catalogue.** The strongest contender on raw completeness: 3 423 foods with
everything including measured leucine, seven times the 500 needed, and the only USDA set with amino
acids at all.

It loses on two counts, neither of them coverage. First, the calibration above would have to be
refitted end to end on a different population, and the category rules and leucine shares rewritten
against different category names — a rebuild of the engine's fitted layer to gain leucine on a
subset. Second, **its data is from 2018 and is measurably stale in at least one place already**:
`calibration.json` records that SR Legacy puts stick margarine at 14,9 g of trans fat per 100 g,
from before the FDA ban on partially hydrogenated oils took full effect in January 2020. Grading a
margarine on that number grades a product that is no longer sold.

**Joining SR Legacy leucine onto FNDDS rows per food, and storing it.** Tempting, because the join
already exists and would turn an estimate into a measurement for part of the catalogue. Rejected
for now on the evidence of the protein-quality report: SR Legacy's amino acid data carries enough
plausible-but-wrong values that a per-food score built on it was judged unusable, and only the
aggregate — a median share over many foods — survived that finding. Storing a measured leucine for
some foods and an estimate for others would also make two foods' protein scores incomparable while
both display as grades.

**Open Food Facts for European packaged products.** Not a competitor for this decision; it answers
a different question. Measured on 50 Romanian products, satiety is calculable for 58% and density
for 2%, because EU labelling makes fibre and micronutrients optional. It belongs to a later
packaged-products layer where Partial Grade is the rule, not to the MVP catalogue.

## Consequences

- **Every shipped food's protein quality is an estimate**, flagged `IsEstimated`. A user sees that
  marking on every food, not on a few. This is the price of the decision and it is visible in the
  product.
- **`Nutrients_Leucine` will be null across the initial load.** The column stays, because
  user-entered and later catalogue sources can carry a measured value.
- **FNDDS carries no trans fat**, so no rule may depend on it without changing this decision.
- **`Category` becomes load-bearing data, not a label.** A normalised, translated or tidied-up
  category silently changes which rule applies. It is stored exactly as it arrives for that reason.
- **The names are American and as-consumed.** Internationalisation is translating a name onto a
  row, which the international addendum already measured as the cheap half; the expensive half was
  packaged products, and that is out of scope here.
- **The next FNDDS release re-measures every percentile.** This decision therefore feeds directly
  into D2 — whether a letter is frozen for the life of a food or versioned with the catalogue —
  and D2 should be taken before the first release lands.
- **Curation is now the open work, not coverage.** Choosing 500 rows out of 5 431 needs a written
  rule, and one problem is already known and unsolved: 30 powders grade A because the grade is per
  100 g of the catalogued form rather than the eaten form — `Cocoa powder, not reconstituted`
  scores A 94,1. That cannot be detected from nutrients. It is the first thing the curation rule
  has to answer.
