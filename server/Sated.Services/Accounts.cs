using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Data.Entities;

namespace Sated.Services;

public class Accounts(SatedDbContext database)
{
    public Task<Consent[]> SignedDocuments(string userId) =>
        database.Consents
            .Where(consent => consent.UserId == userId)
            .Include(consent => consent.Document)
            .OrderBy(consent => consent.GivenAt)
            .ToArrayAsync();

    public async Task Delete(AppUser user)
    {
        database.Users.Remove(user);

        await database.SaveChangesAsync();
    }
}
