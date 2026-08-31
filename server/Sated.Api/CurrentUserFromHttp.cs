using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sated.Data;

namespace Sated.Api;

public class CurrentUserFromHttp(
    IHttpContextAccessor requests, IOptions<IdentityOptions> identity) : ICurrentUser
{
    public string? Id =>
        requests.HttpContext?.User.FindFirstValue(identity.Value.ClaimsIdentity.UserIdClaimType);
}
