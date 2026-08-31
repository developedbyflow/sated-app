namespace Sated.Api.Dtos;

public record CalorieTargetResponseDto(int? Kcal, string? Warning)
{
    private const int TalkToADoctorBelow = 1200;

    public static CalorieTargetResponseDto For(int? kcal) => new(
        kcal,
        kcal < TalkToADoctorBelow
            ? "Below 1,200 calories a day. Consider talking to a doctor."
            : null);
}
