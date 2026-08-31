using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Data.Entities;

namespace Sated.Services;

public enum ProfileUpdate
{
    Saved,
    ConsentMissing,
    UnknownLens
}

public class Profiles(SatedDbContext database, Consents consents, FoodGrading grading)
{
    public Task<AppUser> Of(string userId) =>
        database.Users.FirstAsync(account => account.Id == userId);

    public async Task SetCalorieTarget(string userId, int kcal)
    {
        var user = await Of(userId);

        user.CalorieTargetKcal = kcal;

        await database.SaveChangesAsync();
    }

    public async Task ClearCalorieTarget(string userId)
    {
        var user = await Of(userId);

        user.CalorieTargetKcal = null;

        await database.SaveChangesAsync();
    }

    public async Task<ProfileUpdate> Update(
        string userId, double weightKg, double heightCm, string? lensId)
    {
        if (!await consents.IsGiven(userId, ConsentPurpose.HealthData))
        {
            return ProfileUpdate.ConsentMissing;
        }

        var lens = grading.LensFor(lensId);

        if (lens is null)
        {
            return ProfileUpdate.UnknownLens;
        }

        var user = await Of(userId);

        user.WeightKg = weightKg;
        user.HeightCm = heightCm;
        user.ActiveLensId = lens.Id;

        await database.SaveChangesAsync();

        return ProfileUpdate.Saved;
    }
}
