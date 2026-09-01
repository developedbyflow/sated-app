# Check the examples in the reference still hold

Every JSON example in [the HTTP reference](../reference/http-api.md) was true when it was written.
`tools/DocExampleCheck` says whether it still is: it calls the real API and compares each documented
example, field by field, with what comes back.

## Running it

Start the API, then run the check from its own folder:

```bash
dotnet run --project server/Sated.Api --launch-profile https
```

```bash
cd tools/DocExampleCheck && dotnet run
```

It exits `0` when everything matches and `1` when it does not, so it can gate a release.

## What it does to the database

It creates one account, consents, fills in a profile, sets a calorie target, logs one meal — and
**deletes the account at the end**, which takes the rest with it. The email is a fresh GUID every
run. Nothing else is touched: the catalogue is only read.

If the run is interrupted the account survives, named `doc-example-check-…@sated.test`.

## What it compares, and what it forgives

Every leaf of every documented example is compared with the same leaf of the response. A field that
appears in one and not the other is reported in both directions — that is how a response that grew
a field gets caught.

Three differences are forgiven, because they are not staleness:

| forgiven | why |
|---|---|
| ids, emails, and timestamps | different on every run by construction |
| a documented number with fewer decimals | `88.35` matches `88.34605164272968`; the reference rounds |
| a documented string ending in `…` | the reference shows the first line of a long consent text |

A **fragment** — an example showing part of a response, like the servings block — is compared one
way only: everything it names must match, and the response is allowed to carry more.

## When it reports something

Two kinds of answer, and they need opposite fixes:

- **`different value at …`** — usually the engine moved. `POST /api/grades` documented a score of
  88.03; the density percentiles were re-measured on 2026-08-23 and the weights refitted on
  2026-08-25, and nobody re-ran the example. **The reference is wrong, fix the reference.**
- **`in the response, absent from the documentation`** — the API grew a field. The list rows gained
  `source` when own foods arrived, and the export gained height, the calorie target, foods, recipes
  and meals. **The reference is incomplete, fix the reference.**

Both were real on 2026-09-01, which is why this tool exists.
