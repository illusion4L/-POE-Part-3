using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public static class TestHelpers
{
    public const string Password = "Passw0rd!";

    /// <summary>A client with no session (anonymous).</summary>
    public static HttpClient Anonymous(RaceDayFactory f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Registers a fresh user and logs in. The returned client keeps the session cookie.</summary>
    public static async Task<(HttpClient Client, int UserId)> LoggedIn(RaceDayFactory f, string role)
    {
        var client = Anonymous(f);
        var email = $"{Guid.NewGuid():N}@raceday.test";

        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Test User", email, password = Password, role });
        reg.EnsureSuccessStatusCode();
        var auth = (await reg.Content.ReadFromJsonAsync<AuthResponse>())!;

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        return (client, auth.UserId);
    }

    public static async Task<EventDto> CreateEvent(HttpClient organiser, string name = "Test 10K")
    {
        var resp = await organiser.PostAsJsonAsync("/api/events", new
        {
            name,
            description = "Test event",
            eventDate = DateTime.UtcNow.AddDays(30),
            location = "Johannesburg",
            distance = 10,
            eventType = "Run"
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<EventDto>())!;
    }

    public static async Task<CategoryDto> CreateCategory(HttpClient organiser, int eventId)
    {
        var resp = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Senior", minAge = 20, maxAge = 59, categoryDistance = 10 });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CategoryDto>())!;
    }
}
