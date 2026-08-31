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
| `TypicalGrams` | double precision | yes | what USDA assumes was eaten when the survey respondent did not say. Null when the file carried no such row |
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

## `FoodServings`

The named household measures a person can pick instead of typing grams. Imported from the same
FNDDS file the catalogue came from: **6,810 rows across all 1,933 foods, five each on average, none
without.**

| Column | Type | Null | Meaning |
|---|---|---|---|
| `Id` | integer identity | no | |
| `FoodId` | integer | no | cascades from `Foods` |
| `Description` | text | no | USDA's own wording — `1 egg`, `1 cup`, `1 fl oz` |
| `Grams` | double precision | no | what that measure weighs |
| `Sequence` | integer | no | USDA's `sequenceNumber`, kept so the order is theirs |

### The trap, and it is not where the PRD says it is

The product brief and PRD both record that *"USDA marks no portion as default, and the first in the
list gives one egg = 243 g"*. Measured against the files:

- **`foodPortions` is not in order in the JSON.** For `Egg, whole, raw, fresh` in SR Legacy, array
  element `[0]` is `243 g — cup (4.86 large eggs)`, which carries `sequenceNumber: 5`. The row with
  `sequenceNumber: 1` is `50 g — large`.
- **In FNDDS, the catalogue actually shipped ([0005](../decisions/0005-fndds-is-the-catalogue.md)),
  all seventeen egg entries give `1 egg = 50–55 g` first.** The 243 g figure does not appear.

So the rule is not "USDA cannot be trusted for portions" but **"sort by `sequenceNumber`, never take
`foodPortions[0]`"**. `SurveyPortionsTests` guards it with the 243 g row placed first in the array,
where it was found.

Decision G's conclusion is untouched: the engine is handed the quantity a person logged, never a
portion it deduced.

### `TypicalGrams` is a different question from `Servings`

USDA does mark a default, in a row described literally as `Quantity not specified` — the amount
assumed when a survey respondent did not say how much. **1,932 of our 1,933 foods have one.** It is
kept out of `FoodServings` (nobody picks "quantity not specified" off a list) and stored on `Foods`.

It is not a copy of one of the named servings: measured, it matches one exactly **40.7%** of the
time. Egg → 50 g, the weight of one egg. `Milk, NFS` → 244 g, one cup. Camembert → 21 g.

Inert until FR-14, which needs an amount when a typed sentence did not carry one.

## `Recipes` and `RecipeIngredients`

A recipe is a saved composition of foods with weights. It belongs to exactly one account — there is
no shared catalogue of recipes — and it is behind the same kind of global query filter as `Foods`.

| `Recipes` | Type | Null | Meaning |
|---|---|---|---|
| `Id` | integer identity | no | |
| `Name` | text | no | what the person called it |
| `OwnerId` | text | no | **required**, unlike `Foods.OwnerId`. Cascades from `AspNetUsers` |

| `RecipeIngredients` | Type | Null | Meaning |
|---|---|---|---|
| `Id` | integer identity | no | |
| `RecipeId` | integer | no | cascades from `Recipes` |
| `FoodId` | integer | no | cascades from `Foods` — see below |
| `Grams` | double precision | no | the weight of this food in the recipe |

**No nutrient columns.** The profile is computed on every read by `PortionAggregate`, which sums the
nutrients across the portions and restates them per 100 g of total weight. A stored profile would
be wrong the moment an ingredient changed, and there is nothing expensive about the arithmetic.

**`FoodId` cascades, and that was measured rather than chosen.** It was `Restrict` first, on the
reasoning that a food in use should not vanish. Deleting an account then failed with a `500`:
the account's own foods and its recipes both cascade from `AspNetUsers`, and `Restrict` on the way
to `RecipeIngredients` blocks it. The test `DeletingTheAccount_WithARecipeOverMyOwnFood_TakesEverything`
is what found it.

The consequence to know: if a delete-a-food endpoint is ever added, a recipe will silently lose an
ingredient. There is no such endpoint today, and the catalogue is never deleted from
([0006](../decisions/0006-load-the-catalogue-once-then-own-it.md)), so account deletion — where
everything goes anyway — is the only path that reaches it.

