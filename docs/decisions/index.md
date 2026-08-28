---
title: Decisions
---

# Decision records

An **ADR** (Architecture Decision Record) is one file per decision: what was decided, what the
alternatives were, and what it costs. One file, never edited to hide a reversal — a decision that
turns out wrong gets a **new** record that supersedes the old one, and the old one stays.

That is the property this folder exists for. When Epic 2 asks "why is the engine a pure library
with no database?", the answer is a file with a date on it, not a memory.

## Format

Numbered, four digits, kebab-case, and the number is never reused:

```
0001-fat-quality-is-a-component-not-a-category-rule.md
0002-...
```

Each file follows [`_template.md`](_template.md): **Status · Context · Decision · Consequences**.
Status is one of `proposed`, `accepted`, `superseded by 00NN`.

## Records

| # | Decision | Status |
|---|---|---|
| [0001](0001-fat-quality-is-a-component-not-a-category-rule.md) | Fat quality is a component, not a category rule | accepted |
| [0002](0002-count-added-micronutrients-like-inherent-ones.md) | Count added micronutrients like inherent ones | accepted |

## The scoring decisions are not here yet

Ten decisions (D1–D10) were taken while building the engine and are recorded in the **private**
`sated-docs` repository. Seven are closed; **D1** (catalogue source) and **D2** (whether a letter is
frozen for life or versioned) are still open, and D1 blocks Epic 3.

Porting them here is a deliberate choice, not an oversight: some carry commercial reasoning that
belongs in a private repo, and `sated-app` is public. Each one needs to be read and split before it
moves.
