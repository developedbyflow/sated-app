---
title: 0003 — Build the HTTP layer on controllers
---

# 0003 — Build the HTTP layer on controllers, not minimal APIs

**Status:** accepted
**Date:** 2026-08-29

## Context

`Sated.Api` was a 14-line file with no endpoints: any address returned 404. Epic 2 — accounts,
onboarding and data rights — cannot begin without an HTTP layer, and neither can anything that
follows it.

ASP.NET Core offers two ways to define an endpoint, and they are not layered on top of each other
in a way that makes the choice reversible for free. Both live in the same application, so a project
that mixes them ends up with two conventions for the same job.

Two facts shape the choice more than any benchmark:

- **The API will hold roughly eight resources**, not one: lenses, foods, recipes, meals, days,
  account, export, and the public food pages. That is the size at which the *organising* question
  starts to matter more than the syntax.
- **This codebase is also a learning vehicle.** It is stated in `CLAUDE.md §1` as a first-class
  goal, alongside shipping the product. The .NET work Florin is preparing for is
  overwhelmingly controller-based.

## Decision

Endpoints are **controllers**: one class per resource under `Controllers/`, marked
`[ApiController]`, routed by attribute, returning DTOs declared under `Contracts/`.

## Alternatives considered

**Minimal APIs.** A real contender, not a strawman. For a single endpoint they read better, and
`GET /api/lenses` would have been three lines inside `Program.cs`.

The deciding reason is **not capability**. Minimal APIs are no longer the bare option they were in
.NET 6; the gap on validation and error shaping has largely closed. They lost on the two facts
above: at eight resources, keeping endpoints out of `Program.cs` requires inventing a grouping
convention by hand, which is the thing controllers already are — and the second goal of this
codebase is served by writing the shape that the industry writes.

**Mixing both** — minimal APIs for trivial reads, controllers for the rest. Rejected outright. Two
conventions for one job means every new endpoint starts with a decision that has no criterion, and
cross-cutting concerns (auth, validation, error shape) have to be configured twice.

## Consequences

- The first endpoint costs more words than it would have: an attribute, a base class, a folder.
  This is paid once; every endpoint after it is a method.
- `[ApiController]` is now load-bearing for **input validation and error shape**. It supplies the
  automatic `400` with a `ProblemDetails` body when model validation fails. Removing the attribute
  from a controller silently removes that, which is a security-relevant change disguised as a
  cosmetic one.
- **Route stability is now tied to class names.** `[Route("api/[controller]")]` derives the URL from
  the class name, so renaming `LensesController` changes a public URL without a compiler error.
  Endpoints that reach the public web — the food pages of FR-30 — should carry a literal route
  instead.
- Startup time and AOT trimming are slightly worse than the minimal-API equivalent. Irrelevant at
  this size; it would become the reason to revisit if the API were ever deployed as a
  cold-start-sensitive function rather than a long-running service.
