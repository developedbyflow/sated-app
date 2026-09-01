---
title: 0022 — A public page is reached by a slug, and only catalogue foods have one
---

# 0022 — A public page is reached by a slug, and only catalogue foods have one

**Status:** accepted
**Date:** 2026-09-01

## Context

FR-30 gives every catalogue food a public page at `/food/{slug}`, readable without an account, so
a visitor who lands from a search engine gets a real answer before signing up. FR-11 keeps a food
somebody added for themselves off that web entirely.

A search engine sends the visitor a name, not a number, and the API has only had numbers so far.
Something has to turn `Milk, whole` into `milk-whole` and back.

### What the measurements said

`tools/SlugQuery`, over the 1 933 catalogue foods:

**Descriptions make unique slugs on their own.** 1 933 descriptions produce **1 933 distinct
slugs** — no collisions at all. So no disambiguating suffix: the page is at `milk-whole`, not
`milk-whole-5348`. This could easily have gone the other way, because FNDDS descriptions often
differ only in punctuation (`skin / coating not eaten` against `skin, coating not eaten`).

**A maximum length is what would create collisions.** The longest slug is 110 characters. Cutting
every slug to a cap gives **0 collisions at 100 characters, 10 at 80, 25 at 60, 223 at 40**. The
two longest slugs are both 110 characters and first differ at character **100** — a cap of 99 would
merge two different omelettes. So: no cap.

## Decision

```
GET /api/foods/by-slug/{slug}
```

**The slug is a stored column on `Food`, nullable, with a unique index.** It is written once, at
import, by `Slug.From(description)` in `Sated.Parsing`: lowercase, every run of characters that are
not ASCII letters or digits becomes one hyphen, hyphens trimmed off both ends. The migration
backfills the 1 933 existing rows with the same rule written in SQL, and `tools/SlugQuery` confirms
the two agree on **all 1 933 rows**.

**A food somebody added for themselves gets no slug — the column stays `null`.** That is how FR-11
is enforced: no slug, no page, and nothing to guess. It also removes a failure that a non-null
column would have created, where somebody adding their own *Peach, raw* would collide with the
catalogue's row on the unique index and get a `500`. Postgres treats `null`s in a unique index as
distinct, so any number of private foods can carry none.

**The lookup is exact.** `Milk-Whole` is a `404`, not a redirect. One food, one URL — the frontend
lowercases its route parameter before it calls.

**The endpoint answers with the same body as `GET /api/foods/{id}`**, byte for byte, and the page
then calls `GET /api/foods/{id}/grades` with the id it got. Story 9.1 asks that the public build
call the same endpoints as the signed-in app; two requests that already exist beat one new one that
would only exist for this page.

## Alternatives considered

**`GET /api/foods/{slug}`, sharing the route with the id.** A prettier URL, but one route with two
meanings, and `/api/foods/categories` would keep working only because ASP.NET prefers a literal
segment over a parameter. `by-slug` says what it does.

**One endpoint returning the whole page — food, every lens, breakdown, provenance.** Fewer round
trips, and rejected by the acceptance criterion itself: *"the frontend build calls the same public
endpoints as the authenticated app — no parallel data path"*. It would also be the only endpoint in
the API shaped by a screen rather than by a resource.

**No column: match the normalised description in SQL.** No migration, no backfill, and no index —
every request would scan and normalise 1 933 descriptions. It also makes the slug a derived thing
that cannot be corrected by hand if a description ever needs it.

**A slug for every food, unique only within the catalogue** (a filtered index). Rows would carry a
name for a page they never get.

## Consequences

**`Slug.From` runs in exactly one place**, the importer, and the API never generates a slug — it
only matches one. If a catalogue food is ever added through an endpoint rather than an import, that
endpoint has to set the slug, and `Sated.Services` does not reference `Sated.Parsing` today.

**The description can no longer change quietly.** Nothing changes it today — it is written at import
and at `POST /api/foods`, never updated — but if a correction ever edits one, the slug is now a
second thing to think about, and an already-indexed URL is a third.

**The unique index is proved by a test the API cannot exercise.** It inserts two catalogue rows
under one slug directly into the database, because no request can create a second catalogue food.
Removing `unique: true` from the migration fails exactly that test, and nothing else.

**So is the `OwnerId == null` in the lookup.** No food an owner can create carries a slug, so the
test that kills that condition seeds an owned row *with* one — a row no API path produces. That is
the point: the condition is what holds if a future bug ever writes such a row, and the test says so
out loud rather than leaving the line unexplained.
