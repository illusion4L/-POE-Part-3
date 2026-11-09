using System.Text.Json;

namespace RaceDay.Web.Infrastructure;

/// <summary>An unsuccessful answer from the RaceDay API (or no answer at all).</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }
    public IReadOnlyList<string> Errors { get; }

    public ApiException(int statusCode, string message, IReadOnlyList<string>? errors = null) : base(message)
    {
        StatusCode = statusCode;
        Errors = errors is { Count: > 0 } ? errors : new[] { message };
    }

    public static async Task<ApiException> FromResponseAsync(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        string? title = null;
        var errors = new List<string>();

        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                        title = t.GetString();
                    if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Object)
                        foreach (var field in errs.EnumerateObject())
                            if (field.Value.ValueKind == JsonValueKind.Array)
                                errors.AddRange(field.Value.EnumerateArray()
                                    .Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!));
                }
            }
        }
        catch (JsonException) { /* not JSON - fall through to the generic message */ }

        // Only the API's deliberate, user-facing messages are passed on (validation, conflicts, bad login).
        // For everything else the user gets a generic message - never raw server text.
        var userFacing = status is 400 or 401 or 409 or 422;
        var friendly = userFacing && !string.IsNullOrWhiteSpace(title) ? title! : DefaultMessage(status);
        if (status is not (400 or 409 or 422)) errors.Clear();
        return new ApiException(status, errors.Count > 0 ? errors[0] : friendly, errors);
    }

    public static string DefaultMessage(int status) => status switch
    {
        400 => "Some of the details you entered aren't valid. Please check them and try again.",
        401 => "Please log in to continue.",
        403 => "You don't have permission to do that.",
        404 => "We couldn't find what you were looking for.",
        409 => "That conflicts with something that already exists.",
        >= 500 => "RaceDay is having trouble right now. Please try again in a moment.",
        _ => "Something went wrong. Please try again."
    };
}
