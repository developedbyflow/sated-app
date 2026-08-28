---
title: How a food becomes a letter
---

# How a food becomes a letter

The whole engine in six steps, in plain language. No terminology, no comparisons — just what happens,
in order.

## 1 · What goes in

To the engine, a food is **a category and 16 numbers**: how many calories it has, how much protein,
fat, fibre, and which vitamins. All per 100 grams.

If a number is missing from the catalogue, it is left empty. **Empty is not zero.** Zero means "we
measured, and it has none." Empty means "we don't know."

## 2 · Four formulas read the same numbers

Each one answers a different question:

- **Satiety** — how full it keeps you, relative to the calories it costs
- **Density** — how many nutrients it gives you per 100 calories
- **Protein** — how close it comes to the amount that actually builds muscle
- **Fat** — how good the fat in it is

The first three come from published work. The fourth is ours, because nothing published existed.

## 3 · Raw numbers become positions

A formula produces a raw number. Raw broccoli's density is **184**. But 184 out of what? On its own
it means nothing.

The engine looks at where the food falls **against the other 5,431 in the catalogue**. Broccoli beats
97% of them, so it gets **97**.

It does not say how good the food is. It says **how many it beats**.

## 4 · The four become one number

Each goal weighs them differently:

```
losing weight:   satiety 50% · density 35% · protein 15%
building muscle: satiety 25% · density 25% · protein 50%
```

If a formula could not answer, **zero is not substituted**. The engine divides by the weight it
actually used.

The result is **one number between 0 and 100**.

## 5 · The number becomes a letter

Each goal has its own cutoffs. Losing weight, A starts at **72.01**. Building muscle, at **70.99**.
**They are not the same cutoffs** — which is why one food can carry different letters.

The cutoffs split the catalogue into five equal parts, so roughly **20% of foods get A and 20% get E**.

Two exceptions:

- a food with very low density **cannot beat E**, however good it is otherwise
- a food with no calories at all — water — **gets no letter**. Not A, not E. Nothing.

## 6 · The dials

Every adjustable number — cutoffs, percentages, positions — lives in **a separate file**, not in the
code. Next to each one is written why it is what it is.

Change one, and **every grade in the catalogue moves**, so they have to be redone in order.

---

> The engine measures a food four ways, sees where it falls against five thousand others, adds those
> up using the weights your goal cares about, and cuts the result into five letters.

Each step has its own page: [what a food is](what-a-food-is.md) ·
[the four scores](the-four-scores.md) · [why scores are relative](why-scores-are-relative.md) ·
[architecture](architecture.md)
