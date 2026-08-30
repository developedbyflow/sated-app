---
title: 0007 — Test the foods query against a real database
---

# 0007 — Test the foods query against a real database

**Status:** accepted
**Date:** 2026-08-30

## Context

`GET /api/foods` shipped on 2026-08-30 with search, category filtering and paging over the 1 933
catalogue rows. It was checked by hand through the eleven requests in `Sated.Api.http`. It had no
automated test, and it is the first endpoint that reads from the database at all.

The nine API tests that already existed do not open a connection. `GradesEndpointTests` and
`LensesEndpointTests` exercise the scoring engine, which is a pure library. Measured on 2026-08-30
by pointing the new fixture at a port with nothing behind it: those nine still pass, and only the
eleven new ones fail.

Two things the endpoint depends on are not decided by C#:

- **Case-insensitive search.** The filter is `EF.Functions.ILike`, which translates to PostgreSQL's
  `ILIKE`. That function exists only in the Npgsql provider.
- **Row order.** `OrderBy(food => food.Description)` becomes an `ORDER BY` that the database
  resolves using its collation. Measured with `psql -c '\l'` on 2026-08-30: every database in the
  `sated-db` container is `en_US.utf8` under the libc provider. .NET's own string comparison follows
  different rules.

Five of the eleven tests turn on one of those two. The remaining six — paging arithmetic, the
`total` count, the `pageSize` cap, the shape of a list row — would run against any provider.

**Every test also runs on a build server.** `.github/workflows/ci.yml` runs
`dotnet test server/Sated.slnx` on each push to `main` and each pull request, on a machine that
starts empty. Before this change it had no database, because nothing needed one.

A second database costs nothing new. The container runs one PostgreSQL **server**, and a server
holds many databases: before this change it already held `postgres`, `sated`, `template0` and
`template1`. Only the `Database=` part of the connection string picks between them, so a test
database needs no new container, port, user or password, and no change to `docker-compose.yml`.

**One measurement decided the shape of the seed data.** With the nine seeded foods written in
alphabetical order, deleting `.OrderBy` from `FoodsController` left all twenty tests passing:
without an `ORDER BY`, PostgreSQL returned the rows in insertion order, which happened to be the
order the tests asserted. The seed was rewritten in a scrambled order; the same deletion now fails
four tests.

## Decision

**API tests that read data run against a real PostgreSQL**, in a second database named `sated_test`
on the same server that holds `sated`.

**The test run owns `sated_test`.** `FoodsDatabase`, an xunit class fixture, drops it, rebuilds it
by applying the migrations, and inserts nine foods — once per run, before the first test.

**Only configuration is overridden.** The fixture adds one in-memory configuration source holding
`ConnectionStrings:Sated`, which sits above `appsettings.Development.json` in the stack that
`Program.cs` already reads. No production code knows it is being tested.

**Tests that share the fixture only read.** They run against one database built once, so a test
that wrote would change what the next one sees.

**The seeded rows are stored in a deliberately non-alphabetical order**, for the reason measured
above. Sorting them would look like tidying and would silently disarm four tests.

## Alternatives considered

**The EF Core in-memory provider.** The standard advice, and it needs no container at all. It
fails on the exact thing worth testing: `EF.Functions.ILike` has no implementation outside Npgsql,
and ordering would follow .NET's comparison rules rather than the database's. A test passing there
would show that the LINQ compiles, not that the query returns the right rows.

**SQLite in memory.** Closer, because it is real SQL and does execute an `ORDER BY`. It still has
no `ILIKE`, and its `LIKE` is already case-insensitive for ASCII — so a case test would pass
against SQLite and could still be wrong against PostgreSQL. A test that passes for a reason the
production database does not share is worse than no test.

**Run against `sated` itself.** The endpoint only reads, so nothing would be lost by accident
today. Rejected because the assertions would then be about the catalogue's contents — `search=milk`
returns 93 rows there — and every correction to a food would break tests that have nothing to do
with that food. Nothing would enforce the read-only part either; one careless test would take the
1 933 rows with it.

**Testcontainers**, a library that starts a throwaway database container from inside the test run.
A real contender, and the usual answer to "the build server has no database". It loses because
GitHub Actions already answers that with **service containers** — a block of YAML that starts
`postgres:18` beside the job and waits for its healthcheck. That needs no package, no Docker API
call from C#, and no difference between what runs locally and what runs on the server. Testcontainers
earns its cost when the test run has to control the container itself: several databases at once,
or a different version per test. Neither is true here.

**A second container running its own PostgreSQL.** Rejected because it buys nothing. One server
already holds many databases; a second server would double what has to be running and what has to
be kept in step.

## Consequences

- **`dotnet test` now needs a running PostgreSQL.** With `sated-db` stopped, eleven tests fail. The
  154 engine tests, the 9 parsing tests and the 9 older API tests keep passing, because none of them
  opens a connection. That separation is worth protecting: the engine is a pure library, and its
  tests should stay runnable on a machine with no database at all.
- **CI grew a database.** `ci.yml` now starts a `postgres:18` service container with the same
  database name, user and password as `docker-compose.yml`, and waits for `pg_isready` before the
  job runs. The test code is identical in both places: it connects to `localhost:5432` and creates
  `sated_test` itself.
- **Two files now describe the same server and have to stay in step.** `docker-compose.yml` and
  `ci.yml` each pin `postgres:18` with the same credentials. Moving one to a later major version and
  not the other can change the collation, and the collation is what five of these tests assert
  against. Nothing enforces the pairing.
- **The migrations are exercised on every run.** Rebuilding `sated_test` applies all three from
  empty. A migration that no longer applies now breaks the test run, which is a check we did not
  have before.
- **`sated_test` is dropped at the start of a run, not at the end.** After a failure the rows are
  still there to open in `psql`. The cost is one leftover database sitting in the container.
- **The next test that needs to write needs a different arrangement**, because the database is
  built once for the whole class. The usual answers are a transaction rolled back after each test,
  or a fixture per test class. Neither is built.
- **The development credentials are now written in plain text in a public repository**, in
  `FoodsDatabase.cs`. They are the same ones already in `appsettings.Development.json`. This locks
  in that the development database never holds anything real — no live account, no export of a
  user's log — and that the production connection string arrives another way.
- **The seeded catalogue is nine rows, and every assertion counts them by hand.** Adding a tenth
  food breaks several tests on purpose. That is the trade for assertions that name exact rows
  instead of ranges.
