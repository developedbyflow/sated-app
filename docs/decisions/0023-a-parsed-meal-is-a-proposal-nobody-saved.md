---
title: 0023 — A parsed meal is a proposal nobody saved
---

# 0023 — A parsed meal is a proposal nobody saved

**Status:** accepted
**Date:** 2026-09-01

## Context

FR-14 lets somebody type *"chicken burrito bowl with rice and beans"* instead of running four
searches. The architecture already chose the provider and the shape of the call. What was still
open is everything around it: where the seam sits, what the model is shown, what happens to what it
returns, and what happens when it does not return anything.

This record covers the half that needs no network. The call itself, the token accounting and the
timeout budget follow with the provider.

### What the measurement said

The architecture priced this against a catalogue of *"~500 foods, ~5,000 tokens cached"*. It was
written on 2026-08-10, before FNDDS was loaded.

**The catalogue is 1 933 foods.** Written as `id description`, one per line, it is 77 529
characters — **roughly 19 000 to 21 500 tokens**, four times the estimate. Adding the category to
each line takes it to 116 446 characters, **around 30 000 tokens**.

Nothing about the design flips: the input side of the bill is four times the estimate, which on the
same assumptions moves the cost from about $0.04 per person per month to about $0.16 — still under
4% of a $4.99 subscription. **Cost still does not decide anything here.** But the number in the
architecture was wrong by 4x and is now marked there.

## Decision

```
POST /api/meals/parse      →  { items: [...], unrecognised: [...] }
```

**Nothing is saved.** The answer is a proposal; the confirmation is the `POST /api/meals/{id}/entries`
that already exists. A parse endpoint that logged would make the confirmation modal FR-14 asks for a
decoration rather than a gate.

**The prompt carries `id description` and nothing else.** The category would add half as many tokens
again for signal the description mostly repeats — `Peach, canned, in syrup` already says what
`Peaches and nectarines` would.

**The prompt is built in one place, and its order is the cache.** The shared catalogue first, sorted
by id; the person's own foods after, under their own heading, sorted the same way. A test holds the
property that matters: **the prompt for somebody with their own foods starts with exactly the prompt
for somebody with none.** That is what makes the long prefix identical for every user and therefore
cacheable. An unstable order would break it and nothing would report an error — the bill would
simply grow.

**Every `food_id` is checked against what that person can actually reach.** The schema guarantees
the shape of the answer, never its content. Three different failures land in the same place, and
none of them substitutes a food:

| what came back | what happens |
|---|---|
| an id no row carries | the raw words go to `unrecognised` |
| an id belonging to somebody else's food | the same — the query filter never offered it, so it cannot come back |
| a quantity of zero | the same — the schema requires the field, not a usable number |

**No provider is a `503` that names the way out**, not an error page: *"Nothing was logged and
nothing was lost. Search for each food instead."* `NotConfiguredMealParser` is that branch written
as code, and it is what ships until a key exists. The same branch answers a timeout, a refusal and
a 429 when the real parser arrives, so the degradation path is exercised from the first day rather
than written the day it is needed.

**`quantityEstimated` survives the confirmation, and typing over the quantity clears it.** The
column existed since the first meals migration and nothing had ever written to it. Now the entries
endpoint carries it, and `PUT /api/meals/{id}/entries/{entryId}` sets it back to false — somebody
who types a number is no longer being guessed at.

## Alternatives considered

**Parse straight into a meal, and let the person delete what is wrong.** Fewer requests, and it
inverts FR-14: the model's mistakes would be in the database before anybody looked at them.

**Send only the foods a search already matched.** A shorter prompt, but then the code picks the
candidates by approximate name matching — which is the job being handed to the model. It also makes
the prompt different for every request, so nothing caches.

**Let the model answer with names instead of ids, and match afterwards.** Same problem one step
later, plus the matching has no evidence to work with: `rice` matches 40 rows.

**Throw on an unavailable provider and let the middleware answer.** An exception would make the
ordinary case — no key configured — look like a defect in the logs. The codebase already answers
"could not" with a value, as `FoodRejection` and `MealRejection` do.

## Consequences

**`Sated.Services` now references `Sated.Parsing`.** The boundary the architecture actually cares
about still holds: `Sated.Scoring` references nothing, so the engine cannot reach the network even
by accident, and G0 keeps running without a server.

**Nothing logs tokens yet.** `prompt_tokens`, `cached_tokens`, `completion_tokens` and the model
name are the measurement Q3 needs, and they only exist once a real call does. Until then the cost
per logged meal is still an estimate.

**The daily cap per person is still deferred.** FR-14 stays the only variable cost in the product,
and this record does not add the ceiling that bounds it.

**`unrecognised` means two things.** Mostly "no such food", but a zero quantity lands there too. The
screen action is the same for both — search for it — so the person is not shown the difference. If
that ever needs separating, the seam is `MealParsing`, one place.
