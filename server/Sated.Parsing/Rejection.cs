namespace Sated.Parsing;

public enum RejectionReason
{
    OutsideTheSelectedCategories,
    NotTheEatenForm,
    MissingRequiredNutrient
}

public record Rejection(int FdcId, string Description, RejectionReason Reason);
