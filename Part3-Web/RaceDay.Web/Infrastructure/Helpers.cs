using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RaceDay.Web.Infrastructure;

public static class Fmt
{
    public static string Date(DateTime d) => d.ToString("ddd, d MMM yyyy");
    public static string DateTime_(DateTime d) => d.ToString("ddd, d MMM yyyy 'at' HH:mm");
    public static string Km(decimal d) => d.ToString("0.##") + " km";
    public static string Time(TimeSpan t) => t.ToString(@"hh\:mm\:ss");

    /// <summary>47 -> 47th, 1 -> 1st, 12 -> 12th.</summary>
    public static string Ordinal(int n)
    {
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return n + suffix;
    }

    public static string AgeRange(int? min, int? max) => (min, max) switch
    {
        (null, null) => "All ages",
        (int a, null) => $"{a}+",
        (null, int b) => $"Up to {b}",
        (int a, int b) => $"{a}–{b}"
    };

    public static string EventIcon(string type) => type switch
    {
        "Walk" => "bi-person-walking",
        "Cycle" => "bi-bicycle",
        _ => "bi-lightning-charge-fill"
    };

    public static string Countdown(DateTime eventDate)
    {
        var days = (eventDate.Date - DateTime.Today).Days;
        return days switch { < 0 => "Finished", 0 => "Today", 1 => "Tomorrow", _ => $"In {days} days" };
    }
}

public static class ModelStateExtensions
{
    /// <summary>Shows the API's validation/conflict messages in the form's validation summary.</summary>
    public static void AddApiErrors(this ModelStateDictionary modelState, ApiException ex)
    {
        foreach (var message in ex.Errors.Distinct())
            modelState.AddModelError(string.Empty, message);
    }
}
