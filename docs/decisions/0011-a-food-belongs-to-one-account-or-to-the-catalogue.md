---
title: 0011 — A food belongs to one account, or to the catalogue
---

# 0011 — A food belongs to one account, or to the catalogue

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-11 lets a person add a food the catalogue does not have — a package they can read a label off.
That food is private to them. Until now every row in `Foods` belonged to everybody: 1,933 catalogue
foods, no notion of whose.

`FoodsController` reads `database.Foods` in three places and `FoodGrading` in a fourth. None of the
four is written by anybody thinking about ownership, and none of them has to be wrong for data to
leak — one of them merely has to be *forgotten*. A missing filter does not throw; it returns
somebody else's food and looks like a working search.

The catalogue's category names are not decoration. A name selects the calibrated category rule
(FR-6), and `tools/UserEnteredFoodQuery`, run against the 68 benchmark foods, measured what a
hand-typed food loses: **2 of 68 letters move when the category is gone (2.9%), and 13 of 68 move
when the micronutrients a label does not print are gone (19.1%)**. The category is cheap; the label
is what costs.

## Decision

A nullable `Foods.OwnerId`. Null is the shared catalogue; set means the row belongs to that one
account. The rule is enforced by a **global query filter** registered once on the entity, so every
query EF generates for `Foods` carries it without naming it.

`POST /api/foods` requires a category the catalogue already uses, and requires the seven figures a
nutrition label prints — including carbohydrate, which is checked and then discarded.

## Alternatives considered

**A table per user, created at registration.** A real pattern (table-per-tenant) and the right one
when tenants are companies with contractual isolation. It breaks for individual accounts: ten
thousand accounts is ten thousand tables, every added column is ten thousand migrations, `CREATE
TABLE` moves into the registration path, and "catalogue plus mine" has to build a table name as a
string. Things of the same kind separated by a column; different kinds separated by tables. A
person's food is a food. `Recipe` will get its own table for the opposite reason — it holds a list
of ingredients, which a `Food` does not.

**A separate `UserFoods` table.** Cheaper than one per user and immune to a forgotten filter, but
every search becomes a union of two tables, and a logged meal would later have to point at one of
two kinds of food. The query filter buys the same immunity without either.

**Filtering by hand in each query.** What the filter replaces. Four places today, and the failure
mode is silence.

**An optional category**, which is what this project's first proposal was, on the strength of the
2.9%. Rejected after the second job the category does was named: `GET /api/foods?category=` already
filters by it, and a food with no category is missing from that filter always, not 2.9% of the
time. The measurement said the category is *cheap*, which is not the same as saying it is *useless*.

## Consequences

**`SatedDbContext` now knows who is asking**, through `ICurrentUser` — an interface declared in
`Sated.Data` and implemented in `Sated.Api` over the HTTP request. The data layer states what it
needs and the web layer supplies it, which is the dependency inversion `CLAUDE.md` §4 had filed
under "later".

**That implementation may not use `UserManager`.** `UserManager` needs `SatedDbContext`, so
injecting it into `ICurrentUser` makes the container refuse to start with a circular dependency —
measured, not predicted. It reads the id claim out of the session cookie instead, which needs no
database at all.

**One place turns the filter off on purpose:** `tools/CatalogueLoad` asks whether the table is empty
before filling it and must count rows belonging to everybody — `IgnoreQueryFilters()`. Every new
tool that writes to `Foods` has to decide the same thing, and the default is the safe one.

**A filter that is never written cannot be reviewed either.** Nothing in `FoodsController` says a
food has an owner. Someone reading that file will not learn it there; they learn it from
[database.md](../reference/database.md) or from this record. That is the price of the guarantee.

**Carbohydrate is collected and thrown away.** It is the only field the API asks for and does not
keep. Without it the plausibility check breaks on bread — protein and fat alone imply a quarter of
its energy — so the choice is between storing a nutrient the engine never reads and rejecting real
food. It is asked for at the boundary, used once, and dropped.

**A hand-typed food is graded, not refused, and `isPartial` will read false.** That flag counts
components, not inputs. The honest signal is `isEstimated` on `density` and `proteinQuality`, and
both read true for a label-only food. SM-C4 counts a hidden partial as a product failure; this is
not one, but the naming invites the mistake and the client must not show `isPartial` as "data is
complete".

**Open, not blocking:** nothing marks a food as yours in `GET /api/foods`. The list returns
description and category only, so the catalogue and your own foods are indistinguishable in it.
Story 3.2 (FR-10, provenance) is where that belongs.
