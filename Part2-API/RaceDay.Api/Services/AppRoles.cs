using System.Security.Claims;

namespace RaceDay.Api.Services;

public static class AppRoles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
}

public static class ClaimsExtensions
{
    /// <summary>Reads the logged-in user's id from the session cookie's claims.</summary>
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