**Grams only.** The architecture asks for `DisplayAmount`/`DisplayUnit` alongside, so that "2 eggs"
survives a round trip. Turning "2 eggs" into grams needs `Food.Servings`, which does not exist yet,
so neither does the display pair.

### Loading the servings

The catalogue was loaded before servings existed, and `tools/CatalogueLoad` refuses to run on a
non-empty table by design ([0006](../decisions/0006-load-the-catalogue-once-then-own-it.md)). So
they arrive through a second tool that fills a gap rather than rebuilding anything:

```bash
cd tools/ServingsLoad && dotnet run
```

It refuses in the same way if `FoodServings` already holds rows. A fresh `CatalogueLoad` fills both
in one pass — `CatalogueImport` sets them alongside the nutrients — so this tool exists only for
catalogues loaded before 2026-08-31.

## `Days`, `Meals` and `MealEntries`

| `Days` | Type | Null | Meaning |
|---|---|---|---|
| `Id` | integer identity | no | |
| `OwnerId` | text | no | cascades from `AspNetUsers` |
| `Date` | date | no | **the local date, frozen when the meal was logged** |

| `Meals` | Type | Null | Meaning |
|---|---|---|---|
| `Id` | integer identity | no | |
| `DayId` | integer | no | cascades from `Days` |
| `Name` | text | no | what the person called it |
| `LoggedAt` | timestamptz | no | when it was recorded |
| `EngineVersion` | text | no | `calibration.json`'s `version`, in force at logging |

| `MealEntries` | Type | Null | Meaning |
|---|---|---|---|
| `Id` | integer identity | no | |
| `MealId` | integer | no | cascades from `Meals` |
| `FoodId` | integer | no | cascades from `Foods` |
| `QuantityGrams` | double precision | no | the truth for the engine, frozen |
| `DisplayAmount` | double precision | no | what the person said — `2` |
| `DisplayUnit` | text | no | what they said it in — `1 egg`, or `g` |
| `QuantityEstimated` | boolean | no | false everywhere until FR-14 proposes a quantity |

**`OwnerId` is on `Day`, not on `Meal`.** A meal reaches its owner through its day, and its query
filter says so: `meal.Day.OwnerId == AskedBy`.

### Why the date is stored and not derived

`LoggedAt` is a UTC instant; the date a person means is a local one. Deriving it later needs a time
zone, and a time zone that changes would silently reshuffle history — a meal logged at 23:30 in
Bucharest would move to the previous day after a flight. So the client sends the date and it is
frozen, the same principle as `QuantityGrams`: **derived values are recomputed, recorded inputs are
not.**

`(OwnerId, Date)` is unique — one row per person per day.

### The three quantity fields

All three come from the architecture, and the middle two exist for one reason: **you cannot recover
"2 eggs" from 100 g.** Open an entry to edit it and, without them, the "2 eggs" you typed is gone.

`QuantityEstimated` is written and never set true yet. It belongs to FR-14, where the system
proposes a quantity the sentence did not carry.

### `Meal`'s query filter is defence with no test behind it

Removing it changes nothing today: every query that reaches a `Meal` also includes its `Day`, and
EF filters the meal out when the day it requires is filtered away — measured, the stranger still
gets a `404`. The filter stays for the query nobody has written yet, which is the whole argument
for global filters in [0011](../decisions/0011-a-food-belongs-to-one-account-or-to-the-catalogue.md).
It is stated here because no test can distinguish it.

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
| `20260831151030_AddRecipes` | added `Recipes` and `RecipeIngredients` |
| `20260831162538_FoodCarriesItsServings` | added `FoodServings` and `Foods.TypicalGrams` |
| `20260831162706_PluraliseTheChildTables` | renamed `RecipeIngredient` and `FoodServing` to their plural forms, matching every other table |
| `20260831164107_AddDaysAndMeals` | added `Days`, `Meals` and `MealEntries` |

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
