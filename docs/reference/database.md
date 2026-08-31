---
title: Database
---

# Database

PostgreSQL 18, running in a container defined by `docker-compose.yml`. Schema is code-first: the
tables are generated from `Sated.Data` by EF Core migrations, never edited by hand in the database.

```bash
docker compose up -d
```

## Units — the one place they are written

**Every nutrient amount is per 100 g of the food.** Not per portion, not per serving. Column names
do not repeat it.

| Nutrient group | Unit |
|---|---|
| Calories | kcal |
| Protein, Fat, Fiber, SaturatedFat | g |
| Leucine | g |
| Sodium, Calcium, Iron, Magnesium, Potassium | mg |
| VitaminA, VitaminC, VitaminE, VitaminD, Thiamine | the unit the catalogue publishes for it |

## `Foods`

One row per food — the shared catalogue and every food a person typed in for themselves, in
the same table, told apart by `OwnerId`. Nutrients are an [owned type](../decisions/0004-nutrients-are-an-owned-type-on-food.md),
so they are columns in this same table rather than a related row.

| Column | Type | Null | Meaning |
|---|---|---|---|
| `Id` | integer identity | no | primary key, ours |
| `FdcId` | integer | yes | the USDA FoodData Central id, when the food came from there |
| `Description` | text | no | the catalogue's own name for the food |
| `Category` | text | no | the catalogue's category, stored exactly as it arrives — it selects the category rule (FR-6) |
| `Source` | text | no | where the row's numbers came from: `UsdaFndds` or `UserEntered`. No database default — an INSERT that forgets it fails |
| `OwnerId` | text | yes | **null means the shared catalogue.** Set means the row belongs to that one account and nobody else can see it |
| `Nutrients_Calories` | double precision | no | |
| `Nutrients_Protein` | double precision | no | |
| `Nutrients_Fat` | double precision | no | |
| `Nutrients_Fiber` | double precision | no | |
| `Nutrients_SaturatedFat` | double precision | no | |
| `Nutrients_Sodium` | double precision | no | |
| `Nutrients_VitaminA` | double precision | yes | |
| `Nutrients_VitaminC` | double precision | yes | |
| `Nutrients_VitaminD` | double precision | yes | |
| `Nutrients_VitaminE` | double precision | yes | |
| `Nutrients_Thiamine` | double precision | yes | |
| `Nutrients_Calcium` | double precision | yes | |
| `Nutrients_Iron` | double precision | yes | |
| `Nutrients_Magnesium` | double precision | yes | |
| `Nutrients_Potassium` | double precision | yes | |
| `Nutrients_Leucine` | double precision | yes | measured leucine; null means the engine estimates it from the category |

**Not null and no default.** The six required columns have no database default. Zero is a real
amount of fibre, so a default would let an incomplete INSERT record a food that reads as measured —
FR-7 says zero never means absent.

**Nullable means absent, and the engine knows the difference.** A null micronutrient is dropped
from the density score's denominator; a zero counts against the food.

### `OwnerId` is enforced by a global query filter, not by remembering

`SatedDbContext` registers one filter on `Food`:

```csharp
food.HasQueryFilter(entry => entry.OwnerId == null || entry.OwnerId == AskedBy);
```

EF Core appends it to **every** query it generates for `Foods`, so `GET /api/foods`, the detail
endpoint and `FoodGrading` all carry it without any of them mentioning it. The rule is written once
and cannot be forgotten in a place that never mentions it.

`AskedBy` is the signed-in account's id, supplied through `ICurrentUser` — an interface declared in
`Sated.Data` and implemented in `Sated.Api` over the HTTP request. The data layer says what it
needs; the web layer supplies it. Signed out, it is null and only the catalogue is visible.

`ICurrentUser` reads the id claim directly rather than through `UserManager`, and that is not a
style choice: `UserManager` needs `SatedDbContext`, so injecting it here makes the container refuse
to start with a circular dependency.

**The one place the filter is turned off on purpose** is `tools/CatalogueLoad`, which asks whether
the table is empty before it fills it, and must count rows belonging to everybody:
`context.Foods.IgnoreQueryFilters().CountAsync()`.

**Deleting an account deletes its foods.** The foreign key from `Foods.OwnerId` to `AspNetUsers.Id`
cascades, so the row goes when the owner does — the other half of FR-29.

