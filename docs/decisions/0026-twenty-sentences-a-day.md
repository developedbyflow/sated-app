---
title: 0026 — Twenty sentences a day, because thirty-five is the whole subscription
---

# 0026 — Twenty sentences a day, because thirty-five is the whole subscription

**Status:** accepted
**Date:** 2026-09-01

## Context

`POST /api/meals/parse` is the only thing in Sated that costs money per use. Everything else is
computed from data already held. The architecture said so and deferred the ceiling: *"rate limiting
is deferred, but FR-14 is the only variable cost. A daily cap per user is the only thing that
bounds the bill if somebody abuses it."*

It was deferred before the endpoint existed. It exists now, and a key is one command away.

### The number the measurement gave

The prompt is around 21 000 tokens — the catalogue measured in
[0023](0023-a-parsed-meal-is-a-proposal-nobody-saved.md) plus the sentence. At the architecture's
prices for `gpt-5.6-luna`, $0.20 and $1.20 per million, and roughly 200 tokens of answer, one
uncached call is **$0.0044**.

| cap per day | a month spent at the cap | of a $4.99 subscription |
|---|---|---|
| 5 | $0.67 | 13% |
| 10 | $1.33 | 27% |
| **20** | **$2.66** | **53%** |
| 30 | $4.00 | 80% |
| 50 | $6.66 | 133% |

**Above about 35 a day the ceiling stops being a ceiling** — one person at the limit costs more
than they pay. Ordinary use is three meals a day, which is $0.40 a month, 8%.

These are uncached prices. If prompt caching works as designed the real figures are a fraction of
these, so the cap is conservative in the right direction.

## Decision

**Twenty sentences per account per day**, configurable as `MealParsing:PerDay`. Nearly seven times
ordinary use, and a bad month is half a subscription rather than all of it.

**The day is a rolling twenty-four hours, not a calendar day.** The window opens on the first
sentence and closes a day later. No timezone question — a calendar day would need one, because the
account's day is not the server's. No midnight either, where every allowance in the product returns
in the same minute.

**A refusal names the moment, not a duration.** "Free at 2026-09-02 08:14:33Z" is something a
screen can render as a countdown; "try again later" is not.

**Only a sentence that was read counts.** A `503` from an unconfigured or failing provider takes
nothing from the day. The count, the window and the answer are written in one `SaveChanges`, after
the model has replied — nothing touches the account before that, so a failed call leaves no trace
at all.

**Two counters in the account row, not a table of calls.** `MealParsesUsed` and
`MealParseWindowStartedAt` on `AspNetUsers`. A table would grow by twenty rows per person per day
for a bound that needs two numbers. When the cost measurement of
[0024](0024-the-provider-is-chosen-by-a-key.md) needs per-call token counts stored rather than
logged, that is the moment a table earns itself.

**A second limit sits in front, six a minute per address block.** Different job: the daily cap
bounds the bill, the per-minute limit bounds a burst — including one made of calls that fail, which
cost money and do not count against the day.

## Alternatives considered

**A calendar day in UTC.** Simpler to explain and wrong for everybody not in UTC, whose allowance
returns in the middle of the afternoon.

**The ASP.NET rate limiter, partitioned by user.** It is already in the project and it holds its
windows in memory: a restart returns everybody's allowance, and a second instance doubles it. That
is acceptable for a login limiter, whose job is to slow a guess down. It is not acceptable for
something whose job is to bound a bill.

**A table of parse calls.** It would answer "how much did this person cost" without reading logs,
which is Q3's question. It also grows without a reason to keep the rows. Deferred until the
measurement actually needs it.

**No cap, and an alert on spend.** An alert tells you after the money is gone.

## Consequences

**A window that opens at 23:00 allows forty sentences in two hours** — twenty before it closes and
twenty after. The long-run rate is still twenty a day, which is what bounds the bill, and a rolling
window that never lets a burst through would need the timestamps this decision deliberately does
not keep.

**The cap is per account, so it is only as strong as the cost of an account.** Registering is free
and the address is not verified before use. Story 2.4 makes verification possible but deliberately
does not require it for access, so a determined abuser can still buy more allowance with more
accounts. The per-address-block limit is what stands in the way of doing that quickly.

**Nothing tells a person how much of the day they have left** until they run out. A screen showing
"14 of 20" needs a field in a response nothing currently returns.
