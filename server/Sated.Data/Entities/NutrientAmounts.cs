namespace Sated.Data.Entities;

public class NutrientAmounts
{
    public required double Calories { get; set; }
    public required double Protein { get; set; }
    public required double Fat { get; set; }
    public required double Fiber { get; set; }
    public required double SaturatedFat { get; set; }
    public required double Sodium { get; set; }

    public double? VitaminA { get; set; }
    public double? VitaminC { get; set; }
    public double? VitaminD { get; set; }
    public double? VitaminE { get; set; }
    public double? Thiamine { get; set; }
    public double? Calcium { get; set; }
    public double? Iron { get; set; }
    public double? Magnesium { get; set; }
    public double? Potassium { get; set; }
    public double? Leucine { get; set; }
}
