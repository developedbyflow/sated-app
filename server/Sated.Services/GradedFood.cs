using Sated.Scoring;

namespace Sated.Services;

public record GradedFood(int Id, string Description, Grade? Grade, CombinedScore Score);
