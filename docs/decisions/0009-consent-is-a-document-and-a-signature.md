---
title: 0009 — Consent is a document and a signature
---

# 0009 — Record consent as a document and a signature, not a flag

**Status:** accepted
**Date:** 2026-08-31

## Context

Story 2.2 asks the user for weight and an active lens, and its acceptance criteria require explicit
consent for that data, "recorded separately". Weight and food logs are health data, which the law
treats as a special category with its own conditions.

Four of those conditions decide the shape of the record, and they are not all about the screen:

- **The controller must be able to demonstrate that consent was given.** That means who, when, and
  **to which text** — not that a box was ticked.
- **The request must be clearly separable** from anything else the user agreed to.
- **Withdrawal must be as easy as giving**, and the user must be told so before consenting.
- **Consent must be freely given.** Sated cannot function without weight and food logs, so a user
  cannot refuse and stay. Whether that is still "free" is a live question, recorded as `C1` in the
  private `02_planning/6.prd-addendum-account-security` §6.3. It does not change what is built.

The first draft of this work had a `bool` on the user and no withdrawal. Both were wrong, and the
reasoning for dropping withdrawal — "account deletion already covers it" — was wrong too: deleting
an account is not as easy as unticking a box.

## Decision

Consent is two tables.

`ConsentDocuments` holds the **text itself**, with a purpose, a version and a publication date.
Rows are only ever added. `Consents` holds one row per user per act, pointing at the exact document
signed, with `GivenAt` and a nullable `WithdrawnAt`.

`POST /api/consents/{purpose}` names the version being signed, so a client cannot consent to a text
it never fetched. `DELETE /api/consents/{purpose}` withdraws, and **erases the data that consent
covered** — today the weight.

`PUT /api/profile` answers `403` while no consent stands.

## Alternatives considered

**A `ConsentGiven` boolean on the user.** The obvious first move, and what most tutorials show.

It fails the first condition on its own: a `true` does not say when, and says nothing at all about
what the person read. It also silently rewrites history — change the wording six months later and
every existing `true` now appears to endorse a text nobody saw.

**A version string with no stored text.** Better, and still not enough. `"v1"` is a label; if the
v1 wording lives only in a React component that has since been edited, the label proves nothing.
Keeping the text in the database is what turns the record into evidence.

**Consent as a field inside `PUT /api/profile`.** Fewer requests, and it reads naturally: send the
weight and the agreement together. Rejected because it is precisely what "separately" forbids —
consent bundled into the act of submitting the data is not a distinguishable request. The `403` is
the same rule stated as behaviour rather than as a comment.

## Consequences

- **Every new kind of health data has to join the withdrawal path.** Today `Withdraw` clears the
  weight. When food logs arrive in Epic 4 they must be cleared there too, and nothing in the
  compiler will say so. This is the maintenance cost this decision buys, and it is real.
- **Changing the wording means publishing a new row**, not editing the old one. Old signatures keep
  pointing at what they signed. A translation is a different text and therefore a different
  version — recorded as `C3` in the addendum.
- **The seeded first document lives in a migration.** It ships with the schema, which means the
  text is under version control and identical everywhere, but also that correcting a typo in it is
  a migration rather than an edit.
- **Withdrawal leaves `ActiveLensId` in place.** The consent text names the weight and the logs, so
  the behaviour matches what was agreed. Whether a stated goal of "Weight Loss" is itself health
  data is open — `C4` in the addendum. Changing it later means a new document version, not a
  silent change.
- **The recorded time is cut to microseconds before it is stored.** PostgreSQL keeps six decimal
  places; .NET keeps seven. Without the cut, the response to `POST` carried a digit the database
  then dropped, so asking for the same consent again returned a different timestamp for the same
  act. On macOS the clock only produces microseconds, so this was invisible locally and failed on
  the Linux runner — the account tests now inject a clock that always carries the extra digit, or
  the rule could not fail here at all.
- `403` rather than `400` for missing consent: the request is well formed and the caller is known.
  What is missing is permission, not a field.
