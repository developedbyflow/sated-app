---
title: 0021 — A swap beats the letter, and is ranked by score
---

# 0021 — A swap beats the letter, and is ranked by score

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-19 asks for three foods from the same category with a better grade, on explicit request only,
with the same input always producing the same three.

Grades are not stored — they are computed at read time — so answering means grading a whole
category per request. That was the worry going in. It is not the problem.

### What the measurements said

`tools/SwapCandidateQuery`, over the 1 933 catalogue foods:

**Speed is not a constraint.** The largest category, `Chicken, whole pieces` at 160 foods, grades
in **0.49 ms**. The whole catalogue grades in 13 ms. The median category holds 15 foods. No cache,
no stored grades, no precomputation — filter the category in SQL, grade in memory, answer.

**Ties are real and they decide membership.** Of the 931 foods with more than three candidates,
**280 have an exact score tie between third and fourth place**. Without a second sort key, "the same
three every time" is a promise the code does not keep.

**44.0% of foods have no better letter in their category.** The empty answer is not an edge case.

## Decision

```
GET /api/foods/{id}/swap?lensId=…
```

**Candidates must beat the food on the letter.** Ranked among themselves by **score**, descending,
with an exact tie going to the **lower id**. First three.

```json
{
  "alternatives": [
    { "id": 6410, "description": "Nectarine, raw", "grade": "A", "score": 75.67 },
    { "id": 6417, "description": "Peach, canned, juice pack", "grade": "A", "score": 75.19 },
    { "id": 6414, "description": "Peach, raw", "grade": "A", "score": 75.03 }
  ],
  "message": null
}
```

**Nothing better is a `200` with an empty list and the message**, never a 404 and never a
consolation. The text ships from the API in English, as the calorie warning does in
[0019](0019-the-calorie-target-is-its-own-resource.md).

**`lensId` is explicit**, matching `/grade` and `/grades`. Swap needs no account, so the public food
page of Story 9.1 can use it.

**Only catalogue foods are suggested** — `OwnerId == null`, written out rather than left to the
query filter. Without it a signed-in caller would see their own foods among the suggestions and a
stranger would not, so the same food would answer two different ways depending on who asked.

## The tension with `Grade.cs`

`Grade.cs` says, in its own summary, that the letter is *"not a way to compare two foods: a Swap
compares scores within a category, because two foods can share a letter while their scores differ
sixteenfold."* That argues for selecting candidates by score, not by letter. Measured, the two rules
are very far apart:

| rule | no alternatives | three or more |
|---|---|---|
| strictly better **letter** | 44.0% | 50.9% |
| strictly higher **score** | 4.2% | 88.9% |

Selecting by score would almost always find three. But **42.2% of those suggestions would carry the
same letter as the food you are looking at** — the product would answer "swap this E for these three
E's". Butter (E, 12.3) would be offered lard (E, 30.7) and ghee (E, 19.6).

**The letter wins, because the letter is what the product says out loud.** Offering a swap that does
not change the letter contradicts the letter. The epic agrees, and wrote the copy for the empty case
in advance: *"No higher-graded foods in this category."* — *higher-graded*, not *higher-scoring*.

The note in `Grade.cs` is honoured where it applies: **within** the candidates, the ranking is by
score, which is exactly the sixteenfold spread it warns about.

**This is one line to flip** if the product later prefers coverage over consistency with the letter.
The number to weigh it against is above: 44.0% silence versus 42.2% suggestions that change nothing
the user can see.

## Alternatives considered

**Store the grades so a swap is a query.** Nothing to buy: 0.49 ms is already below the cost of the
round trip, and stored grades would have to be invalidated on every calibration change — which is
the thing [0018](0018-the-day-is-one-plate.md) and Story 8.2 deliberately avoid by never storing a
letter.

**Rank by letter alone, then by id.** Loses the sixteenfold spread `Grade.cs` warns about: an A at
95 and an A at 71 would be interchangeable.

**Return more than three when they exist.** FR-19 says three. A longer list turns a suggestion into
a browse, which is what `GET /api/foods?category=…` already is.

## Consequences

**The partial-grade exclusion cannot be reached by any food a request can create.** `IsPartial` means
density was unavailable, which happens only at exactly zero calories, and a food at zero calories
with no macronutrients is nutritionally empty and carries no letter at all — so it is already
excluded by needing a better letter. The rule is written anyway, because it is an acceptance
criterion and because the engine could change. Its test seeds a row **no API path can produce**:
zero calories with 5 g of protein, which grades A at 80.35 and would otherwise top the list.
Recorded so nobody reads that test as a realistic case.

**Deleting the tiebreaker is not caught by any test.** Reversing it is — the third slot changes from
`Peach, raw` to `Peach, frozen`, two rows with identical nutrients. Deleting it entirely leaves the
result unchanged, because LINQ's sort is stable and Postgres happens to return rows in insertion
order. **That accident is exactly what the line defends against**: Postgres promises no order for a
query without `ORDER BY`. The line stays, and this paragraph is why no test proves it.

**A food is never compared with itself, and nothing says so.** The self-exclusion was written first
and then removed: a food is never strictly better than itself, so the condition was dead, and no
mutation of it could fail a test.