### Provenance is one column, and the rest is derived

`Source` says where a **row** came from. Nothing says where a **field** came from, and that is a
measurement, not an omission: all 1,933 catalogue rows carry all fifteen nutrients, and none of
them carries leucine. There is no variation inside a row to record, so sixteen source columns
would store the same constant 29,000 times.

What `GET /api/foods/{id}` reports as `provenance` is computed from the row it already has:

| | |
|---|---|
| the value is there | it came from `Source` |
| the value is null and the engine fills it in | `estimated` — leucine only, from the category |
| the value is null and nothing fills it in | `absent` — the density score renormalises over the rest |

**The backfill was verified on the real catalogue, not in a test.** Migrations run against an empty
database in the test suite, so the `UsdaFndds` fill has no test behind it; it was checked by
counting rows in the development database after `database update`: 1,933 `UsdaFndds`, 0 anything
else.

## Loading the catalogue

`tools/CatalogueLoad` reads the FNDDS survey file, drops every row of `Foods` and inserts what
passes. The file is not in the repository — it is 63 MB — and lives at
`tools/UsdaCoverageQuery/data/surveyDownload.json`.

```bash
dotnet run --project tools/CatalogueLoad
```

### What the catalogue is for

**Sated supplies the building blocks; people assemble their own meals from them.** So the table
holds single foods and drinks — chicken, rice, broccoli, cheese, coffee — and not the survey's
cooked dishes, sandwiches, pizzas and desserts. A stew is something a person composes, not
something the catalogue ships.

That makes the filter a **list of categories to include**, held in
`Sated.Parsing/CatalogueCategories.cs`. Growing the catalogue is adding a category name to that
list and running the load again. A list of things to exclude would have to anticipate every kind of
row the survey contains, and would silently admit anything nobody thought of.

| Rule | Kept | Removed |
| --- | ---: | ---: |
| the category is one of the 71 selected | 1 935 | 3 497 |
| the description does not say `not reconstituted` | 1 933 | 2 |

**5 432 read · 1 933 stored** — 1 583 foods and 350 drinks.

The second rule stays even though the first removes most powders on its own: frozen orange juice
concentrate and frozen lemonade concentrate sit inside `Citrus juice` and `Fruit drinks`, which are
selected. A grade per 100 g of concentrate describes nothing anybody drinks.

A third check requires the six non-null nutrients before mapping. No food inside the selected
categories fails it, so it removes nothing today — it is there because the six columns are `NOT
NULL` and the mapping would otherwise throw rather than report.

Reading, filtering and mapping live in `Sated.Parsing`, with tests, because a nutrient mapped to
the wrong column fails silently: it produces a different grade, not an error.

## Migrations

| Migration | What it did |
|---|---|
| `20260830120815_CreateFoods` | created `Foods` with its four identity columns |
| `20260830122029_AddNutrientsToFoods` | added the sixteen `Nutrients_*` columns |
| `20260830144959_MakeFdcIdUnique` | made `FdcId` unique, so the same USDA food cannot land twice |
| `20260831070929_AddIdentityTables` | added the ASP.NET Identity tables, `AspNetUsers` among them |
| `20260831075022_AddProfileAndConsent` | added `WeightKg` and `ActiveLensId`, plus `ConsentDocuments` and `Consents` |
| `20260831125607_FoodBelongsToItsOwner` | added `Foods.OwnerId`, its index, and the cascade from `AspNetUsers` |
| `20260831131409_FoodCarriesItsSource` | added `Foods.Source`, filled the 1,933 existing rows with `UsdaFndds`, then dropped the default |

```bash
dotnet ef migrations add <Name> -p Sated.Data -s Sated.Api
dotnet ef database update -p Sated.Data -s Sated.Api
```

`-p` is the project that holds the migrations, `-s` the project that holds the connection string.
`dotnet ef database update <EarlierMigrationName>` rolls back to that migration.

## Connection

The connection string lives in `Sated.Api/appsettings.Development.json` under
`ConnectionStrings:Sated`. The development credentials are the same ones `docker-compose.yml`
creates, and they are deliberately in the repository: the database holds no data that is not
public. Anything with real user data gets its connection string from the environment.
