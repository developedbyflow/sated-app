---
title: What a food is to the engine
---

# What a food is to the engine

**File:** [`server/Sated.Scoring/FoodInput.cs`](https://github.com/developedbyflow/sated-app/blob/main/server/Sated.Scoring/FoodInput.cs)

`FoodInput` is the **only thing that enters the engine**. Everything downstream is derived from it,
so its shape decides what the engine is capable of noticing.

To the engine, a food is a category, 16 numbers per 100 g, and one flag saying whether leucine was
measured or estimated. It has no name, no image, and no portion size — *which* food it is does not
affect the grade.

```csharp
public record FoodInput(
    string? Category,
    double  Calories,
    double  Protein,
    double  Fat,
    double  Fiber,
    double? VitaminA,
    double? VitaminC,
    // ... five more optional micronutrients
    double  SaturatedFat,
    double  Sodium,
    double? VitaminD,
    double? Thiamine,
    double? LeucinePer100g = null,
    bool    LeucineIsEstimated = false
);
```

## Zero never means missing

Look at why `VitaminA` may be null and `SaturatedFat` may not.

A food with 0 mg of vitamin A and a food the catalogue **says nothing about** are different things.
The first is a measurement; the second is a hole.

Conflating them grades a food as though it had no vitamin A when the truth is that nobody knows.
The engine drops the component from the calculation instead — which is why the combiner divides by
the weight actually used rather than by 100, and why a component that came back empty leaves both
sums. See [Architecture, level 3](architecture.md#level-3-components-inside-the-engine).

## Optional value versus optional argument

Two properties that look related and are not. Both appear in the signature above.

| Written as | Means |
|---|---|
| `double? VitaminD` | the value may be null — **but the caller must still pass it** |
| `double? LeucinePer100g = null` | may be null **and the caller may omit it entirely** |

The second is a default argument value. The distinction is not cosmetic, and the engine carries a
scar to prove it.

`VitaminD` and `Thiamine` once carried `= null`. Callers were allowed to stay silent, and they did.
`PortionAggregate` dropped both without a word, and two measurement tools graded the **GLP-1
lens** — the lens defined precisely by vitamin D and thiamine — as though no food in the catalogue
contained either.

Nothing crashed. Grades came out. They were simply wrong.

Removing the two defaults turned a silent mistake into a compile error naming every caller that had
forgotten. That is the rule this file follows: **if the engine must never see a value absent, give
it no default**, and let the compiler enumerate the callers.

## When this record gains a field

Three places must change together, or the addition fails silently somewhere far away — see
[Change the engine safely](../how-to/change-the-engine-safely.md).
