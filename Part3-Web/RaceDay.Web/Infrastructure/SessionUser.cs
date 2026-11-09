using System.Text.Json;

namespace RaceDay.Web.Infrastructure;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

/// <summary>The logged-in user, kept in the MVC session for the whole visit.</summary>
public record SessionUser(int UserId, string FullName, string Role)
{
    public bool IsOrganiser => Role == Roles.Organiser;
    public bool IsParticipant => Role == Roles.Participant;
}

public static class SessionExtensions
{
    private const string UserKey = "RaceDay.User";

    public static SessionUser? GetUser(this ISession session)
    {
        try
        {
            if (!session.IsAvailable) return null;
            var json = session.GetString(UserKey);
            return json is null ? null : JsonSerializer.Deserialize<SessionUser>(json);
        }
        catch (Exception) { return null; }
    }

    public static void SetUser(this ISession session, SessionUser user) =>
        session.SetString(UserKey, JsonSerializer.Serialize(user));
}
