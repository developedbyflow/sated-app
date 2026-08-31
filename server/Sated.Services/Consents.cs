using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Data.Entities;

namespace Sated.Services;

public class Consents(SatedDbContext database, TimeProvider clock)
{
    public Task<ConsentDocument?> CurrentDocument(ConsentPurpose purpose) =>
        database.ConsentDocuments
            .Where(document => document.Purpose == purpose)
            .OrderByDescending(document => document.PublishedAt)
            .FirstOrDefaultAsync();

    public Task<bool> IsGiven(string userId, ConsentPurpose purpose) =>
        Signatures(userId, purpose).AnyAsync();

    public Task<Consent?> Standing(string userId, ConsentPurpose purpose) =>
        Signatures(userId, purpose).FirstOrDefaultAsync();

    public async Task<Consent?> Give(string userId, string version, ConsentPurpose purpose)
    {
        var document = await database.ConsentDocuments.FirstOrDefaultAsync(published =>
            published.Purpose == purpose && published.Version == version);

        if (document is null)
        {
            return null;
        }

        var standing = await Signatures(userId, purpose).FirstOrDefaultAsync();

        if (standing is not null && standing.DocumentId == document.Id)
        {
            return standing;
        }

        var consent = new Consent
        {
            UserId = userId,
            DocumentId = document.Id,
            GivenAt = Now()
        };

        database.Consents.Add(consent);
        await database.SaveChangesAsync();

        return consent;
    }

    public async Task<bool> Withdraw(string userId, ConsentPurpose purpose)
    {
        var standing = await Signatures(userId, purpose).ToListAsync();

        if (standing.Count == 0)
        {
            return false;
        }

        foreach (var consent in standing)
        {
            consent.WithdrawnAt = Now();
        }

        await Erase(userId, purpose);
        await database.SaveChangesAsync();

        return true;
    }

    private async Task Erase(string userId, ConsentPurpose purpose)
    {
        if (purpose is not ConsentPurpose.HealthData)
        {
            return;
        }

        var user = await database.Users.FirstAsync(account => account.Id == userId);

        user.WeightKg = null;
        user.HeightCm = null;

        var logged = await database.Days
            .Where(day => day.OwnerId == userId)
            .ToListAsync();

        database.Days.RemoveRange(logged);
    }

    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();

        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private IQueryable<Consent> Signatures(string userId, ConsentPurpose purpose) =>
        database.Consents.Where(consent =>
            consent.UserId == userId
            && consent.WithdrawnAt == null
            && consent.Document.Purpose == purpose);
}
