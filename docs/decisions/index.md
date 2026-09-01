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
| [0003](0003-controllers-over-minimal-apis.md) | Build the HTTP layer on controllers, not minimal APIs | accepted |
| [0004](0004-nutrients-are-an-owned-type-on-food.md) | Store nutrients as an owned type on `Food` | accepted |
| [0005](0005-fndds-is-the-catalogue.md) | Ship FNDDS 2021-2023 as the catalogue | accepted |
| [0006](0006-load-the-catalogue-once-then-own-it.md) | Load the catalogue once, then own it | accepted |
| [0007](0007-test-the-foods-query-against-a-real-database.md) | Test the foods query against a real database | accepted |
| [0008](0008-keep-the-session-in-an-httponly-cookie.md) | Keep the session in an HttpOnly cookie | accepted |
| [0009](0009-consent-is-a-document-and-a-signature.md) | Record consent as a document and a signature | accepted |
| [0010](0010-ask-for-the-password-before-export-and-deletion.md) | Ask for the password before export and deletion | accepted |
| [0011](0011-a-food-belongs-to-one-account-or-to-the-catalogue.md) | A food belongs to one account, or to the catalogue | accepted |
| [0012](0012-store-the-source-of-a-row-and-derive-the-rest.md) | Store the source of a row and derive the rest | accepted |
| [0013](0013-a-recipe-stores-its-parts-and-derives-everything-else.md) | A recipe stores its parts and derives everything else | accepted |
| [0014](0014-import-usda-servings-sorted-and-keep-the-default-apart.md) | Import USDA servings sorted, and keep the default apart | accepted |
| [0015](0015-freeze-the-day-and-the-grams-when-a-meal-is-logged.md) | Freeze the day and the grams when a meal is logged | accepted |
| [0016](0016-unpack-a-recipe-when-it-is-logged.md) | Unpack a recipe when it is logged | accepted |
| [0017](0017-derive-the-protein-target-from-adjusted-body-weight.md) | Derive the protein target from adjusted body weight | accepted |
| [0018](0018-the-day-is-one-plate.md) | The day is one plate | accepted |
| [0019](0019-the-calorie-target-is-its-own-resource.md) | The calorie target is its own resource | accepted |
| [0020](0020-compare-every-lens-in-one-request.md) | Compare every lens in one request | accepted |
| [0021](0021-a-swap-beats-the-letter-and-is-ranked-by-score.md) | A swap beats the letter, and is ranked by score | accepted |
| [0022](0022-a-public-page-is-reached-by-a-slug.md) | A public page is reached by a slug, and only catalogue foods have one | accepted |
| [0023](0023-a-parsed-meal-is-a-proposal-nobody-saved.md) | A parsed meal is a proposal nobody saved | accepted |
| [0024](0024-the-provider-is-chosen-by-a-key.md) | The provider is chosen by a key, and the schema is not what the exporter hands you | accepted |
| [0025](0025-a-reset-link-is-single-use-and-says-nothing-about-who-has-an-account.md) | A reset link is single use, and says nothing about who has an account | accepted |

## The scoring decisions are not here yet

Ten decisions (D1–D10) were taken while building the engine and are recorded in the **private**
`sated-docs` repository. Seven are closed; **D1** (catalogue source) and **D2** (whether a letter is
frozen for life or versioned) are still open, and D1 blocks Epic 3.

**D1 has been split.** Its technical half — which catalogue fills the `Foods` table, and what that
costs — is [0005](0005-fndds-is-the-catalogue.md), accepted on 2026-08-30. What stays private is
the commercial reasoning around it.

Porting them here is a deliberate choice, not an oversight: some carry commercial reasoning that
belongs in a private repo, and `sated-app` is public. Each one needs to be read and split before it
moves.
