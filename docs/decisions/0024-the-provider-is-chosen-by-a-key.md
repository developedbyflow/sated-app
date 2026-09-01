---
title: 0024 — The provider is chosen by a key, and the schema is not what the exporter hands you
---

# 0024 — The provider is chosen by a key, and the schema is not what the exporter hands you

**Status:** accepted
**Date:** 2026-09-01

## Context

[0023](0023-a-parsed-meal-is-a-proposal-nobody-saved.md) built everything around the call. This is
the call: the OpenAI SDK, the schema that constrains the answer, where the key lives, and what
decides whether the product talks to a paid service at all.

The architecture picked the provider on 2026-08-11 and named the mechanism: `OpenAI` 2.x, Chat
Completions with `ChatResponseFormat.CreateJsonSchemaFormat`, `strict: true`, a schema generated
from the C# type by `JsonSchemaExporter`, and `prompt_cache_key`. Reading the SDK rather than
assuming it moved two of those.

### What the SDK actually offers

**`prompt_cache_key` is not on Chat Completions.** In `OpenAI` 2.13.0 the property exists on
`OpenAI.Responses.CreateResponseOptions` and nowhere else. `ChatCompletionOptions` carries
`EndUserId`, `SafetyIdentifier` and `Metadata`, none of which is it. The two things the
architecture asked for cannot both be had on the same call.

**`JsonSchemaExporter` does not produce a strict-mode schema on its own.** Exported straight from
`ParsedMeal` with web defaults, three things are wrong for `strict: true`:

| what comes out | why | fix |
|---|---|---|
| `"quantityGrams": { "type": ["string","number"], "pattern": "…" }` | `JsonSerializerDefaults.Web` allows reading numbers from strings | plain options with a camelCase policy, not the Web preset |
| `"type": ["object","null"]`, on the root and on every item | the exporter treats a null-oblivious reference type as nullable | `TreatNullObliviousAsNonNullable = true` |
| no `additionalProperties` anywhere | never emitted | added in `TransformSchemaNode` |

`required` is the one strict mode wants that the exporter already gets right: every constructor
parameter without a default value is listed. Writing it again in the transform would be a line no
test could kill.

## Decision

**Chat Completions, not Responses.** `prompt_cache_key` is a routing hint — it helps a request
reach a machine that already holds the prefix. It is not what makes caching happen; that is exact
prefix matching, which happens either way, and `usage.InputTokenDetails.CachedTokenCount` reports
the result either way. So the measurement that matters is available on both, and the simpler,
better-documented surface wins in a codebase whose declared purpose is learning .NET.

**This is one class to swap** if the cached count turns out disappointing. The number to swap on is
`CachedTokenCount` staying near zero across repeated calls.

**The schema is generated once, from the record, and the strictness is tested rather than trusted.**
Six tests hold the rules `strict: true` demands. One of them fails the moment somebody gives a
parameter a default value — which silently drops it from `required` and would be rejected by the
API at the first call.

**The key lives in .NET user secrets, never in a file inside the repository.**
`appsettings.Development.json` **is committed** — it already carries the local database password,
which is a local value that is meant to be shared. An API key is not. The user-secrets store sits
outside the repository, per machine, and is read automatically in Development.

**Configuration decides which parser is registered, at startup:**

```
OpenAi:ApiKey missing or blank  →  NotConfiguredMealParser  →  503, fall back to search
OpenAi:ApiKey present           →  OpenAiMealParser
```

A blank key counts as no key. Half-configured is the state a copied `.env` line produces, and it
should behave like the honest thing it is rather than like a broken call.

**Usage is logged before the answer is read**, so a refusal and a failure still report what they
cost. The order inside is: count the tokens, then check `Refusal`, then read the content — the
architecture asks for the refusal check before the content, and the cost line sits above both
because the bill arrives whatever the answer was.

## Alternatives considered

**The Responses API, for `prompt_cache_key`.** It carries both features. Its answer is a list of
output items rather than a content list, so reading the JSON back is a step longer, and the
architecture named Chat Completions first. Worth revisiting on the cached-token measurement, not
before it.

**A hand-written JSON schema string.** No exporter surprises, and two definitions of the same shape
to keep in step. The record is the source; a schema that disagrees with it fails at deserialisation
instead of at the call.

**A timeout enforced with a linked `CancellationTokenSource`.** The SDK has
`OpenAIClientOptions.NetworkTimeout`, which is the same budget expressed where the network is. The
request's own token still cancels the call when the person closes the tab.

## Consequences

**Nothing has reached the network yet.** Everything here was checked against the SDK's types and
the exporter's real output. The model id `gpt-5.6-luna` is a default in code and a test pins it,
but only a live call proves it exists.

**`cached_tokens` is the number to look at first.** The architecture is explicit: if it stays 0
across repeated calls, the cache design does not work whatever the code looks like. The catalogue
is around 20 000 tokens, far above the 1 024 that caching needs, so a zero would mean the prefix is
not identical — the first suspect being the order the catalogue is written in.

**The per-person daily cap is still not written.** FR-14 remains the only variable cost, and now it
can actually be spent.
